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

        private static DateTime GetVietnamNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(7);
                }
            }
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

        private DateTime GetShiftEndTime(DateTime shiftDate, string shiftType)
        {
            return shiftType?.ToUpper().Trim() switch
            {
                "MORNING" => shiftDate.Date.AddHours(12),
                "AFTERNOON" => shiftDate.Date.AddHours(18),
                "EVENING" => shiftDate.Date.AddHours(23),
                "NIGHT" => shiftDate.Date.AddDays(1).AddHours(2).AddMinutes(30),
                _ => shiftDate.Date.AddDays(1)
            };
        }

        public async Task<int> AutoCloseExpiredShiftsAsync()
        {
            // Tính năng tự động chốt ca đã được vô hiệu hóa theo yêu cầu.
            // Bắt buộc tất cả các ca phải được chốt thủ công.
            await Task.CompletedTask;
            return 0;
        }

        public async Task<ShiftHandoverDetailDTO> GetOrCreateShiftAsync(InitShiftRequestDTO req)
        {
            DateOnly sDate = DateOnly.FromDateTime(req.ShiftDate);
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == req.BranchId);
            if (branch == null || !branch.IsActive)
            {
                throw new InvalidOperationException($"Cơ sở '{branch?.Name ?? req.BranchId.ToString()}' hiện đang tạm khóa hoặc ngừng hoạt động. Không thể mở ca làm việc!");
            }
            string branchName = branch.Name;

            DateTime now = GetVietnamNow();
            DateTime today = now.Date;
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
                    int reqOrder = GetShiftOrder(req.ShiftType);
                    var priorCandidates = await _context.Shifts
                        .Where(s => s.BranchId == req.BranchId &&
                                    s.Id != existingShift.Id &&
                                    s.ShiftDate <= sDate &&
                                    (s.Status == null ||
                                     (!s.Status.StartsWith("Closed") &&
                                      !s.Status.StartsWith("Close"))))
                        .ToListAsync();

                    var unclosedPriorShift = priorCandidates
                        .Where(s => s.ShiftDate < sDate || (s.ShiftDate == sDate && GetShiftOrder(s.ShiftType) < reqOrder))
                        .OrderByDescending(s => s.ShiftDate)
                        .ThenByDescending(s => s.Id)
                        .FirstOrDefault();

                    if (unclosedPriorShift != null)
                    {
                        throw new InvalidOperationException("Hãy chốt ca trước đã .");
                    }

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

            // C. Kiểm tra ca trống trong quá khứ không có ai làm
            bool isYesterdayNightRunning = req.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase)
                && req.ShiftDate.Date == today.AddDays(-1)
                && now.TimeOfDay < new TimeSpan(2, 30, 0);

            bool isPastDay = req.ShiftDate.Date < today && !isYesterdayNightRunning;
            bool isPastShiftToday = false;
            if (req.ShiftDate.Date == today)
            {
                DateTime shiftEndTime = GetShiftEndTime(req.ShiftDate, req.ShiftType);
                if (now > shiftEndTime)
                {
                    isPastShiftToday = true;
                }
                else
                {
                    int reqOrder = GetShiftOrder(req.ShiftType);
                    bool hasLaterShiftToday = await _context.Shifts.AnyAsync(s => s.BranchId == req.BranchId && s.ShiftDate == sDate && (
                        (s.ShiftType == "AFTERNOON" && reqOrder < 2) ||
                        (s.ShiftType == "EVENING" && reqOrder < 3) ||
                        (s.ShiftType == "NIGHT" && reqOrder < 4)
                    ));
                    if (hasLaterShiftToday)
                    {
                        isPastShiftToday = true;
                    }
                }
            }

            if (isPastDay || isPastShiftToday)
            {
                throw new InvalidOperationException($"Ca này trống!\n" +
                    $"Ca {GetShiftTypeName(req.ShiftType)} ngày {sDate:dd/MM/yyyy} không có nhân viên trực ca (cửa hàng không hoạt động ca này).");
            }

            // D. Kiểm tra ca trước đã chốt chưa: Nếu ca trước chưa chốt thì chặn lại và thông báo
            int currentReqOrder = GetShiftOrder(req.ShiftType);
            var candidateShifts = await _context.Shifts
                .Where(s => s.BranchId == req.BranchId &&
                            s.ShiftDate <= sDate &&
                            (s.Status == null ||
                             (!s.Status.StartsWith("Closed") &&
                              !s.Status.StartsWith("Close"))))
                .ToListAsync();

            var unclosedShift = candidateShifts
                .Where(s => s.ShiftDate < sDate || (s.ShiftDate == sDate && GetShiftOrder(s.ShiftType) < currentReqOrder))
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

            if (unclosedShift != null)
            {
                throw new InvalidOperationException("Hãy chốt ca trước đã .");
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

            int reqOrder = GetShiftOrder(shift.ShiftType);
            var candidates = await _context.Shifts
                .Where(s => s.BranchId == shift.BranchId &&
                            s.Id != shift.Id &&
                            s.ShiftDate <= shift.ShiftDate &&
                            (s.Status == null || (!s.Status.StartsWith("Closed") && !s.Status.StartsWith("Close"))))
                .ToListAsync();

            bool hasUnclosedPriorShift = candidates.Any(s =>
                s.ShiftDate < shift.ShiftDate || (s.ShiftDate == shift.ShiftDate && GetShiftOrder(s.ShiftType) < reqOrder));

            if (hasUnclosedPriorShift)
            {
                throw new InvalidOperationException("Hãy chốt ca trước đã .");
            }

            shift.Status = (shift.Status != null && IsShiftModified(shift.Status)) ? "ConfirmStartNC" : "ConfirmStart";
            shift.OpenedByUserId = req.UserId;
            shift.OpenedAt = DateTime.UtcNow;
            shift.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateShiftChannelsAsync(int shiftId, ShiftChannelSelection channels)
        {
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);
            if (shift == null) return false;

            var list = new List<string>();
            if (channels.Pos1Active) list.Add("POS1");
            if (channels.Pos2Active) list.Add("POS2");
            if (channels.Pos3Active) list.Add("POS3");
            if (channels.Bank1Active) list.Add("BANK1");
            if (channels.Bank2Active) list.Add("BANK2");
            if (channels.Bank3Active) list.Add("BANK3");

            shift.ActiveChannels = string.Join(",", list);
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

            if (req.Signatures.Count > 2)
            {
                return new CloseShiftResponseDTO { Success = false, Message = "Mỗi ca làm việc chỉ cho phép tối đa 2 nhân viên trực cùng lúc theo quy định cửa hàng!" };
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
            var (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, bank3Config, pos1, pos2, pos3, bank1, bank2, bank3) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            string pos1Name = pos1Config?.PosName ?? "Máy POS 1";
            string pos2Name = pos2Config?.PosName ?? "Máy POS 2";
            string pos3Name = pos3Config?.PosName ?? "Máy POS 3";
            string bank1Name = bank1Config?.BankName ?? "Ngân hàng 1";
            string bank2Name = bank2Config?.BankName ?? "Ngân hàng 2";
            string bank3Name = bank3Config?.BankName ?? "Ngân hàng 3";

            bool hasActiveChannels = !string.IsNullOrWhiteSpace(shift.ActiveChannels);
            bool isPos1Active = (pos1Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("POS1", StringComparison.OrdinalIgnoreCase));
            bool isPos2Active = (pos2Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("POS2", StringComparison.OrdinalIgnoreCase));
            bool isPos3Active = (pos3Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("POS3", StringComparison.OrdinalIgnoreCase));
            bool isBank1Active = (bank1Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("BANK1", StringComparison.OrdinalIgnoreCase));
            bool isBank2Active = (bank2Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("BANK2", StringComparison.OrdinalIgnoreCase));
            bool isBank3Active = (bank3Config?.IsActive ?? false) && (!hasActiveChannels || shift.ActiveChannels!.Contains("BANK3", StringComparison.OrdinalIgnoreCase));

            if (isPos1Active && pos1 != null && req.Pos1Closing < pos1.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos1Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (isPos2Active && pos2 != null && req.Pos2Closing < pos2.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos2Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (isPos3Active && pos3 != null && req.Pos3Closing < pos3.PosOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {pos3Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (isBank1Active && bank1 != null && req.Bank1Closing < bank1.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {bank1Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (isBank2Active && bank2 != null && req.Bank2Closing < bank2.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {bank2Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            if (isBank3Active && bank3 != null && req.Bank3Closing < bank3.BankOpening)
            {
                return new CloseShiftResponseDTO { Success = false, Message = $"Số liệu cuối ca của {bank3Name} phải lớn hơn hoặc bằng đầu ca!" };
            }

            // 3. Cập nhật thông tin ca (Nếu trước đó đã có NC thì lưu ClosedNC, ngược lại Closed)
            shift.Status = (shift.Status != null && IsShiftModified(shift.Status)) ? "ClosedNC" : "Closed";
            var firstSignerId = verifiedUsers.First().Id;
            shift.ClosedByUserId = firstSignerId;
            if (!shift.OpenedByUserId.HasValue)
            {
                shift.OpenedByUserId = firstSignerId;
            }
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

            if (bank3 != null)
            {
                if (bank3.Id == 0) _context.ShiftBankEntries.Add(bank3);
                bank3.BankClosing = req.Bank3Closing;
                bank3.BankClosingDay2 = req.Bank3Night;
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

            // 5. Lưu danh sách kênh bán hàng thực tế có dữ liệu nếu ca chưa lưu ActiveChannels
            if (string.IsNullOrWhiteSpace(shift.ActiveChannels))
            {
                var usedChans = new List<string>();
                if (pos1Config != null && pos1Config.IsActive && ((pos1?.PosOpening ?? 0m) != 0 || req.Pos1Closing != 0 || req.Pos1Night != 0)) usedChans.Add("POS1");
                if (pos2Config != null && pos2Config.IsActive && ((pos2?.PosOpening ?? 0m) != 0 || req.Pos2Closing != 0 || req.Pos2Night != 0)) usedChans.Add("POS2");
                if (pos3Config != null && pos3Config.IsActive && ((pos3?.PosOpening ?? 0m) != 0 || req.Pos3Closing != 0 || req.Pos3Night != 0)) usedChans.Add("POS3");
                if (bank1Config != null && bank1Config.IsActive && ((bank1?.BankOpening ?? 0m) != 0 || req.Bank1Closing != 0 || req.Bank1Night != 0)) usedChans.Add("BANK1");
                if (bank2Config != null && bank2Config.IsActive && ((bank2?.BankOpening ?? 0m) != 0 || req.Bank2Closing != 0 || req.Bank2Night != 0)) usedChans.Add("BANK2");
                if (bank3Config != null && bank3Config.IsActive && ((bank3?.BankOpening ?? 0m) != 0 || req.Bank3Closing != 0 || req.Bank3Night != 0)) usedChans.Add("BANK3");
                shift.ActiveChannels = string.Join(",", usedChans);
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

            int reqOrder = GetShiftOrder(shift.ShiftType);
            var candidates = await _context.Shifts
                .Where(s => s.BranchId == shift.BranchId &&
                            s.Id != shift.Id &&
                            s.ShiftDate <= shift.ShiftDate &&
                            (s.Status == null || (!s.Status.StartsWith("Closed") && !s.Status.StartsWith("Close"))))
                .ToListAsync();

            bool hasUnclosedPriorShift = candidates.Any(s =>
                s.ShiftDate < shift.ShiftDate || (s.ShiftDate == shift.ShiftDate && GetShiftOrder(s.ShiftType) < reqOrder));

            if (hasUnclosedPriorShift)
            {
                throw new InvalidOperationException("Hãy chốt ca trước đã .");
            }

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

            var (_, _, _, _, _, _, pos1, pos2, pos3, bank1, bank2, bank3) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            if (pos1 != null)
            {
                pos1.PosOpening = req.Pos1Opening;
                if (pos1.Id == 0) _context.ShiftPosEntries.Add(pos1);
            }
            if (pos2 != null)
            {
                pos2.PosOpening = req.Pos2Opening;
                if (pos2.Id == 0) _context.ShiftPosEntries.Add(pos2);
            }
            if (pos3 != null)
            {
                pos3.PosOpening = req.Pos3Opening;
                if (pos3.Id == 0) _context.ShiftPosEntries.Add(pos3);
            }

            if (bank1 != null)
            {
                bank1.BankOpening = req.Bank1Opening;
                if (bank1.Id == 0) _context.ShiftBankEntries.Add(bank1);
            }
            if (bank2 != null)
            {
                bank2.BankOpening = req.Bank2Opening;
                if (bank2.Id == 0) _context.ShiftBankEntries.Add(bank2);
            }
            if (bank3 != null)
            {
                bank3.BankOpening = req.Bank3Opening;
                if (bank3.Id == 0) _context.ShiftBankEntries.Add(bank3);
            }

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

            // 1. Kiểm tra: Chỉ được thay đổi duy nhất một lần sau khi chốt ca
            if (HasClosingBeenEdited(shift.Note))
            {
                return new VerifyShiftOwnerResponseDTO
                {
                    IsAuthorized = false,
                    Message = "Ca làm việc này đã được chỉnh sửa sau khi chốt ca một lần, không thể điều chỉnh thêm nữa!"
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

        private static bool HasClosingBeenEdited(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return false;
            return note.IndexOf("Cuối ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   note.IndexOf("đã chỉnh sửa", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<bool> UpdateClosedShiftAsync(UpdateClosedShiftRequestDTO req)
        {
            var shift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null) return false;

            // Kiểm tra: Chỉ được thay đổi duy nhất một lần sau khi chốt ca
            if (HasClosingBeenEdited(shift.Note))
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

            // Cập nhật ghi chú kèm audit log chuẩn hóa
            string closingDetails = $"Cuối ca: {req.ChangeLog}";
            string noteDetail = !string.IsNullOrWhiteSpace(req.UserNote) ? $"Ghi chú: {req.UserNote.Trim()}" : "";

            // Trích xuất log đầu ca cũ nếu có (bảo tồn nguyên vẹn)
            string openingPart = "";
            if (!string.IsNullOrWhiteSpace(shift.Note))
            {
                string raw = shift.Note.Trim();
                int idxCuoiCa = raw.IndexOf("Cuối ca:", StringComparison.OrdinalIgnoreCase);
                if (idxCuoiCa >= 0)
                {
                    raw = raw.Substring(0, idxCuoiCa).Trim().TrimEnd('|', ' ');
                }

                int idxUserNote = raw.IndexOf("Ghi chú:", StringComparison.OrdinalIgnoreCase);
                if (idxUserNote < 0) idxUserNote = raw.IndexOf("Ghi chú nhân viên:", StringComparison.OrdinalIgnoreCase);

                if (idxUserNote > 0)
                {
                    openingPart = raw.Substring(0, idxUserNote).Trim().TrimEnd('|', ' ');
                }
                else if (raw.IndexOf("Đầu ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         raw.IndexOf("Nhân viên đã thay đổi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    openingPart = raw;
                }
            }

            string combinedNote = "";
            if (!string.IsNullOrEmpty(openingPart))
            {
                combinedNote = $"{openingPart} | {closingDetails}";
            }
            else
            {
                combinedNote = closingDetails;
            }

            if (!string.IsNullOrEmpty(noteDetail))
            {
                combinedNote += $" | {noteDetail}";
            }

            shift.Note = combinedNote;

            var (_, _, _, _, _, _, pos1, pos2, pos3, bank1, bank2, bank3) =
                await ResolveShiftConfigsAndEntriesAsync(shift);

            if (pos1 != null)
            {
                pos1.PosOpening = req.Pos1Opening;
                pos1.PosClosing = req.Pos1Closing;
                pos1.PosClosingDay2 = req.Pos1Night;
                if (pos1.Id == 0) _context.ShiftPosEntries.Add(pos1);
            }

            if (pos2 != null)
            {
                pos2.PosOpening = req.Pos2Opening;
                pos2.PosClosing = req.Pos2Closing;
                pos2.PosClosingDay2 = req.Pos2Night;
                if (pos2.Id == 0) _context.ShiftPosEntries.Add(pos2);
            }

            if (pos3 != null)
            {
                pos3.PosOpening = req.Pos3Opening;
                pos3.PosClosing = req.Pos3Closing;
                pos3.PosClosingDay2 = req.Pos3Night;
                if (pos3.Id == 0) _context.ShiftPosEntries.Add(pos3);
            }

            if (bank1 != null)
            {
                bank1.BankOpening = req.Bank1Opening;
                bank1.BankClosing = req.Bank1Closing;
                bank1.BankClosingDay2 = req.Bank1Night;
                if (bank1.Id == 0) _context.ShiftBankEntries.Add(bank1);
            }

            if (bank2 != null)
            {
                bank2.BankOpening = req.Bank2Opening;
                bank2.BankClosing = req.Bank2Closing;
                bank2.BankClosingDay2 = req.Bank2Night;
                if (bank2.Id == 0) _context.ShiftBankEntries.Add(bank2);
            }

            if (bank3 != null)
            {
                bank3.BankOpening = req.Bank3Opening;
                bank3.BankClosing = req.Bank3Closing;
                bank3.BankClosingDay2 = req.Bank3Night;
                if (bank3.Id == 0) _context.ShiftBankEntries.Add(bank3);
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
                            CreatedByUserId = req.EditorUserId > 0 ? req.EditorUserId : (shift.ClosedByUserId ?? shift.OpenedByUserId ?? 1),
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
            BranchBank? bank1Config, BranchBank? bank2Config, BranchBank? bank3Config,
            ShiftPosEntry? pos1, ShiftPosEntry? pos2, ShiftPosEntry? pos3,
            ShiftBankEntry? bank1, ShiftBankEntry? bank2, ShiftBankEntry? bank3
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
            var bank3Config = activeBanks.ElementAtOrDefault(2) ?? branchBanks.Where(b => b != bank1Config && b != bank2Config).FirstOrDefault();

            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => pos1Config != null && p.PosConfigId == pos1Config.Id);
            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => pos2Config != null && p.PosConfigId == pos2Config.Id);
            var pos3 = shift.ShiftPosEntries.FirstOrDefault(p => pos3Config != null && p.PosConfigId == pos3Config.Id);

            if (pos1 == null && pos1Config != null)
            {
                pos1 = new ShiftPosEntry
                {
                    ShiftId = shift.Id,
                    PosConfigId = pos1Config.Id,
                    PosOpening = 0m,
                    PosClosing = 0m
                };
            }

            if (pos2 == null && pos2Config != null)
            {
                pos2 = new ShiftPosEntry
                {
                    ShiftId = shift.Id,
                    PosConfigId = pos2Config.Id,
                    PosOpening = 0m,
                    PosClosing = 0m
                };
            }

            if (pos3 == null && pos3Config != null)
            {
                pos3 = new ShiftPosEntry
                {
                    ShiftId = shift.Id,
                    PosConfigId = pos3Config.Id,
                    PosOpening = 0m,
                    PosClosing = 0m
                };
            }

            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => (bank1Config != null && b.BranchBankId == bank1Config.Id));
            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => (bank2Config != null && b.BranchBankId == bank2Config.Id));
            var bank3 = shift.ShiftBankEntries.FirstOrDefault(b => (bank3Config != null && b.BranchBankId == bank3Config.Id));

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

            if (bank3 == null && bank3Config != null)
            {
                bank3 = new ShiftBankEntry
                {
                    ShiftId = shift.Id,
                    BranchBankId = bank3Config.Id,
                    BankOpening = 0m,
                    BankClosing = 0m
                };
            }

            return (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, bank3Config, pos1, pos2, pos3, bank1, bank2, bank3);
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

            var (pos1Config, pos2Config, pos3Config, bank1Config, bank2Config, bank3Config, pos1, pos2, pos3, bank1, bank2, bank3) =
                await ResolveShiftConfigsAndEntriesAsync(s);

            bool isClosed = s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));

            bool hasActiveChannels = !string.IsNullOrWhiteSpace(s.ActiveChannels);

            bool p1Used = hasActiveChannels
                ? (pos1Config?.IsActive ?? true) && s.ActiveChannels!.Contains("POS1", StringComparison.OrdinalIgnoreCase)
                : ((pos1Config?.IsActive ?? true) && ((pos1?.PosOpening ?? 0m) != 0m || (pos1?.PosClosing ?? 0m) != 0m || (pos1?.PosClosingDay2 ?? 0m) != 0m));

            bool p2Used = hasActiveChannels
                ? (pos2Config?.IsActive ?? false) && s.ActiveChannels!.Contains("POS2", StringComparison.OrdinalIgnoreCase)
                : ((pos2Config?.IsActive ?? false) && ((pos2?.PosOpening ?? 0m) != 0m || (pos2?.PosClosing ?? 0m) != 0m || (pos2?.PosClosingDay2 ?? 0m) != 0m));

            bool p3Used = hasActiveChannels
                ? (pos3Config != null && pos3Config.IsActive) && s.ActiveChannels!.Contains("POS3", StringComparison.OrdinalIgnoreCase)
                : ((pos3Config != null && pos3Config.IsActive) && ((pos3?.PosOpening ?? 0m) != 0m || (pos3?.PosClosing ?? 0m) != 0m || (pos3?.PosClosingDay2 ?? 0m) != 0m));

            bool b1Used = hasActiveChannels
                ? (bank1Config?.IsActive ?? true) && s.ActiveChannels!.Contains("BANK1", StringComparison.OrdinalIgnoreCase)
                : ((bank1Config?.IsActive ?? true) && ((bank1?.BankOpening ?? 0m) != 0m || (bank1?.BankClosing ?? 0m) != 0m || (bank1?.BankClosingDay2 ?? 0m) != 0m));

            bool b2Used = hasActiveChannels
                ? (bank2Config?.IsActive ?? false) && s.ActiveChannels!.Contains("BANK2", StringComparison.OrdinalIgnoreCase)
                : ((bank2Config?.IsActive ?? false) && ((bank2?.BankOpening ?? 0m) != 0m || (bank2?.BankClosing ?? 0m) != 0m || (bank2?.BankClosingDay2 ?? 0m) != 0m));

            bool b3Used = hasActiveChannels
                ? (bank3Config?.IsActive ?? false) && s.ActiveChannels!.Contains("BANK3", StringComparison.OrdinalIgnoreCase)
                : ((bank3Config?.IsActive ?? false) && ((bank3?.BankOpening ?? 0m) != 0m || (bank3?.BankClosing ?? 0m) != 0m || (bank3?.BankClosingDay2 ?? 0m) != 0m));

            if (!hasActiveChannels && !isClosed)
            {
                p1Used = pos1Config?.IsActive ?? true;
                p2Used = pos2Config?.IsActive ?? false;
                p3Used = pos3Config != null && pos3Config.IsActive;
                b1Used = bank1Config?.IsActive ?? true;
                b2Used = bank2Config?.IsActive ?? false;
                b3Used = bank3Config?.IsActive ?? false;
            }

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
                Pos1ImageUrl = pos1Config?.ImageUrl,
                Pos1IsActive = p1Used,
                Pos1Opening = pos1?.PosOpening ?? 0m,
                Pos1Closing = pos1?.PosClosing,
                Pos1Night = pos1?.PosClosingDay2,

                Pos2Name = pos2Config?.PosName ?? "KiotViet",
                Pos2ImageUrl = pos2Config?.ImageUrl,
                Pos2IsActive = p2Used,
                Pos2Opening = pos2?.PosOpening ?? 0m,
                Pos2Closing = pos2?.PosClosing,
                Pos2Night = pos2?.PosClosingDay2,

                Pos3Name = pos3Config?.PosName ?? "",
                Pos3ImageUrl = pos3Config?.ImageUrl,
                Pos3IsActive = p3Used,
                Pos3Opening = pos3?.PosOpening ?? 0m,
                Pos3Closing = pos3?.PosClosing,
                Pos3Night = pos3?.PosClosingDay2,

                Bank1Name = bank1Config?.BankName ?? "TingTing",
                Bank1ImageUrl = bank1Config?.ImageUrl,
                Bank1IsActive = b1Used,
                Bank1Opening = bank1?.BankOpening ?? 0m,
                Bank1Closing = bank1?.BankClosing,
                Bank1Night = bank1?.BankClosingDay2,

                Bank2Name = bank2Config?.BankName ?? "Zalo Pay",
                Bank2ImageUrl = bank2Config?.ImageUrl,
                Bank2IsActive = b2Used,
                Bank2Opening = bank2?.BankOpening ?? 0m,
                Bank2Closing = bank2?.BankClosing,
                Bank2Night = bank2?.BankClosingDay2,

                Bank3Name = bank3Config?.BankName ?? "Ngân hàng 3",
                Bank3ImageUrl = bank3Config?.ImageUrl,
                Bank3IsActive = b3Used,
                Bank3Opening = bank3?.BankOpening ?? 0m,
                Bank3Closing = bank3?.BankClosing,
                Bank3Night = bank3?.BankClosingDay2,

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

            bool isAutoClosed = s.ClosedByUserId == null && (s.Note?.Contains("tự động chốt") ?? false);

            string statusDisplay = s.Status switch
            {
                "Closed" => isAutoClosed ? "Tự động chốt" : "Đã chốt",
                "ClosedNC" => "⚠️ Đã chốt (Có sửa - NC)",
                "Close" => isAutoClosed ? "Tự động chốt" : "Đã chốt",
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
                ClosedByUser = s.ClosedByUser?.FullName ?? (isAutoClosed ? "— (Tự chốt)" : "—"),
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

