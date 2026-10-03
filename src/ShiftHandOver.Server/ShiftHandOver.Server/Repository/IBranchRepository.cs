using System.Collections.Generic;
using System.Threading.Tasks;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IBranchRepository
    {
        Task<List<BranchDTO>> GetActiveBranchesAsync();
        Task<List<BranchDTO>> GetAllBranchesAsync();
        Task<BranchDTO?> CreateBranchAsync(BranchDTO branch);
        Task<bool> UpdateBranchAsync(BranchDTO branch);
        Task<bool> UpdateCashOpeningAsync(int branchId, decimal amount);
        Task<List<BranchBankSettingDTO>> GetBranchBanksAsync(int branchId);
        Task<bool> SaveBranchBanksAsync(int branchId, List<BranchBankSettingDTO> banks);
        Task<bool> DeleteBankAsync(int branchId, int bankId);
        Task<List<PosConfigSettingDTO>> GetBranchPosConfigsAsync(int branchId);
        Task<bool> SaveBranchPosConfigsAsync(int branchId, List<PosConfigSettingDTO> posList);
        Task<bool> DeletePosAsync(int branchId, int posId);

        Task<List<BranchHandoverConfigDTO>> GetAllBranchHandoverConfigsAsync();
        Task<BranchHandoverConfigDTO?> GetBranchHandoverConfigAsync(int branchId);
        Task<bool> SaveFullBranchHandoverConfigAsync(BranchHandoverConfigDTO config);
    }
}

