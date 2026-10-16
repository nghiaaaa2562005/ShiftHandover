using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Server.Repository;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly ShiftHandoverDbContext _context;
        private readonly IShiftRepository _shiftRepository;

        public SyncController(ShiftHandoverDbContext context, IShiftRepository shiftRepository)
        {
            _context = context;
            _shiftRepository = shiftRepository;
        }

        /// <summary>
        /// Endpoint kiểm tra kết nối nhanh (Ping)
        /// </summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "online", serverTime = DateTime.UtcNow });
        }

        /// <summary>
        /// Cấp toàn bộ Master Data cho máy con lưu vào SQLite cục bộ
        /// </summary>
        [HttpGet("bootstrap")]
        public async Task<ActionResult<MasterDataBootstrapDTO>> GetBootstrapData([FromQuery] int branchId = 0)
        {
            var result = new MasterDataBootstrapDTO();

            // 1. Lấy thông tin cơ sở
            Branch? branch = null;
            if (branchId > 0)
            {
                branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive);
            }
            if (branch == null)
            {
                branch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive);
            }

            if (branch != null)
            {
                result.Branch = new BranchDTO
                {
                    Id = branch.Id,
                    Name = branch.Name,
                    DefaultCashOpening = branch.DefaultCashOpening,
                    IsActive = branch.IsActive
                };

                // Lấy Banks của cơ sở
                var banks = await _context.BranchBanks
                    .Where(b => b.BranchId == branch.Id && b.IsActive)
                    .OrderBy(b => b.SlotIndex)
                    .ToListAsync();

                result.Banks = banks.Select(b => new BranchBankSettingDTO
                {
                    Id = b.Id,
                    BranchId = b.BranchId,
                    SlotIndex = b.SlotIndex,
                    BankName = b.BankName,
                    ImageUrl = b.ImageUrl,
                    IsActive = b.IsActive
                }).ToList();

                // Lấy PosConfigs của cơ sở
                var configs = await _context.PosConfigs
                    .Where(p => p.BranchId == branch.Id && p.IsActive)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();

                result.PosConfigs = configs.Select(p => new PosConfigSettingDTO
                {
                    Id = p.Id,
                    BranchId = p.BranchId,
                    PosName = p.PosName,
                    ImageUrl = p.ImageUrl,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive
                }).ToList();

                // Lấy ca chốt gần nhất của cơ sở để làm cơ sở tính số dư đầu ca
                var lastClosed = await _context.Shifts
                    .Include(s => s.ShiftPosEntries)
                    .Include(s => s.ShiftBankEntries)
                    .Include(s => s.ShiftEmployees).ThenInclude(se => se.User)
                    .Where(s => s.BranchId == branch.Id && s.Status != null && s.Status.StartsWith("Close"))
                    .OrderByDescending(s => s.ShiftDate)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (lastClosed != null)
                {
                    result.LastClosedShift = await _shiftRepository.GetShiftByIdAsync(lastClosed.Id);
                }
            }

            // 2. Lấy danh sách nhân viên & mật khẩu băm để xác thực offline
            var users = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            result.Users = users.Select(u => new UserDTO
            {
                Id = u.Id,
                Username = u.Username,
                PasswordHash = u.PasswordHash,
                FullName = u.FullName,
                Role = u.Role,
                ShiftCount = u.ShiftCount,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();

            // 3. Lấy danh mục ca
            result.ShiftTypes = _shiftRepository.GetShiftTypes();

            return Ok(result);
        }

        /// <summary>
        /// Tiếp nhận ca làm việc đã chốt từ SQLite máy con đẩy lên
        /// </summary>
        [HttpPost("upload-shift")]
        public async Task<ActionResult<SyncShiftResponseDTO>> UploadShift([FromBody] SyncShiftUploadDTO req)
        {
            if (req == null)
            {
                return BadRequest(new SyncShiftResponseDTO
                {
                    Success = false,
                    Message = "Dữ liệu ca đồng bộ không hợp lệ."
                });
            }

            DateOnly sDate = DateOnly.FromDateTime(req.ShiftDate);

            // Kiểm tra xem ca đã tồn tại trong SQL Server chưa
            var existingShift = await _context.Shifts
                .Include(s => s.ShiftPosEntries)
                .Include(s => s.ShiftBankEntries)
                .Include(s => s.ShiftExpenses)
                .Include(s => s.ShiftEmployees)
                .FirstOrDefaultAsync(s => s.BranchId == req.BranchId && s.ShiftDate == sDate && s.ShiftType == req.ShiftType);

            Shift targetShift;

            if (existingShift == null)
            {
                // Tạo mới ca từ dữ liệu offline
                targetShift = new Shift
                {
                    BranchId = req.BranchId,
                    ShiftDate = sDate,
                    ShiftType = req.ShiftType,
                    Status = req.Status ?? "Closed",
                    CashOpening = req.CashOpening,
                    CashClosing = req.CashClosing,
                    CashDifference = req.CashDifference,
                    OpenedByUserId = req.OpenedByUserId,
                    ClosedByUserId = req.ClosedByUserId,
                    OpenedAt = req.OpenedAt,
                    ClosedAt = req.ClosedAt,
                    Note = req.Note,
                    ActiveChannels = req.ActiveChannels,
                    CreatedAt = req.OpenedAt ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Shifts.Add(targetShift);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật lại ca đã có
                targetShift = existingShift;
                targetShift.Status = req.Status ?? "Closed";
                targetShift.CashOpening = req.CashOpening;
                targetShift.CashClosing = req.CashClosing;
                targetShift.CashDifference = req.CashDifference;
                if (req.ClosedByUserId.HasValue) targetShift.ClosedByUserId = req.ClosedByUserId;
                if (req.ClosedAt.HasValue) targetShift.ClosedAt = req.ClosedAt;
                if (!string.IsNullOrEmpty(req.Note)) targetShift.Note = req.Note;
                if (!string.IsNullOrEmpty(req.ActiveChannels)) targetShift.ActiveChannels = req.ActiveChannels;
                targetShift.UpdatedAt = DateTime.UtcNow;
            }

            // Đồng bộ POS Entries
            var branchPosConfigs = await _context.PosConfigs.Where(p => p.BranchId == req.BranchId && p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync();
            for (int i = 0; i < branchPosConfigs.Count; i++)
            {
                var pConf = branchPosConfigs[i];
                var entry = targetShift.ShiftPosEntries.FirstOrDefault(e => e.PosConfigId == pConf.Id);
                decimal opening = i == 0 ? req.Pos1Opening : (i == 1 ? req.Pos2Opening : req.Pos3Opening);
                decimal? closing = i == 0 ? req.Pos1Closing : (i == 1 ? req.Pos2Closing : req.Pos3Closing);
                decimal? night = i == 0 ? req.Pos1Night : (i == 1 ? req.Pos2Night : req.Pos3Night);

                if (entry == null)
                {
                    targetShift.ShiftPosEntries.Add(new ShiftPosEntry
                    {
                        ShiftId = targetShift.Id,
                        PosConfigId = pConf.Id,
                        PosOpening = opening,
                        PosClosing = closing,
                        PosClosingDay1 = night,
                        PosClosingDay2 = closing
                    });
                }
                else
                {
                    entry.PosOpening = opening;
                    entry.PosClosing = closing;
                    if (req.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.PosClosingDay1 = night;
                        entry.PosClosingDay2 = closing;
                    }
                }
            }

            // Đồng bộ Bank Entries
            var branchBanks = await _context.BranchBanks.Where(b => b.BranchId == req.BranchId && b.IsActive).OrderBy(b => b.SlotIndex).ToListAsync();
            for (int i = 0; i < branchBanks.Count; i++)
            {
                var bBank = branchBanks[i];
                var entry = targetShift.ShiftBankEntries.FirstOrDefault(e => e.BranchBankId == bBank.Id);
                decimal opening = i == 0 ? req.Bank1Opening : (i == 1 ? req.Bank2Opening : req.Bank3Opening);
                decimal? closing = i == 0 ? req.Bank1Closing : (i == 1 ? req.Bank2Closing : req.Bank3Closing);
                decimal? night = i == 0 ? req.Bank1Night : (i == 1 ? req.Bank2Night : req.Bank3Night);

                if (entry == null)
                {
                    targetShift.ShiftBankEntries.Add(new ShiftBankEntry
                    {
                        ShiftId = targetShift.Id,
                        BranchBankId = bBank.Id,
                        BankOpening = opening,
                        BankClosing = closing,
                        BankClosingDay1 = night,
                        BankClosingDay2 = closing
                    });
                }
                else
                {
                    entry.BankOpening = opening;
                    entry.BankClosing = closing;
                    if (req.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.BankClosingDay1 = night;
                        entry.BankClosingDay2 = closing;
                    }
                }
            }

            // Đồng bộ Expenses
            if (req.Expenses != null && req.Expenses.Count > 0)
            {
                int defaultUserId = req.ClosedByUserId ?? req.OpenedByUserId ?? 2;
                foreach (var exp in req.Expenses)
                {
                    if (!targetShift.ShiftExpenses.Any(e => e.Description == exp.Description && e.Amount == exp.Amount))
                    {
                        targetShift.ShiftExpenses.Add(new ShiftExpense
                        {
                            ShiftId = targetShift.Id,
                            Description = exp.Description,
                            Amount = exp.Amount,
                            CreatedByUserId = defaultUserId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // Đồng bộ nhân viên tham gia ca từ Signatures
            if (req.Signatures != null && req.Signatures.Count > 0)
            {
                foreach (var sig in req.Signatures)
                {
                    var u = await _context.Users.FirstOrDefaultAsync(user => user.Username.ToLower() == sig.Username.ToLower());
                    if (u != null && !targetShift.ShiftEmployees.Any(se => se.UserId == u.Id))
                    {
                        targetShift.ShiftEmployees.Add(new ShiftEmployee
                        {
                            ShiftId = targetShift.Id,
                            UserId = u.Id
                        });

                        // Tăng số ca làm của nhân viên
                        u.ShiftCount += 1;
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new SyncShiftResponseDTO
            {
                Success = true,
                Message = "Đồng bộ ca thành công vào máy chủ!",
                ClientShiftGuid = req.ClientShiftGuid,
                ServerShiftId = targetShift.Id
            });
        }
    }
}
