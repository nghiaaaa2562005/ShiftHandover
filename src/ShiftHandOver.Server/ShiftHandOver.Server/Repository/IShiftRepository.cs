using System.Collections.Generic;
using System.Threading.Tasks;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IShiftRepository
    {
        List<ShiftTypeDTO> GetShiftTypes();
        Task<ShiftHandoverDetailDTO> GetOrCreateShiftAsync(InitShiftRequestDTO req);
        Task<bool> ConfirmStartAsync(ConfirmStartRequestDTO req);
        Task<bool> CloseShiftAsync(CloseShiftRequestDTO req);
        Task<bool> RequestChangeInitialDataAsync(int shiftId, int userId);
        Task<bool> ConfirmChangeInitialDataAsync(ChangeInitialDataRequestDTO req);
    }
}

