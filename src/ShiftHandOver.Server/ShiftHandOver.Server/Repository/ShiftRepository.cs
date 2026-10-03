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

        private int GetShiftOrder(string shiftType)
        {
            return shiftType?.ToUpper().Trim() switch
            {
                "MORNING" => 1,
                "AFTERNOON" => 2,
                "EVENING" => 3,
                "NIGHT" => 4,
                _ => 0
            };
        }

        private string GetShiftTypeName(string shiftType)
        {
            return shiftType?.ToUpper().Trim() switch
            {
                "MORNING" => "Ca Sáng",
                "AFTERNOON" => "Ca Chiều",
                "EVENING" => "Ca Tối",
                "NIGHT" => "Ca Đêm",
                _ => shiftType ?? ""
            };
        }

        private DateTime GetShiftStartTime(DateTime shiftDate, string shiftType)
        {
            return shiftType?.ToUpper().Trim() switch
            {
                "MORNING" => shiftDate.Date.AddHours(7),
                "AFTERNOON" => shiftDate.Date.AddHours(12),
                "EVENING" => shiftDate.Date.AddHours(18),
                "NIGHT" => shiftDate.Date.AddHours(23),
                _ => shiftDate.Date
            };
        }

        public async Task<int> AutoCloseExpiredShiftsAsync()
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

            // Tìm tài khoản Admin mặc định để làm người đóng/chốt ca
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin" && u.IsActive)
                         ?? await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
            if (adminUser == null) return 0;

            // Tìm tất cả các ca thuộc ngày trước hôm nay (ShiftDate < today) mà chưa được chốt
            // Áp dụng cho cả 4 ca: Sáng, Chiều, Tối, Đêm của ngày hôm trước
            var expiredShifts = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftEmployees)
                .Where(s => s.ShiftDate < today &&
                            (s.Status == null ||
                             (!s.Status.StartsWith("Closed") &&
                              !s.Status.StartsWith("Close")) ||
                             s.ClosedByUserId == null))
                .ToListAsync();

            if (!expiredShifts.Any()) return 0;

            foreach (var shift in expiredShifts)
            {
                shift.Status = (shift.Status != null && IsShiftModified(shift.Status)) ? "ClosedNC" : "Closed";
                shift.ClosedByUserId = adminUser.Id;
                shift.OpenedByUserId = shift.OpenedByUserId ?? adminUser.Id;
                shift.ClosedAt = DateTime.UtcNow;
                shift.UpdatedAt = DateTime.UtcNow;

                shift.CashClosing = shift.CashClosing ?? shift.CashOpening;
                shift.CashDifference = shift.CashDifference ?? 0m;

                string autoNote = "[Tự động chốt ca cuối ngày - Tài khoản Admin]";
                if (string.IsNullOrWhiteSpace(shift.Note))
                {
                    shift.Note = autoNote;
                }
                else if (!shift.Note.Contains(autoNote))
                {
                    shift.Note = shift.Note + " | " + autoNote;
                }

                // Tự động chốt PosClosing nếu chưa có
                foreach (var pos in shift.ShiftPosEntries)
                {
                    if (pos.PosClosing == null)
                    {
                        pos.PosClosing = pos.PosOpening;
                    }
                }

                // Tự động chốt BankClosing nếu chưa có
                foreach (var bank in shift.ShiftBankEntries)
                {
                    if (bank.BankClosing == null)
                    {
                        bank.BankClosing = bank.BankOpening;
                    }
                }

                // Gắn Admin vào danh sách nhân viên nếu chưa có ai
                if (!shift.ShiftEmployees.Any())
                {
                    shift.ShiftEmployees.Add(new ShiftEmployee
                    {
                        ShiftId = shift.Id,
                        UserId = adminUser.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            return expiredShifts.Count;
        }

        public async Task<ShiftHandoverDetailDTO> GetOrCreateShiftAsync(InitShiftRequestDTO req)
        {
            // 0. Tự động chốt các ca chưa chốt của các ngày hôm trước (hết ngày) bởi tài khoản Admin
            await AutoCloseExpiredShiftsAsync();

            DateOnly sDate = DateOnly.FromDateTime(req.ShiftDate);
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == req.BranchId);
            if (branch == null || !branch.IsActive)
            {
                throw new InvalidOperationException($"Cơ sở '{branch?.Name ?? req.BranchId.ToString()}' hiện đang tạm khóa hoặc ngừng hoạt động. Không thể mở ca làm việc!");
            }
            string branchName = branch.Name;

            DateTime now = DateTime.Now;
            DateTime shiftStart = GetShiftStartTime(req.ShiftDate, req.ShiftType);

            // 1. Kiểm tra xem ca làm việc này đã được tạo trong DB chưa
            var existingShift = await _context.Shifts
                .Include(s => s.ShiftPosEntries).ThenInclude(p => p.PosConfig)
                .Include(s => s.ShiftBankEntries).ThenInclude(b => b.BranchBank)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.ClosedByUser)
                .Include(s => s.OpenedByUser)
                .FirstOrDefaultAsync(s => s.BranchId == req.BranchId && s.ShiftDate == sDate && s.ShiftType == req.ShiftType);

            if (existingShift != null)
            {
                bool isClosedShift = existingShift.Status != null && (existingShift.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || existingShift.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));
                
                // Nếu ca trong DB thuộc tương lai (chưa tới giờ mở ca) và chưa được chốt hợp lệ
                if (now < shiftStart && !isClosedShift)
                {
                    throw new InvalidOperationException($"Không thể mở ca làm việc trong tương lai!\n" +
                        $"Ca {GetShiftTypeName(req.ShiftType)} ngày {sDate:dd/MM/yyyy} (bắt đầu lúc {shiftStart:HH:mm}) chưa tới giờ làm việc.\n" +
                        $"Thời gian hiện tại trên máy chủ: {now:HH:mm}.");
                }

                if (!isClosedShift)
                {
                    // Tự động bổ sung entry cho các POS hoặc Ngân hàng mới được kích hoạt/thêm vào
                    var currentPosConfigs = await _context.PosConfigs.Where(p => p.BranchId == req.BranchId && p.IsActive).ToListAsync();
                    bool entryAdded = false;
                    foreach (var pConf in currentPosConfigs)
                    {
                        if (!existingShift.ShiftPosEntries.Any(p => p.PosConfigId == pConf.Id))
                        {
                            existingShift.ShiftPosEntries.Add(new ShiftPosEntry
                            {
                                ShiftId = existingShift.Id,
                                PosConfigId = pConf.Id,
                                PosOpening = 0m,
                                PosClosing = 0m
                            });
                            entryAdded = true;
                        }
                    }

                    var currentBranchBanks = await _context.BranchBanks.Where(b => b.BranchId == req.BranchId && b.IsActive).ToListAsync();
                    foreach (var bBank in currentBranchBanks)
                    {
                        if (!existingShift.ShiftBankEntries.Any(b => b.BranchBankId == bBank.Id))
                        {
                            existingShift.ShiftBankEntries.Add(new ShiftBankEntry
                            {
                                ShiftId = existingShift.Id,
                                BranchBankId = bBank.Id,
                                BankOpening = 0m,
                                BankClosing = 0m
                            });
                            entryAdded = true;
                        }
                    }

                    if (entryAdded)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                return await MapToDetailDTOAsync(existingShift, branchName);
            }

            // 2. Nếu ca CHƯA ĐƯỢC TẠO (tạo ca mới / ca tiếp theo)
            // A. Tuyệt đối không cho phép tạo ca trong tương lai
            if (now < shiftStart)
            {
                throw new InvalidOperationException($"Không thể mở ca làm việc trong tương lai!\n" +
                    $"Ca {GetShiftTypeName(req.ShiftType)} ngày {sDate:dd/MM/yyyy} (bắt đầu lúc {shiftStart:HH:mm}) chưa tới giờ làm việc.\n" +
                    $"Thời gian hiện tại trên máy chủ: {now:HH:mm}. Vui lòng chỉ mở ca hiện tại hoặc ca quá khứ.");
            }

            // B. Khung giờ nghỉ giữa ca (02:30 – 07:00 sáng): chặn mở ca mới do cửa hàng đóng cửa
            if (now.TimeOfDay >= new TimeSpan(2, 30, 0) && now.TimeOfDay < new TimeSpan(7, 0, 0))
            {
                throw new InvalidOperationException("Cửa hàng đang trong khung giờ đóng cửa nghỉ giữa ca (02:30 – 07:00 sáng).\n" +
                    "Hệ thống không cho phép mở ca làm việc mới vào thời điểm này.");
            }

            // C. Không cho phép tạo mới ca của ngày trong quá khứ (ngoại trừ ca Đêm hôm trước đang chạy đến 02:30)
            bool isYesterdayNightRunning = req.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase)
                && req.ShiftDate.Date == DateTime.Today.AddDays(-1)
                && now.TimeOfDay < new TimeSpan(2, 30, 0);

            if (req.ShiftDate.Date < DateTime.Today && !isYesterdayNightRunning)
            {
                throw new InvalidOperationException($"Không thể tạo ca làm việc mới cho ngày trong quá khứ ({sDate:dd/MM/yyyy}).\n" +
                    $"Chỉ có thể tra cứu xem lại các ca quá khứ đã được chốt sổ trước đó.");
            }

            // D. Bắt buộc ca trước phải chốt ca và có người ký tên
            var unclosedShift = await _context.Shifts
                .Where(s => s.BranchId == req.BranchId &&
                            (s.Status == null ||
                             (!s.Status.StartsWith("Closed") &&
                              !s.Status.StartsWith("Close")) ||
                             s.ClosedByUserId == null))
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (unclosedShift != null)
            {
                string unclosedTypeName = GetShiftTypeName(unclosedShift.ShiftType);
                throw new InvalidOperationException($"Không thể mở ca mới ({GetShiftTypeName(req.ShiftType)} ngày {sDate:dd/MM/yyyy})!\n" +
                    $"Ca trước ({unclosedTypeName} ngày {unclosedShift.ShiftDate:dd/MM/yyyy}) chưa được chốt ca.\n" +
                    $"Cần phải chốt ca trước và có người ký tên chịu trách nhiệm trước khi mở ca tiếp theo.");
            }

            // 3. Nếu các ca trước đã chốt -> Tìm ca chốt gần nhất của chi nhánh này để kế thừa số liệu cuối ca -> đầu ca hiện tại
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
                OpenedByUserId = null,
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

            return await MapToDetailDTOAsync(newShift, branchName);
        }

        private static bool IsShiftModified(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            if (status.Equals("NConfirm", StringComparison.OrdinalIgnoreCase)) return false;
            return status.EndsWith("NC", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Changed", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> ConfirmStartAsync(ConfirmStartRequestDTO req)
        {
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId);
            if (shift == null) return false;

            shift.Status = (shift.Status != null && IsShiftModified(shift.Status)) ? "ConfirmStartNC" : "ConfirmStart";
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
            var (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, pos1, pos2, pos3, bank1, bank2) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            string pos1Name = pos1Config?.PosName ?? "Máy POS 1";
            string pos2Name = pos2Config?.PosName ?? "Máy POS 2";
            string pos3Name = pos3Config?.PosName ?? "Máy POS 3";
            string bank1Name = bank1Config?.BankName ?? "Ngân hàng 1";
            string bank2Name = bank2Config?.BankName ?? "Ngân hàng 2";

            if (pos1Config != null && pos1Config.IsActive && pos1 != null && req.Pos1Closing < pos1.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos1Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (pos2Config != null && pos2Config.IsActive && pos2 != null && req.Pos2Closing < pos2.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos2Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (pos3Config != null && pos3Config.IsActive && pos3 != null && req.Pos3Closing < pos3.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos3Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (bank1Config != null && bank1Config.IsActive && bank1 != null && req.Bank1Closing < bank1.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {bank1Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (bank2Config != null && bank2Config.IsActive && bank2 != null && req.Bank2Closing < bank2.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {bank2Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            // 3. Cập nhật thông tin ca (Nếu trước đó đã có NC thì lưu ClosedNC, ngược lại Closed)
            shift.Status = (shift.Status != null && IsShiftModified(shift.Status)) ? "ClosedNC" : "Closed";
            var firstSignerId = verifiedUsers.First().Id;
            shift.ClosedByUserId = firstSignerId;
            shift.OpenedByUserId = firstSignerId; // Mặc định người mở ca là người ký thứ nhất của chốt ca
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

            if (pos3 != null)
            {
                pos3.PosClosing = req.Pos3Closing;
                pos3.PosClosingDay2 = req.Pos3Night;
            }

            if (pos1 != null && pos1.Id == 0) _context.ShiftPosEntries.Add(pos1);
            if (pos2 != null && pos2.Id == 0) _context.ShiftPosEntries.Add(pos2);
            if (pos3 != null && pos3.Id == 0) _context.ShiftPosEntries.Add(pos3);

            if (bank1 != null)
            {
                if (bank1.Id == 0) _context.ShiftBankEntries.Add(bank1);
                bank1.BankClosing = req.Bank1Closing;
                bank1.BankClosingDay2 = req.Bank1Night;
            }

            if (bank2 != null)
            {
                if (bank2.Id == 0) _context.ShiftBankEntries.Add(bank2);
                bank2.BankClosing = req.Bank2Closing;
                bank2.BankClosingDay2 = req.Bank2Night;
            }

            // Cập nhật chi phí (Chỉ lưu khoản chi có số tiền > 0 để thỏa mãn CHK_ShiftExpenses_Amount)
            _context.ShiftExpenses.RemoveRange(shift.ShiftExpenses);
            if (req.Expenses != null)
            {
                foreach (var exp in req.Expenses)
                {
                    if (exp.Amount > 0)
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

            var (_, _, _, _, _, pos1, pos2, pos3, bank1, bank2) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            if (pos1 != null) pos1.PosOpening = req.Pos1Opening;
            if (pos2 != null) pos2.PosOpening = req.Pos2Opening;
            if (pos3 != null) pos3.PosOpening = req.Pos3Opening;

            if (bank1 != null) bank1.BankOpening = req.Bank1Opening;
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
            if (shift.Status != null && IsShiftModified(shift.Status))
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
            if (shift.Status != null && IsShiftModified(shift.Status))
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

            var (_, _, _, _, _, pos1, pos2, pos3, bank1, bank2) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            if (pos1 != null)
            {
                pos1.PosOpening = req.Pos1Opening;
                pos1.PosClosing = req.Pos1Closing;
                pos1.PosClosingDay2 = req.Pos1Night;
            }

            if (pos2 != null)
            {
                pos2.PosOpening = req.Pos2Opening;
                pos2.PosClosing = req.Pos2Closing;
                pos2.PosClosingDay2 = req.Pos2Night;
            }

            if (pos3 != null)
            {
                pos3.PosOpening = req.Pos3Opening;
                pos3.PosClosing = req.Pos3Closing;
                pos3.PosClosingDay2 = req.Pos3Night;
            }

            if (bank1 != null)
            {
                bank1.BankOpening = req.Bank1Opening;
                bank1.BankClosing = req.Bank1Closing;
                bank1.BankClosingDay2 = req.Bank1Night;
            }

            if (bank2 != null)
            {
                bank2.BankOpening = req.Bank2Opening;
                bank2.BankClosing = req.Bank2Closing;
                bank2.BankClosingDay2 = req.Bank2Night;
            }

            // Cập nhật chi phí (Chỉ lưu khoản chi có số tiền > 0 để thỏa mãn CHK_ShiftExpenses_Amount)
            _context.ShiftExpenses.RemoveRange(shift.ShiftExpenses);
            if (req.Expenses != null)
            {
                foreach (var exp in req.Expenses)
                {
                    if (exp.Amount > 0)
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
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<EmployeeShiftStatisticsDTO>> GetEmployeeStatisticsAsync()
        {
            var users = await _context.Users
                .Where(u => u.Role == "Employee")
                .OrderBy(u => u.Id)
                .ToListAsync();
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

        private async Task<(
            PosConfig? pos1Config, PosConfig? pos2Config, PosConfig? pos3Config,
            BranchBank? bank1Config, BranchBank? bank2Config,
            ShiftPosEntry? pos1, ShiftPosEntry? pos2, ShiftPosEntry? pos3,
            ShiftBankEntry? bank1, ShiftBankEntry? bank2
        )> ResolveShiftConfigsAndEntriesAsync(Shift shift)
        {
            var branchPos = await _context.PosConfigs
                .Where(p => p.BranchId == shift.BranchId)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var activePos = branchPos.Where(p => p.IsActive).ToList();
            var pos1Config = activePos.ElementAtOrDefault(0) ?? branchPos.ElementAtOrDefault(0);
            var pos2Config = activePos.ElementAtOrDefault(1) ?? branchPos.Where(p => p != pos1Config).FirstOrDefault();
            var pos3Config = activePos.ElementAtOrDefault(2) ?? branchPos.Where(p => p != pos1Config && p != pos2Config).FirstOrDefault();

            var branchBanks = await _context.BranchBanks
                .Where(b => b.BranchId == shift.BranchId)
                .OrderBy(b => b.SlotIndex)
                .ToListAsync();

            var activeBanks = branchBanks.Where(b => b.IsActive).ToList();
            var bank1Config = activeBanks.ElementAtOrDefault(0) ?? branchBanks.ElementAtOrDefault(0);
            var bank2Config = activeBanks.ElementAtOrDefault(1) ?? branchBanks.Where(b => b != bank1Config).FirstOrDefault();

            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => pos1Config != null && p.PosConfigId == pos1Config.Id);
            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => pos2Config != null && p.PosConfigId == pos2Config.Id);
            var pos3 = shift.ShiftPosEntries.FirstOrDefault(p => pos3Config != null && p.PosConfigId == pos3Config.Id);

            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => (bank1Config != null && b.BranchBankId == bank1Config.Id));
            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => (bank2Config != null && b.BranchBankId == bank2Config.Id));

            if (bank1 == null && bank1Config != null)
            {
                bank1 = new ShiftBankEntry
                {
                    ShiftId = shift.Id,
                    BranchBankId = bank1Config.Id,
                    BankOpening = 0m,
                    BankClosing = 0m
                };
            }

            if (bank2 == null && bank2Config != null)
            {
                bank2 = new ShiftBankEntry
                {
                    ShiftId = shift.Id,
                    BranchBankId = bank2Config.Id,
                    BankOpening = 0m,
                    BankClosing = 0m
                };
            }

            return (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, pos1, pos2, pos3, bank1, bank2);
        }

        private async Task<ShiftHandoverDetailDTO> MapToDetailDTOAsync(Shift s, string branchName)
        {
            string shiftTypeName = s.ShiftType switch
            {
                "MORNING" => "Ca Sáng",
                "AFTERNOON" => "Ca Chiều",
                "EVENING" => "Ca Tối",
                "NIGHT" => "Ca Đêm",
                _ => s.ShiftType
            };

            var (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, pos1, pos2, pos3, bank1, bank2) =
                await ResolveShiftConfigsAndEntriesAsync(s);

            bool isClosed = s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));

            var dto = new ShiftHandoverDetailDTO
            {
                ShiftId = s.Id,
                ShiftCode = GenerateShiftCode(s),
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
                OpenedByUser = s.OpenedByUser?.FullName ?? s.ClosedByUser?.FullName ?? "—",
                ClosedByUser = s.ClosedByUser?.FullName ?? "—",

                Pos1Name = pos1Config?.PosName ?? "Sapo POS",
                Pos1IsActive = pos1Config?.IsActive ?? true,
                Pos1Opening = pos1?.PosOpening ?? 0m,
                Pos1Closing = pos1?.PosClosing,
                Pos1Night = pos1?.PosClosingDay2,

                Pos2Name = pos2Config?.PosName ?? "KiotViet",
                Pos2IsActive = pos2Config?.IsActive ?? false,
                Pos2Opening = pos2?.PosOpening ?? 0m,
                Pos2Closing = pos2?.PosClosing,
                Pos2Night = pos2?.PosClosingDay2,

                Pos3Name = pos3Config?.PosName ?? "",
                Pos3IsActive = pos3Config != null && pos3Config.IsActive,
                Pos3Opening = pos3?.PosOpening ?? 0m,
                Pos3Closing = pos3?.PosClosing,
                Pos3Night = pos3?.PosClosingDay2,

                Bank1Name = bank1Config?.BankName ?? "TingTing",
                Bank1IsActive = bank1Config?.IsActive ?? true,
                Bank1Opening = bank1?.BankOpening ?? 0m,
                Bank1Closing = bank1?.BankClosing,
                Bank1Night = bank1?.BankClosingDay2,

                Bank2Name = bank2Config?.BankName ?? "Zalo Pay",
                Bank2IsActive = bank2Config?.IsActive ?? false,
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
            await AutoCloseExpiredShiftsAsync();

            var shift = await _context.Shifts
                .Include(s => s.Branch)
                .Include(s => s.ShiftPosEntries).ThenInclude(p => p.PosConfig)
                .Include(s => s.ShiftBankEntries).ThenInclude(b => b.BranchBank)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.OpenedByUser)
                .Include(s => s.ClosedByUser)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return null;

            string branchName = shift.Branch?.Name ?? $"Cơ sở {shift.BranchId}";
            return await MapToDetailDTOAsync(shift, branchName);
        }

        public async Task<List<AdminShiftSummaryDTO>> GetAllShiftsAsync()
        {
            await AutoCloseExpiredShiftsAsync();

            var shifts = await _context.Shifts
                .Include(s => s.Branch)
                .Include(s => s.ClosedByUser)
                .Include(s => s.OpenedByUser)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.ShiftBankEntries)
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
                .Include(s => s.OpenedByUser)
                .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(s => s.ShiftBankEntries)
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
                    .ThenInclude(s => s.Branch)
                .Include(e => e.Shift)
                    .ThenInclude(s => s.ShiftEmployees).ThenInclude(se => se.User)
                .Include(e => e.Shift)
                    .ThenInclude(s => s.ClosedByUser)
                .Include(e => e.Shift)
                    .ThenInclude(s => s.OpenedByUser)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return expenses.Select(e =>
            {
                var empList = e.Shift?.ShiftEmployees?
                    .Select(se => se.User?.FullName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Select(name => name!)
                    .Distinct()
                    .ToList() ?? new List<string>();

                if (!empList.Any())
                {
                    if (!string.IsNullOrEmpty(e.Shift?.ClosedByUser?.FullName))
                        empList.Add(e.Shift.ClosedByUser.FullName);
                    if (!string.IsNullOrEmpty(e.Shift?.OpenedByUser?.FullName) && !empList.Contains(e.Shift.OpenedByUser.FullName))
                        empList.Add(e.Shift.OpenedByUser.FullName);
                }

                if (!empList.Any() && !string.IsNullOrEmpty(e.CreatedByUser?.FullName))
                {
                    empList.Add(e.CreatedByUser.FullName);
                }

                string empNames = string.Join(", ", empList);

                return new AdminExpenseDTO
                {
                    Id = e.Id,
                    ShiftCode = (e.Shift != null) ? GenerateShiftCode(e.Shift) : $"exp_{e.ShiftId}",
                    ShiftId = e.ShiftId,
                    BranchId = e.Shift?.BranchId ?? 0,
                    BranchName = e.Shift?.Branch?.Name ?? "",
                    CreatedAt = e.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    CreatedByUser = e.CreatedByUser?.FullName ?? "Nhân viên",
                    EmployeeNames = empNames,
                    Amount = e.Amount,
                    AmountDisplay = $"{e.Amount:N0} đ",
                    Description = e.Description,
                    Note = e.Note ?? ""
                };
            }).ToList();
        }

        public static string GenerateShiftCode(Shift s)
        {
            string username = s.ClosedByUser?.Username ?? s.OpenedByUser?.Username ?? "user";

            DateTime? rawTime = s.ClosedAt ?? s.OpenedAt ?? s.CreatedAt;
            DateTime localTime;
            if (rawTime.HasValue)
            {
                var t = rawTime.Value;
                localTime = t.Kind switch
                {
                    DateTimeKind.Utc => t.ToLocalTime(),
                    DateTimeKind.Local => t,
                    _ => DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime()
                };
            }
            else
            {
                localTime = s.ShiftDate.ToDateTime(TimeOnly.MinValue);
            }

            return $"{username.ToLower().Trim()}_{localTime:ddMMyyyy_HH\\hmm}";
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

            bool isClosed = s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));

            decimal diff = s.CashDifference ?? 0m;
            string diffDisplay = diff > 0 ? $"+{diff:N0} đ" : (diff < 0 ? $"{diff:N0} đ" : "0 đ");

            // Chỉ tính chênh lệch tiền mặt cho các ca đã chốt (tránh ca chưa chốt bị 0 - CashOpening = âm tiền két)
            decimal cashDiffClosing = (isClosed && s.CashClosing.HasValue) ? (s.CashClosing.Value - s.CashOpening) : 0m;

            decimal bankRev = 0m;
            if (isClosed && s.ShiftBankEntries != null)
            {
                bool isNight = s.ShiftType != null && (s.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase) || s.ShiftType.Contains("Đêm", StringComparison.OrdinalIgnoreCase) || s.ShiftType.Contains("Dem", StringComparison.OrdinalIgnoreCase));
                foreach (var b in s.ShiftBankEntries)
                {
                    decimal bDiff = (b.BankClosing ?? 0m) - b.BankOpening;
                    if (bDiff > 0) bankRev += bDiff;
                    if (isNight && (b.BankClosingDay2 ?? 0m) > 0)
                    {
                        bankRev += (b.BankClosingDay2 ?? 0m);
                    }
                }
            }

            var empList = s.ShiftEmployees?.Select(se => se.User?.FullName).Where(name => !string.IsNullOrEmpty(name)).Select(name => name!).ToList() ?? new List<string>();
            string empNames = empList.Any() ? string.Join(", ", empList) : "";
            var empIdList = s.ShiftEmployees?.Select(se => se.UserId).ToList() ?? new List<int>();

            return new AdminShiftSummaryDTO
            {
                Id = s.Id,
                ShiftCode = GenerateShiftCode(s),
                ShiftDate = s.ShiftDate.ToString("dd/MM/yyyy"),
                ShiftType = typeName,
                BranchName = s.Branch?.Name ?? $"Cơ sở {s.BranchId}",
                ClosedByUser = s.ClosedByUser?.FullName ?? "—",
                OpenedByUser = s.OpenedByUser?.FullName ?? s.ClosedByUser?.FullName ?? "—",
                EmployeeNames = empNames,
                ClosedByUserId = s.ClosedByUserId,
                EmployeeUserIds = empIdList,
                CashDifference = s.CashDifference,
                CashDifferenceDisplay = diffDisplay,
                Note = s.Note ?? "",
                Status = s.Status ?? "",
                StatusDisplay = statusDisplay,
                CashDiffClosing = cashDiffClosing,
                BankRevenue = bankRev
            };
        }
    }
}

