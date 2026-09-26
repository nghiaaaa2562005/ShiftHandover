using System.Collections.Generic;
using System.Threading.Tasks;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IBranchRepository
    {
        Task<List<BranchDTO>> GetActiveBranchesAsync();
    }
}
