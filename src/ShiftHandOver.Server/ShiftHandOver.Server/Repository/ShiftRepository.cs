using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public class ShiftRepository : IShiftRepository
    {
        private readonly ShiftHandoverDbContext _context;

        public ShiftRepository(ShiftHandoverDbContext context)
        {
            _context = context;
        }

        public List<ShiftTypeDTO> GetShiftTypes()
        {
            return new List<ShiftTypeDTO>
            {
                new ShiftTypeDTO { Code = "MORNING",   Name = "Ca Sáng" },
                new ShiftTypeDTO { Code = "AFTERNOON", Name = "Ca Chiều" },
                new ShiftTypeDTO { Code = "EVENING",   Name = "Ca Tối" },
                new ShiftTypeDTO { Code = "NIGHT",     Name = "Ca Đêm" }
            };
        }

        public async Task<ShiftHandoverDetailDTO> GetOrCreateShiftAsync(InitShiftRequestDTO req)
        {
            DateOnly sDate = DateOnly.FromDateTime(req.ShiftDate);
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == req.BranchId);
            string branchName = branch?.Name ?? $"Cơ sở {req.BranchId}";

            // 1. Kiểm tra xem ca làm việc này đã được tạo trong DB chưa
            var existingShift = await _context.Shifts
                .Include(s => s.ShiftPosEntries).ThenInclude(p => p.PosConfig)
                .Include(s => s.ShiftBankEntries).ThenInclude(b => b.BranchBank)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.ClosedByUser)
                .FirstOrDefaultAsync(s => s.BranchId == req.BranchId && s.ShiftDate == sDate && s.ShiftType == req.ShiftType);

            if (existingShift != null)
            {
                return MapToDetailDTO(existingShift, branchName);
            }

            // 2. Nếu chưa có -> Tìm ca chốt gần nhất của chi nhánh này để kế thừa số liệu cuối ca -> đầu ca hiện tại
            var prevShift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Where(s => s.BranchId == req.BranchId && (s.Status == "Closed" || s.Status == "ClosedNC" || s.Status == "Close" || s.Status == "CloseNC" || s.Status == "CLOSED"))
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            // Kiểm tra xem ca này có phải là khởi đầu ngày mới hay không
            bool isNewDay = req.ShiftType.Equals("MORNING", StringComparison.OrdinalIgnoreCase) 
                            || prevShift == null 
                            || sDate > prevShift.ShiftDate;

            // Kiểm tra xem ca liền trước có phải là Ca Đêm (NIGHT) của ngày hôm trước hay không
            bool prevIsYesterdayNight = prevShift != null 
                                       && prevShift.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase) 
                                       && prevShift.ShiftDate == sDate.AddDays(-1);

            // Mức tiền mồi két định mức do Admin cấu hình cho chi nhánh (mặc định 2.000.000)
            decimal defaultCash = branch?.DefaultCashOpening ?? 2000000m;
            decimal cashOpening;

            if (req.ShiftType.Equals("MORNING", StringComparison.OrdinalIgnoreCase))
            {
                // Riêng Ca Sáng: Kế thừa nguyên vẹn tiền mặt cuối ca từ Ca Đêm hôm trước nếu có
                cashOpening = (prevIsYesterdayNight && prevShift?.CashClosing != null)
                    ? prevShift.CashClosing.Value
                    : defaultCash;
            }
            else
            {
                // Các ca trong ngày (Chiều, Tối, Đêm): Mặc định luôn lấy định mức tiền mồi két do Admin thiết lập
                cashOpening = defaultCash;
            }

            // Tạo ca mới với trạng thái NConfirm
            var newShift = new Shift
            {
                BranchId = req.BranchId,
                ShiftDate = sDate,
                ShiftType = req.ShiftType,
                Status = "NConfirm",
                CashOpening = cashOpening,
                OpenedByUserId = req.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Shifts.Add(newShift);
            await _context.SaveChangesAsync();

            // 3. Khởi tạo danh sách POS cho ca
            var posConfigs = await _context.PosConfigs.Where(p => p.BranchId == req.BranchId && p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync();
            foreach (var pos in posConfigs)
            {
                var prevPos = prevShift?.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == pos.Id);
                decimal posOpening = 0m;

                if (isNewDay)
                {
                    // Sang ngày mới: Nếu hôm qua có ca đêm -> Lấy doanh thu từ 0h-2h30 (cột Đêm - PosClosingDay2) làm khởi điểm; ngược lại reset về 0
                    posOpening = prevIsYesterdayNight ? (prevPos?.PosClosingDay2 ?? 0m) : 0m;
                }
                else
                {
                    // Trong cùng ngày: Kế thừa cuối ca trước làm đầu ca hiện tại
                    posOpening = prevPos?.PosClosing ?? 0m;
                }

                newShift.ShiftPosEntries.Add(new ShiftPosEntry
                {
                    ShiftId = newShift.Id,
                    PosConfigId = pos.Id,
                    PosOpening = posOpening,
                    PosClosing = posOpening
                });
            }

            // 4. Khởi tạo danh sách Chuyển khoản ngân hàng cho ca
            var branchBanks = await _context.BranchBanks.Where(b => b.BranchId == req.BranchId && b.IsActive).OrderBy(b => b.SlotIndex).ToListAsync();
            foreach (var bank in branchBanks)
            {
                var prevBank = prevShift?.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == bank.Id);
                decimal bankOpening = 0m;

                if (isNewDay)
                {
                    // Sang ngày mới: Nếu hôm qua có ca đêm -> Lấy doanh thu từ 0h-2h30 (cột Đêm - BankClosingDay2) làm khởi điểm; ngược lại reset về 0
                    bankOpening = prevIsYesterdayNight ? (prevBank?.BankClosingDay2 ?? 0m) : 0m;
                }
                else
                {
                    // Trong cùng ngày: Kế thừa cuối ca trước làm đầu ca hiện tại
                    bankOpening = prevBank?.BankClosing ?? 0m;
                }

                newShift.ShiftBankEntries.Add(new ShiftBankEntry
                {
                    ShiftId = newShift.Id,
                    BranchBankId = bank.Id,
                    BankOpening = bankOpening,
                    BankClosing = bankOpening
                });
            }

            await _context.SaveChangesAsync();

            return MapToDetailDTO(newShift, branchName);
        }

        public async Task<bool> ConfirmStartAsync(ConfirmStartRequestDTO req)
        {
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId);
            if (shift == null) return false;

            shift.Status = (shift.Status != null && shift.Status.Contains("NC")) ? "ConfirmStartNC" : "ConfirmStart";
            shift.OpenedByUserId = req.UserId;
            shift.OpenedAt = DateTime.UtcNow;
            shift.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CloseShiftResponseDTO> CloseShiftAsync(CloseShiftRequestDTO req)
        {
            var shift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null) 
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Không tìm thấy ca làm việc cần chốt!" };
            }

            // 1. Kiểm tra chữ ký xác thực của nhân viên tham gia ca
            if (req.Signatures == null || req.Signatures.Count == 0)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Vui lòng nhập tài khoản và mật khẩu của nhân viên trực ca để ký chốt ca!" };
            }

            var verifiedUsers = new List<User>();
            foreach (var sign in req.Signatures)
            {
                if (string.IsNullOrWhiteSpace(sign.Username) || string.IsNullOrWhiteSpace(sign.Password))
                {
                    return new CloseShiftResponseDTO { Success = false, Message = "Tên tài khoản hoặc mật khẩu ký xác nhận không được để trống!" };
                }

                string uname = sign.Username.Trim().ToLower();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == uname && u.PasswordHash == sign.Password && u.IsActive);
                if (user == null)
                {
                    return new CloseShiftResponseDTO { Success = false, Message = $"Tài khoản hoặc mật khẩu của '{sign.Username}' không chính xác!" };
                }

                if (verifiedUsers.Any(u => u.Id == user.Id))
                {
                    return new CloseShiftResponseDTO { Success = false, Message = $"Nhân viên '{user.FullName}' bị nhập trùng lặp nhiều lần!" };
                }

                verifiedUsers.Add(user);
            }

            // 2. Validate số liệu cuối ca >= đầu ca
            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 1);
            if (pos1 != null && req.Pos1Closing < pos1.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Số liệu cuối ca của Sapo POS phải lớn hơn hoặc bằng đầu ca!" };
            }

            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 2);
            if (pos2 != null && req.Pos2Closing < pos2.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Số liệu cuối ca của KiotViet phải lớn hơn hoặc bằng đầu ca!" };
            }

            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 1);
            if (bank1 != null && req.Bank1Closing < bank1.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Số liệu cuối ca của TingTing phải lớn hơn hoặc bằng đầu ca!" };
            }

            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 2);
            if (bank2 != null && req.Bank2Closing < bank2.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Số liệu cuối ca của Zalo Pay phải lớn hơn hoặc bằng đầu ca!" };
            }

            // 3. Cập nhật thông tin ca (Nếu trước đó đã có NC thì lưu ClosedNC, ngược lại Closed)
            shift.Status = (shift.Status != null && shift.Status.Contains("NC")) ? "ClosedNC" : "Closed";
            shift.ClosedByUserId = verifiedUsers.First().Id;
            shift.ClosedAt = DateTime.UtcNow;
            shift.CashClosing = req.CashClosing;
            shift.CashDifference = req.CashDifference;
            shift.Note = req.Note;
            shift.UpdatedAt = DateTime.UtcNow;

            if (pos1 != null)
            {
                pos1.PosClosing = req.Pos1Closing;
                pos1.PosClosingDay2 = req.Pos1Night;
            }

            if (pos2 != null)
            {
                pos2.PosClosing = req.Pos2Closing;
                pos2.PosClosingDay2 = req.Pos2Night;
            }

            if (bank1 != null)
            {
                bank1.BankClosing = req.Bank1Closing;
                bank1.BankClosingDay2 = req.Bank1Night;
            }

            if (bank2 != null)
            {
                bank2.BankClosing = req.Bank2Closing;
                bank2.BankClosingDay2 = req.Bank2Night;
            }

            // Cập nhật chi phí
            _context.ShiftExpenses.RemoveRange(shift.ShiftExpenses);
            if (req.Expenses != null)
            {
                foreach (var exp in req.Expenses)
                {
                    _context.ShiftExpenses.Add(new ShiftExpense
                    {
                        ShiftId = shift.Id,
                        Description = exp.Description,
                        Amount = exp.Amount,
                        CreatedByUserId = shift.ClosedByUserId.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // 4. Gắn nhân viên trực ca vào ShiftEmployees và cập nhật số ca làm
            _context.ShiftEmployees.RemoveRange(shift.ShiftEmployees);
            foreach (var user in verifiedUsers)
            {
                _context.ShiftEmployees.Add(new ShiftEmployee
                {
                    ShiftId = shift.Id,
                    UserId = user.Id
                });
                user.ShiftCount += 1;
            }

            await _context.SaveChangesAsync();

            return new CloseShiftResponseDTO
            {
                Success = true,
                Message = "Đã ký xác nhận và chốt ca thành công!",
                ConfirmedEmployeeNames = verifiedUsers.Select(u => u.FullName).ToList()
            };
        }

        public async Task<bool> RequestChangeInitialDataAsync(int shiftId, int userId)
        {
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
            if (shift == null) return false;

            shift.Status = "Changed";
            shift.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ConfirmChangeInitialDataAsync(ChangeInitialDataRequestDTO req)
        {
            var shift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null) return false;

            // Cập nhật thông tin đầu ca mới
            shift.CashOpening = req.CashOpening;
            shift.Status = "ConfirmStartNC"; // Sau khi xác nhận thay đổi đầu ca, chuyển sang ConfirmStartNC
            shift.OpenedByUserId = req.UserId;
            shift.OpenedAt = DateTime.UtcNow;
            shift.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(req.Note))
            {
                shift.Note = string.IsNullOrWhiteSpace(shift.Note) ? req.Note : (shift.Note + " | " + req.Note);
            }

            // Cập nhật PosOpening
            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 1);
            if (pos1 != null) pos1.PosOpening = req.Pos1Opening;

            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 2);
            if (pos2 != null) pos2.PosOpening = req.Pos2Opening;

            // Cập nhật BankOpening
            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 1);
            if (bank1 != null) bank1.BankOpening = req.Bank1Opening;

            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 2);
            if (bank2 != null) bank2.BankOpening = req.Bank2Opening;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<VerifyShiftOwnerResponseDTO> VerifyShiftOwnerAsync(VerifyShiftOwnerRequestDTO req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return new VerifyShiftOwnerResponseDTO { IsAuthorized = false, Message = "Vui lòng nhập đầy đủ tên tài khoản và mật khẩu!" };
            }

            string uname = req.Username.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == uname && u.PasswordHash == req.Password && u.IsActive);
            if (user == null)
            {
                return new VerifyShiftOwnerResponseDTO { IsAuthorized = false, Message = "Tài khoản hoặc mật khẩu không chính xác!" };
            }

            var shift = await _context.Shifts
                .Include(s => s.ShiftEmployees)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null)
            {
                return new VerifyShiftOwnerResponseDTO { IsAuthorized = false, Message = "Không tìm thấy ca làm việc cần chỉnh sửa!" };
            }

            // 1. Kiểm tra: Chỉ được thay đổi duy nhất một lần
            if (shift.Status != null && shift.Status.Contains("NC"))
            {
                return new VerifyShiftOwnerResponseDTO
                {
                    IsAuthorized = false,
                    Message = "Ca làm việc này chỉ được thay đổi một lần thôi và không thể điều chỉnh thêm nữa!"
                };
            }

            // 2. Kiểm tra xem người này có phải là nhân viên phụ trách ca hay không (nằm trong ShiftEmployees hoặc OpenedBy/ClosedBy hoặc Admin)
            bool isAssigned = shift.ShiftEmployees.Any(se => se.UserId == user.Id)
                              || shift.ClosedByUserId == user.Id
                              || shift.OpenedByUserId == user.Id
                              || user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            if (!isAssigned)
            {
                return new VerifyShiftOwnerResponseDTO
                {
                    IsAuthorized = false,
                    Message = "Bạn không phụ trách ca này"
                };
            }

            return new VerifyShiftOwnerResponseDTO
            {
                IsAuthorized = true,
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role,
                Message = $"Xác thực thành công nhân sự phụ trách ca: {user.FullName}."
            };
        }

        public async Task<bool> UpdateClosedShiftAsync(UpdateClosedShiftRequestDTO req)
        {
            var shift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null) return false;

            // Kiểm tra: Chỉ được thay đổi duy nhất một lần
            if (shift.Status != null && shift.Status.Contains("NC"))
            {
                return false;
            }

            // Cập nhật thông tin ca
            shift.CashOpening = req.CashOpening;
            shift.CashClosing = req.CashClosing;
            shift.CashDifference = req.CashDifference;
            shift.Status = "ClosedNC"; // Ca đã chốt được chỉnh sửa -> chuyển sang ClosedNC
            shift.UpdatedAt = DateTime.UtcNow;

            // Đảm bảo không vi phạm ràng buộc CHECK constraint CHK_Shifts_ClosedInfo
            if (shift.ClosedByUserId == null)
            {
                shift.ClosedByUserId = req.EditorUserId > 0 ? req.EditorUserId : (shift.OpenedByUserId ?? 1);
            }
            if (shift.ClosedAt == null)
            {
                shift.ClosedAt = DateTime.UtcNow;
            }

            // Cập nhật ghi chú kèm audit log
            string timeStr = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
            string auditEntry = $"[{timeStr}] Nhân viên '{req.EditorFullName}' đã chỉnh sửa ca: {req.ChangeLog}";
            if (!string.IsNullOrWhiteSpace(req.UserNote))
            {
                auditEntry += $" (Lý do: {req.UserNote})";
            }

            shift.Note = string.IsNullOrWhiteSpace(shift.Note) ? auditEntry : $"{shift.Note} | {auditEntry}";

            // Cập nhật POS
            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 1);
            if (pos1 != null)
            {
                pos1.PosOpening = req.Pos1Opening;
                pos1.PosClosing = req.Pos1Closing;
                pos1.PosClosingDay2 = req.Pos1Night;
            }

            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 2);
            if (pos2 != null)
            {
                pos2.PosOpening = req.Pos2Opening;
                pos2.PosClosing = req.Pos2Closing;
                pos2.PosClosingDay2 = req.Pos2Night;
            }

            // Cập nhật Bank
            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 1);
            if (bank1 != null)
            {
                bank1.BankOpening = req.Bank1Opening;
                bank1.BankClosing = req.Bank1Closing;
                bank1.BankClosingDay2 = req.Bank1Night;
            }

            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 2);
            if (bank2 != null)
            {
                bank2.BankOpening = req.Bank2Opening;
                bank2.BankClosing = req.Bank2Closing;
                bank2.BankClosingDay2 = req.Bank2Night;
            }

            // Cập nhật chi phí
            _context.ShiftExpenses.RemoveRange(shift.ShiftExpenses);
            if (req.Expenses != null)
            {
                foreach (var exp in req.Expenses)
                {
                    _context.ShiftExpenses.Add(new ShiftExpense
                    {
                        ShiftId = shift.Id,
                        Description = exp.Description,
                        Amount = exp.Amount,
                        CreatedByUserId = req.EditorUserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<EmployeeShiftStatisticsDTO>> GetEmployeeStatisticsAsync()
        {
            var users = await _context.Users.OrderBy(u => u.Id).ToListAsync();
            var closedShifts = await _context.Shifts
                .Include(s => s.ShiftEmployees)
                .Where(s => s.Status == "Closed" || s.Status == "ClosedNC" || s.Status == "Close" || s.Status == "CloseNC" || s.Status == "CLOSED")
                .ToListAsync();

            var result = new List<EmployeeShiftStatisticsDTO>();

            foreach (var u in users)
            {
                // Lấy tất cả các ca mà nhân viên này tham gia (qua ShiftEmployees hoặc ClosedByUserId)
                var userShifts = closedShifts
                    .Where(s => s.ShiftEmployees.Any(se => se.UserId == u.Id) || s.ClosedByUserId == u.Id)
                    .Distinct()
                    .ToList();

                int totalShifts = userShifts.Count;

                // Tách riêng ca âm và ca dương - KHÔNG CỘNG TRIỆT TIÊU
                int negativeCount = userShifts.Count(s => (s.CashDifference ?? 0m) < 0m);
                decimal totalNegative = userShifts.Where(s => (s.CashDifference ?? 0m) < 0m).Sum(s => s.CashDifference ?? 0m);

                int positiveCount = userShifts.Count(s => (s.CashDifference ?? 0m) > 0m);
                decimal totalPositive = userShifts.Where(s => (s.CashDifference ?? 0m) > 0m).Sum(s => s.CashDifference ?? 0m);

                int balancedCount = userShifts.Count(s => (s.CashDifference ?? 0m) == 0m);

                result.Add(new EmployeeShiftStatisticsDTO
                {
                    UserId = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Role = u.Role,
                    TotalShifts = totalShifts,
                    NegativeShiftsCount = negativeCount,
                    TotalNegativeDiff = totalNegative,
                    PositiveShiftsCount = positiveCount,
                    TotalPositiveDiff = totalPositive,
                    BalancedShiftsCount = balancedCount,
                    IsActive = u.IsActive
                });
            }

            return result;
        }

        private ShiftHandoverDetailDTO MapToDetailDTO(Shift s, string branchName)
        {
            string shiftTypeName = s.ShiftType switch
            {
                "MORNING" => "Ca Sáng",
                "AFTERNOON" => "Ca Chiều",
                "EVENING" => "Ca Tối",
                "NIGHT" => "Ca Đêm",
                _ => s.ShiftType
            };

            var pos1 = s.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 1);
            var pos2 = s.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 2);
            var bank1 = s.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 1);
            var bank2 = s.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 2);

            bool isClosed = s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));

            var dto = new ShiftHandoverDetailDTO
            {
                ShiftId = s.Id,
                BranchId = s.BranchId,
                BranchName = branchName,
                ShiftDate = s.ShiftDate.ToDateTime(TimeOnly.MinValue),
                ShiftType = s.ShiftType,
                ShiftTypeName = shiftTypeName,
                Status = s.Status ?? "NConfirm",
                IsReadOnly = isClosed,
                CashOpening = s.CashOpening,
                CashClosing = s.CashClosing,
                CashDifference = s.CashDifference,
                Note = s.Note,
                OpenedByUser = s.OpenedByUser?.FullName ?? "—",
                ClosedByUser = s.ClosedByUser?.FullName ?? "—",

                Pos1Opening = pos1?.PosOpening ?? 0m,
                Pos1Closing = pos1?.PosClosing,
                Pos1Night = pos1?.PosClosingDay2,

                Pos2Opening = pos2?.PosOpening ?? 0m,
                Pos2Closing = pos2?.PosClosing,
                Pos2Night = pos2?.PosClosingDay2,

                Bank1Opening = bank1?.BankOpening ?? 0m,
                Bank1Closing = bank1?.BankClosing,
                Bank1Night = bank1?.BankClosingDay2,

                Bank2Opening = bank2?.BankOpening ?? 0m,
                Bank2Closing = bank2?.BankClosing,
                Bank2Night = bank2?.BankClosingDay2,

                Expenses = s.ShiftExpenses.Select(e => new ShiftExpenseItemDTO
                {
                    Description = e.Description,
                    Amount = e.Amount
                }).ToList(),

                EmployeeNames = s.ShiftEmployees.Select(se => se.User?.FullName ?? "").Where(name => !string.IsNullOrEmpty(name)).ToList()
            };

            if (dto.EmployeeNames.Count == 0 && s.ClosedByUser != null && !string.IsNullOrEmpty(s.ClosedByUser.FullName))
            {
                dto.EmployeeNames.Add(s.ClosedByUser.FullName);
            }

            return dto;
        }

        public async Task<ShiftHandoverDetailDTO?> GetShiftByIdAsync(int id)
        {
            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.OpenedByUser)
                .Include(s => s.ClosedByUser)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return null;

            string branchName = shift.Branch?.Name ?? $"Cơ sở {shift.BranchId}";
            return MapToDetailDTO(shift, branchName);
        }

        public async Task<List<AdminShiftSummaryDTO>> GetAllShiftsAsync()
        {
            var shifts = await _context.Shifts
                .Include(s => s.Branch)
                .Include(s => s.ClosedByUser)
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync();

            return shifts.Select(s => MapToAdminSummary(s)).ToList();
        }

        public async Task<List<AdminShiftSummaryDTO>> GetRecentDifferencesAsync()
        {
            var shifts = await _context.Shifts
                .Include(s => s.Branch)
                .Include(s => s.ClosedByUser)
                .Where(s => s.CashDifference != null && s.CashDifference != 0)
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .Take(20)
                .ToListAsync();

            return shifts.Select(s => MapToAdminSummary(s)).ToList();
        }

        public async Task<List<AdminExpenseDTO>> GetAllExpensesAsync()
        {
            var expenses = await _context.ShiftExpenses
                .Include(e => e.CreatedByUser)
                .Include(e => e.Shift)
                .OrderByDescending(e => e.CreatedAt)
                .Take(50)
                .ToListAsync();

            return expenses.Select(e => new AdminExpenseDTO
            {
                Id = e.Id,
                ShiftCode = $"SH-{e.ShiftId}",
                CreatedAt = e.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                CreatedByUser = e.CreatedByUser?.FullName ?? "Nhân viên",
                Amount = e.Amount,
                AmountDisplay = $"{e.Amount:N0} đ",
                Description = e.Description,
                Note = e.Note ?? ""
            }).ToList();
        }

        private static AdminShiftSummaryDTO MapToAdminSummary(Shift s)
        {
            string typeName = s.ShiftType switch
            {
                "MORNING" => "Ca Sáng",
                "AFTERNOON" => "Ca Chiều",
                "EVENING" => "Ca Tối",
                "NIGHT" => "Ca Đêm",
                _ => s.ShiftType
            };

            string statusDisplay = s.Status switch
            {
                "Closed" => "Đã chốt",
                "ClosedNC" => "⚠️ Đã chốt (Có sửa - NC)",
                "Close" => "Đã chốt",
                "CloseNC" => "⚠️ Đã chốt (Có sửa - NC)",
                "ConfirmStart" => "Đang làm việc",
                "ConfirmStartNC" => "⚠️ Đang làm (Có sửa - NC)",
                "NConfirm" => "Mới mở ca",
                "NConfirmNC" => "⚠️ Mới mở ca (Đang sửa)",
                _ => s.Status ?? ""
            };

            decimal diff = s.CashDifference ?? 0m;
            string diffDisplay = diff > 0 ? $"+{diff:N0} đ" : (diff < 0 ? $"{diff:N0} đ" : "0 đ");

            return new AdminShiftSummaryDTO
            {
                Id = s.Id,
                ShiftCode = $"SH-{s.Id}",
                ShiftDate = s.ShiftDate.ToString("dd/MM/yyyy"),
                ShiftType = typeName,
                BranchName = s.Branch?.Name ?? $"Cơ sở {s.BranchId}",
                ClosedByUser = s.ClosedByUser?.FullName ?? "—",
                CashDifference = s.CashDifference,
                CashDifferenceDisplay = diffDisplay,
                Note = s.Note ?? "",
                Status = s.Status ?? "",
                StatusDisplay = statusDisplay
            };
        }
    }
}

