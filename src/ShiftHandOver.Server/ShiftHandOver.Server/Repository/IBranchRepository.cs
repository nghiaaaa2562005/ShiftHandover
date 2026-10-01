using System.Collections.Generic;
using System.Threading.Tasks;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IBranchRepository
    {
        Task<List<BranchDTO>> GetActiveBranchesAsync();
        Task<bool> UpdateCashOpeningAsync(int branchId, decimal amount);
        Task<List<BranchBankSettingDTO>> GetBranchBanksAsync(int branchId);
        Task<bool> SaveBranchBanksAsync(int branchId, List<BranchBankSettingDTO> banks);
        Task<List<PosConfigSettingDTO>> GetBranchPosConfigsAsync(int branchId);
        Task<bool> SaveBranchPosConfigsAsync(int branchId, List<PosConfigSettingDTO> posList);
    }
}

