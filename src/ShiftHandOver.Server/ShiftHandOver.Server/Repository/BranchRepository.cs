using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public class BranchRepository : IBranchRepository
    {
        private readonly ShiftHandoverDbContext _context;

        public BranchRepository(ShiftHandoverDbContext context)
        {
            _context = context;
        }

        public async Task<List<BranchDTO>> GetActiveBranchesAsync()
        {
            return await _context.Branches
                .Where(b => b.IsActive)
                .Select(b => new BranchDTO
                {
                    Id = b.Id,
                    Name = b.Name,
                    DefaultCashOpening = b.DefaultCashOpening,
                    IsActive = b.IsActive
                })
                .ToListAsync();
        }

        public async Task<List<BranchDTO>> GetAllBranchesAsync()
        {
            return await _context.Branches
                .Select(b => new BranchDTO
                {
                    Id = b.Id,
                    Name = b.Name,
                    DefaultCashOpening = b.DefaultCashOpening,
                    IsActive = b.IsActive
                })
                .ToListAsync();
        }

        public async Task<bool> UpdateCashOpeningAsync(int branchId, decimal amount)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return false;
            branch.DefaultCashOpening = amount;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<BranchBankSettingDTO>> GetBranchBanksAsync(int branchId)
        {
            return await _context.BranchBanks
                .Where(b => b.BranchId == branchId)
                .OrderBy(b => b.SlotIndex)
                .Select(b => new BranchBankSettingDTO
                {
                    Id = b.Id,
                    BranchId = b.BranchId,
                    SlotIndex = b.SlotIndex,
                    BankName = b.BankName,
                    IsActive = b.IsActive
                })
                .ToListAsync();
        }

        public async Task<BranchDTO?> CreateBranchAsync(BranchDTO branchDto)
        {
            var branch = new Branch
            {
                Name = branchDto.Name.Trim(),
                DefaultCashOpening = branchDto.DefaultCashOpening > 0 ? branchDto.DefaultCashOpening : 2000000m,
                IsActive = true
            };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            // Khởi tạo mặc định 2 cổng ngân hàng và 2 máy POS cho cơ sở mới
            _context.BranchBanks.AddRange(
                new BranchBank { BranchId = branch.Id, SlotIndex = 1, BankName = "Ngân hàng 1", IsActive = true },
                new BranchBank { BranchId = branch.Id, SlotIndex = 2, BankName = "Ngân hàng 2", IsActive = true }
            );

            _context.PosConfigs.AddRange(
                new PosConfig { BranchId = branch.Id, PosName = "POS 1", DisplayOrder = 1, IsActive = true },
                new PosConfig { BranchId = branch.Id, PosName = "POS 2", DisplayOrder = 2, IsActive = true }
            );

            await _context.SaveChangesAsync();

            return new BranchDTO
            {
                Id = branch.Id,
                Name = branch.Name,
                DefaultCashOpening = branch.DefaultCashOpening,
                IsActive = branch.IsActive
            };
        }

        public async Task<bool> UpdateBranchAsync(BranchDTO branchDto)
        {
            var branch = await _context.Branches.FindAsync(branchDto.Id);
            if (branch == null) return false;

            if (!string.IsNullOrWhiteSpace(branchDto.Name)) branch.Name = branchDto.Name.Trim();
            if (branchDto.DefaultCashOpening >= 0) branch.DefaultCashOpening = branchDto.DefaultCashOpening;
            branch.IsActive = branchDto.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SaveBranchBanksAsync(int branchId, List<BranchBankSettingDTO> banks)
        {
            if (banks.Count > 2)
            {
                throw new InvalidOperationException("Mỗi cơ sở chỉ được cấu hình tối đa 2 ngân hàng / ví điện tử!");
            }

            var existing = await _context.BranchBanks.Where(b => b.BranchId == branchId).ToListAsync();

            // Deactivate or remove banks that were deleted in the UI
            var keptIds = banks.Where(b => b.Id > 0).Select(b => b.Id).ToHashSet();
            var toRemove = existing.Where(e => !keptIds.Contains(e.Id) && e.Id > 0).ToList();
            foreach (var rem in toRemove)
            {
                bool hasEntries = await _context.ShiftBankEntries.AnyAsync(s => s.BranchBankId == rem.Id);
                if (hasEntries)
                {
                    rem.IsActive = false;
                }
                else
                {
                    _context.BranchBanks.Remove(rem);
                    existing.Remove(rem);
                }
            }

            foreach (var dto in banks)
            {
                BranchBank? bank = null;
                if (dto.Id > 0)
                {
                    bank = existing.FirstOrDefault(e => e.Id == dto.Id);
                }
                if (bank == null)
                {
                    bank = existing.FirstOrDefault(e => e.SlotIndex == dto.SlotIndex);
                }

                if (bank != null)
                {
                    bank.BankName = dto.BankName;
                    bank.IsActive = dto.IsActive;
                    if (dto.SlotIndex > 0) bank.SlotIndex = dto.SlotIndex;
                }
                else
                {
                    byte nextSlot = dto.SlotIndex > 0 ? dto.SlotIndex : (byte)((existing.Any() ? existing.Max(e => (byte?)e.SlotIndex) ?? 0 : 0) + 1);
                    var newBank = new BranchBank
                    {
                        BranchId = branchId,
                        SlotIndex = nextSlot,
                        BankName = dto.BankName,
                        IsActive = dto.IsActive
                    };
                    existing.Add(newBank);
                    _context.BranchBanks.Add(newBank);
                }
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteBankAsync(int branchId, int bankId)
        {
            var bank = await _context.BranchBanks.FirstOrDefaultAsync(b => b.BranchId == branchId && b.Id == bankId);
            if (bank == null) return false;

            bool hasEntries = await _context.ShiftBankEntries.AnyAsync(e => e.BranchBankId == bankId);
            if (hasEntries)
            {
                bank.IsActive = false;
            }
            else
            {
                _context.BranchBanks.Remove(bank);
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PosConfigSettingDTO>> GetBranchPosConfigsAsync(int branchId)
        {
            return await _context.PosConfigs
                .Where(p => p.BranchId == branchId)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new PosConfigSettingDTO
                {
                    Id = p.Id,
                    BranchId = p.BranchId,
                    PosName = p.PosName,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive
                })
                .ToListAsync();
        }

        public async Task<bool> SaveBranchPosConfigsAsync(int branchId, List<PosConfigSettingDTO> posList)
        {
            if (posList.Count > 2)
            {
                throw new InvalidOperationException("Mỗi cơ sở chỉ được cấu hình tối đa 2 máy POS!");
            }

            var existing = await _context.PosConfigs.Where(p => p.BranchId == branchId).ToListAsync();

            // Deactivate or remove POS configs that were deleted in the UI
            var keptPosIds = posList.Where(p => p.Id > 0).Select(p => p.Id).ToHashSet();
            var toRemovePos = existing.Where(e => !keptPosIds.Contains(e.Id) && e.Id > 0).ToList();
            foreach (var rem in toRemovePos)
            {
                bool hasEntries = await _context.ShiftPosEntries.AnyAsync(s => s.PosConfigId == rem.Id);
                if (hasEntries)
                {
                    rem.IsActive = false;
                }
                else
                {
                    _context.PosConfigs.Remove(rem);
                    existing.Remove(rem);
                }
            }

            foreach (var dto in posList)
            {
                PosConfig? pos = null;
                if (dto.Id > 0)
                {
                    pos = existing.FirstOrDefault(e => e.Id == dto.Id);
                }

                if (pos != null)
                {
                    pos.PosName = dto.PosName;
                    pos.DisplayOrder = dto.DisplayOrder;
                    pos.IsActive = dto.IsActive;
                }
                else
                {
                    var newPos = new PosConfig
                    {
                        BranchId = branchId,
                        PosName = dto.PosName,
                        DisplayOrder = dto.DisplayOrder,
                        IsActive = dto.IsActive
                    };
                    existing.Add(newPos);
                    _context.PosConfigs.Add(newPos);
                }
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePosAsync(int branchId, int posId)
        {
            var pos = await _context.PosConfigs.FirstOrDefaultAsync(p => p.BranchId == branchId && p.Id == posId);
            if (pos == null) return false;

            bool hasEntries = await _context.ShiftPosEntries.AnyAsync(e => e.PosConfigId == posId);
            if (hasEntries)
            {
                pos.IsActive = false;
            }
            else
            {
                _context.PosConfigs.Remove(pos);
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<BranchHandoverConfigDTO>> GetAllBranchHandoverConfigsAsync()
        {
            var branches = await _context.Branches
                .Include(b => b.BranchBanks)
                .Include(b => b.PosConfigs)
                .ToListAsync();

            return branches.Select(b => new BranchHandoverConfigDTO
            {
                BranchId = b.Id,
                BranchName = b.Name,
                DefaultCashOpening = b.DefaultCashOpening,
                IsActive = b.IsActive,
                Banks = b.BranchBanks.OrderBy(x => x.SlotIndex).Select(x => new BranchBankSettingDTO
                {
                    Id = x.Id,
                    BranchId = x.BranchId,
                    SlotIndex = x.SlotIndex,
                    BankName = x.BankName,
                    IsActive = x.IsActive
                }).ToList(),
                PosConfigs = b.PosConfigs.OrderBy(p => p.DisplayOrder).Select(p => new PosConfigSettingDTO
                {
                    Id = p.Id,
                    BranchId = p.BranchId,
                    PosName = p.PosName,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive
                }).ToList()
            }).ToList();
        }

        public async Task<BranchHandoverConfigDTO?> GetBranchHandoverConfigAsync(int branchId)
        {
            var b = await _context.Branches
                .Include(x => x.BranchBanks)
                .Include(x => x.PosConfigs)
                .FirstOrDefaultAsync(x => x.Id == branchId);

            if (b == null) return null;

            return new BranchHandoverConfigDTO
            {
                BranchId = b.Id,
                BranchName = b.Name,
                DefaultCashOpening = b.DefaultCashOpening,
                IsActive = b.IsActive,
                Banks = b.BranchBanks.OrderBy(x => x.SlotIndex).Select(x => new BranchBankSettingDTO
                {
                    Id = x.Id,
                    BranchId = x.BranchId,
                    SlotIndex = x.SlotIndex,
                    BankName = x.BankName,
                    IsActive = x.IsActive
                }).ToList(),
                PosConfigs = b.PosConfigs.OrderBy(p => p.DisplayOrder).Select(p => new PosConfigSettingDTO
                {
                    Id = p.Id,
                    BranchId = p.BranchId,
                    PosName = p.PosName,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive
                }).ToList()
            };
        }

        public async Task<bool> SaveFullBranchHandoverConfigAsync(BranchHandoverConfigDTO config)
        {
            Branch? branch = null;
            if (config.BranchId > 0)
            {
                branch = await _context.Branches
                    .Include(b => b.BranchBanks)
                    .Include(b => b.PosConfigs)
                    .FirstOrDefaultAsync(b => b.Id == config.BranchId);
            }

            if (branch == null)
            {
                branch = new Branch
                {
                    Name = config.BranchName.Trim(),
                    DefaultCashOpening = config.DefaultCashOpening >= 0 ? config.DefaultCashOpening : 2000000m,
                    IsActive = config.IsActive
                };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();
                config.BranchId = branch.Id;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(config.BranchName)) branch.Name = config.BranchName.Trim();
                if (config.DefaultCashOpening >= 0) branch.DefaultCashOpening = config.DefaultCashOpening;
                branch.IsActive = config.IsActive;
            }

            // Sync Banks
            if (config.Banks != null)
            {
                await SaveBranchBanksAsync(branch.Id, config.Banks);
            }

            // Sync Pos
            if (config.PosConfigs != null)
            {
                await SaveBranchPosConfigsAsync(branch.Id, config.PosConfigs);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}

