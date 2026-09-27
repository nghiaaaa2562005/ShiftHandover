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
                .FirstOrDefaultAsync(s => s.BranchId == req.BranchId && s.ShiftDate == sDate && s.ShiftType == req.ShiftType);

            if (existingShift != null)
            {
                return MapToDetailDTO(existingShift, branchName);
            }

            // 2. Nếu chưa có -> Tìm ca chốt gần nhất của chi nhánh này để kế thừa số liệu cuối ca -> đầu ca hiện tại
            var prevShift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Where(s => s.BranchId == req.BranchId && (s.Status == "Closed" || s.Status == "CLOSED"))
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            decimal cashOpening = prevShift?.CashClosing ?? (branch?.DefaultCashOpening ?? 2000000m);

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
                decimal posOpening = prevPos?.PosClosing ?? (pos.DisplayOrder == 1 ? 2719000m : 0m);

                newShift.ShiftPosEntries.Add(new ShiftPosEntry
                {
                    ShiftId = newShift.Id,
                    PosConfigId = pos.Id,
                    PosOpening = posOpening,
                    PosClosing = 0
                });
            }

            // 4. Khởi tạo danh sách Chuyển khoản ngân hàng cho ca
            var branchBanks = await _context.BranchBanks.Where(b => b.BranchId == req.BranchId && b.IsActive).OrderBy(b => b.SlotIndex).ToListAsync();
            foreach (var bank in branchBanks)
            {
                var prevBank = prevShift?.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == bank.Id);
                decimal bankOpening = prevBank?.BankClosing ?? (bank.SlotIndex == 1 ? 2537000m : 0m);

                newShift.ShiftBankEntries.Add(new ShiftBankEntry
                {
                    ShiftId = newShift.Id,
                    BranchBankId = bank.Id,
                    BankOpening = bankOpening,
                    BankClosing = 0
                });
            }

            await _context.SaveChangesAsync();

            return MapToDetailDTO(newShift, branchName);
        }

        public async Task<bool> ConfirmStartAsync(ConfirmStartRequestDTO req)
        {
            var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == req.ShiftId);
            if (shift == null) return false;

            shift.Status = "ConfirmStart";
            shift.OpenedByUserId = req.UserId;
            shift.OpenedAt = DateTime.UtcNow;
            shift.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CloseShiftAsync(CloseShiftRequestDTO req)
        {
            var shift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .FirstOrDefaultAsync(s => s.Id == req.ShiftId);

            if (shift == null) return false;

            shift.Status = "Closed";
            shift.ClosedByUserId = req.ClosedByUserId;
            shift.ClosedAt = DateTime.UtcNow;
            shift.CashClosing = req.CashClosing;
            shift.CashDifference = req.CashDifference;
            shift.Note = req.Note;
            shift.UpdatedAt = DateTime.UtcNow;

            // Cập nhật POS
            var pos1 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 1);
            if (pos1 != null) { pos1.PosClosing = req.Pos1Closing; pos1.PosClosingDay2 = req.Pos1Night; }

            var pos2 = shift.ShiftPosEntries.FirstOrDefault(p => p.PosConfigId == 2);
            if (pos2 != null) { pos2.PosClosing = req.Pos2Closing; pos2.PosClosingDay2 = req.Pos2Night; }

            // Cập nhật Bank
            var bank1 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 1);
            if (bank1 != null) { bank1.BankClosing = req.Bank1Closing; bank1.BankClosingDay2 = req.Bank1Night; }

            var bank2 = shift.ShiftBankEntries.FirstOrDefault(b => b.BranchBankId == 2);
            if (bank2 != null) { bank2.BankClosing = req.Bank2Closing; bank2.BankClosingDay2 = req.Bank2Night; }

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
                        CreatedByUserId = req.ClosedByUserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
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
            shift.Status = "ConfirmStart"; // Sau khi xác nhận thay đổi, tiếp tục ca bình thường
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

            return new ShiftHandoverDetailDTO
            {
                ShiftId = s.Id,
                BranchId = s.BranchId,
                BranchName = branchName,
                ShiftDate = s.ShiftDate.ToDateTime(TimeOnly.MinValue),
                ShiftType = s.ShiftType,
                ShiftTypeName = shiftTypeName,
                Status = s.Status,
                IsReadOnly = s.Status == "Closed" || s.Status == "CLOSED",
                CashOpening = s.CashOpening,
                CashClosing = s.CashClosing,
                CashDifference = s.CashDifference,
                Note = s.Note,

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
                }).ToList()
            };
        }
    }
}

