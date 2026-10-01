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

        public async Task<bool> SaveBranchBanksAsync(int branchId, List<BranchBankSettingDTO> banks)
        {
            var existing = await _context.BranchBanks.Where(b => b.BranchId == branchId).ToListAsync();
            foreach (var dto in banks)
            {
                var bank = existing.FirstOrDefault(e => e.SlotIndex == dto.SlotIndex);
                if (bank != null)
                {
                    bank.BankName = dto.BankName;
                    bank.IsActive = dto.IsActive;
                }
                else
                {
                    _context.BranchBanks.Add(new BranchBank
                    {
                        BranchId = branchId,
                        SlotIndex = dto.SlotIndex,
                        BankName = dto.BankName,
                        IsActive = dto.IsActive
                    });
                }
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
            var existing = await _context.PosConfigs.Where(p => p.BranchId == branchId).ToListAsync();
            foreach (var dto in posList)
            {
                var pos = existing.FirstOrDefault(e => e.Id == dto.Id || e.DisplayOrder == dto.DisplayOrder);
                if (pos != null)
                {
                    pos.PosName = dto.PosName;
                    pos.DisplayOrder = dto.DisplayOrder;
                    pos.IsActive = dto.IsActive;
                }
                else
                {
                    _context.PosConfigs.Add(new PosConfig
                    {
                        BranchId = branchId,
                        PosName = dto.PosName,
                        DisplayOrder = dto.DisplayOrder,
                        IsActive = dto.IsActive
                    });
                }
            }
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

