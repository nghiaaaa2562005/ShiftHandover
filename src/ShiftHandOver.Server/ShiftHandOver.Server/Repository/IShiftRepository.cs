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
        Task<CloseShiftResponseDTO> CloseShiftAsync(CloseShiftRequestDTO req);
        Task<bool> RequestChangeInitialDataAsync(int shiftId, int userId);
        Task<bool> ConfirmChangeInitialDataAsync(ChangeInitialDataRequestDTO req);
        Task<List<EmployeeShiftStatisticsDTO>> GetEmployeeStatisticsAsync();
        Task<VerifyShiftOwnerResponseDTO> VerifyShiftOwnerAsync(VerifyShiftOwnerRequestDTO req);
        Task<bool> UpdateClosedShiftAsync(UpdateClosedShiftRequestDTO req);
        Task<List<AdminShiftSummaryDTO>> GetAllShiftsAsync();
        Task<List<AdminShiftSummaryDTO>> GetRecentDifferencesAsync();
        Task<List<AdminExpenseDTO>> GetAllExpensesAsync();
        Task<ShiftHandoverDetailDTO?> GetShiftByIdAsync(int id);
        Task<int> AutoCloseExpiredShiftsAsync();
        Task<bool> UpdateShiftChannelsAsync(int shiftId, ShiftChannelSelection channels);
    }
}

