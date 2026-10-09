using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShiftHandOver.Server.Repository;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShiftController : ControllerBase
    {
        private readonly IShiftRepository _shiftRepository;

        public ShiftController(IShiftRepository shiftRepository)
        {
            _shiftRepository = shiftRepository;
        }

        [HttpGet("types")]
        public ActionResult<List<ShiftTypeDTO>> GetShiftTypes()
        {
            return Ok(_shiftRepository.GetShiftTypes());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftHandoverDetailDTO>> GetShiftById(int id)
        {
            var detail = await _shiftRepository.GetShiftByIdAsync(id);
            if (detail == null)
            {
                return NotFound(new { message = $"Không tìm thấy ca làm việc với ID = {id}" });
            }
            return Ok(detail);
        }

        /// <summary>
        /// Khởi tạo ca hoặc lấy chi tiết ca hiện tại.
        /// Kế thừa số liệu đầu ca từ ca trước, trạng thái ban đầu là NConfirm.
        /// </summary>
        [HttpPost("init")]
        public async Task<ActionResult<ShiftHandoverDetailDTO>> InitShift([FromBody] InitShiftRequestDTO req)
        {
            if (req.BranchId <= 0)
            {
                return BadRequest("BranchId không hợp lệ.");
            }

            try
            {
                var result = await _shiftRepository.GetOrCreateShiftAsync(req);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lưu hoặc cập nhật danh sách các kênh bán hàng (App POS, Ngân hàng) kích hoạt trong ca
        /// </summary>
        [HttpPost("{id}/channels")]
        public async Task<IActionResult> UpdateChannels(int id, [FromBody] ShiftChannelSelection channels)
        {
            var success = await _shiftRepository.UpdateShiftChannelsAsync(id, channels);
            if (!success)
            {
                return NotFound(new { message = "Không tìm thấy ca làm việc cần cập nhật kênh!" });
            }
            return Ok(new { success = true, message = "Đã cập nhật kênh bán hàng cho ca thành công!" });
        }

        /// <summary>
        /// Tự động chốt các ca chưa chốt của các ngày hôm trước bởi tài khoản Admin
        /// </summary>
        [HttpPost("auto-close-expired")]
        public async Task<IActionResult> AutoCloseExpiredShifts()
        {
            var count = await _shiftRepository.AutoCloseExpiredShiftsAsync();
            return Ok(new { success = true, closedCount = count, message = $"Đã tự động chốt {count} ca quá hạn bằng tài khoản Admin." });
        }

        /// <summary>
        /// Nhân viên bấm 'Xác nhận dữ liệu đầu ca' -> chuyển status từ NConfirm sang ConfirmStart
        /// </summary>
        [HttpPost("confirm-start")]
        public async Task<IActionResult> ConfirmStart([FromBody] ConfirmStartRequestDTO req)
        {
            try
            {
                var success = await _shiftRepository.ConfirmStartAsync(req);
                if (!success)
                {
                    return NotFound("Không tìm thấy ca làm việc cần xác nhận.");
                }
                return Ok(new { message = "Xác nhận dữ liệu đầu ca thành công", status = "ConfirmStart" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Nhân viên bấm 'Hoàn tất & Chốt ca' -> xác thực chữ ký (tài khoản & mật khẩu) và chuyển status sang Closed
        /// </summary>
        [HttpPost("close")]
        public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequestDTO req)
        {
            var res = await _shiftRepository.CloseShiftAsync(req);
            if (!res.Success)
            {
                return BadRequest(res.Message);
            }
            return Ok(res);
        }

        /// <summary>
        /// Thống kê chi tiết số ca làm, số ca âm, số ca dương và số ca khớp của từng nhân viên (không triệt tiêu)
        /// </summary>
        [HttpGet("employee-statistics")]
        public async Task<ActionResult<List<EmployeeShiftStatisticsDTO>>> GetEmployeeStatistics()
        {
            var stats = await _shiftRepository.GetEmployeeStatisticsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Nhân viên bấm 'Thay đổi thông tin' -> chuyển status sang Changed
        /// </summary>
        [HttpPost("request-change")]
        public async Task<IActionResult> RequestChange([FromBody] ConfirmStartRequestDTO req)
        {
            var success = await _shiftRepository.RequestChangeInitialDataAsync(req.ShiftId, req.UserId);
            if (!success)
            {
                return NotFound("Không tìm thấy ca làm việc.");
            }
            return Ok(new { message = "Yêu cầu thay đổi thông tin đầu ca thành công", status = "Changed" });
        }

        /// <summary>
        /// Nhân viên bấm 'Xác nhận thay đổi' -> lưu số liệu đầu ca mới và chuyển status sang ConfirmStart
        /// </summary>
        [HttpPost("confirm-change")]
        public async Task<IActionResult> ConfirmChange([FromBody] ChangeInitialDataRequestDTO req)
        {
            try
            {
                var success = await _shiftRepository.ConfirmChangeInitialDataAsync(req);
                if (!success)
                {
                    return NotFound("Không tìm thấy ca làm việc để cập nhật.");
                }
                return Ok(new { message = "Đã cập nhật thông tin đầu ca thành công", status = "ConfirmStart" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xác thực tài khoản & mật khẩu của người phụ trách ca khi muốn sửa đổi ca đã chốt
        /// </summary>
        [HttpPost("verify-owner")]
        public async Task<ActionResult<VerifyShiftOwnerResponseDTO>> VerifyShiftOwner([FromBody] VerifyShiftOwnerRequestDTO req)
        {
            var result = await _shiftRepository.VerifyShiftOwnerAsync(req);
            if (!result.IsAuthorized)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        /// <summary>
        /// Cập nhật thay đổi số liệu cho ca đã chốt (sau khi đã xác thực người phụ trách ca)
        /// </summary>
        [HttpPost("update-closed-shift")]
        public async Task<IActionResult> UpdateClosedShift([FromBody] UpdateClosedShiftRequestDTO req)
        {
            var success = await _shiftRepository.UpdateClosedShiftAsync(req);
            if (!success)
            {
                return NotFound("Không tìm thấy ca làm việc cần cập nhật.");
            }
            return Ok(new { message = "Cập nhật thay đổi ca đã chốt thành công!", status = "Closed" });
        }

        /// <summary>
        /// Lấy tất cả ca làm việc cho Admin Dashboard
        /// </summary>
        [HttpGet("all-shifts")]
        public async Task<ActionResult<List<AdminShiftSummaryDTO>>> GetAllShifts()
        {
            var shifts = await _shiftRepository.GetAllShiftsAsync();
            return Ok(shifts);
        }

        /// <summary>
        /// Lấy các ca bị lệch tiền gần đây cho Admin Dashboard
        /// </summary>
        [HttpGet("recent-differences")]
        public async Task<ActionResult<List<AdminShiftSummaryDTO>>> GetRecentDifferences()
        {
            var diffs = await _shiftRepository.GetRecentDifferencesAsync();
            return Ok(diffs);
        }

        /// <summary>
        /// Lấy danh sách thu chi két trong các ca cho Admin Dashboard
        /// </summary>
        [HttpGet("expenses")]
        public async Task<ActionResult<List<AdminExpenseDTO>>> GetAllExpenses()
        {
            var expenses = await _shiftRepository.GetAllExpensesAsync();
            return Ok(expenses);
        }

        /// <summary>
        /// Lấy danh sách ca làm việc theo bộ lọc khoảng ngày để chuẩn bị Reset / Xóa
        /// </summary>
        [HttpGet("reset-filter")]
        public async Task<ActionResult<List<ResetShiftItemDTO>>> GetShiftsForReset([FromQuery] string? fromDate, [FromQuery] string? toDate, [FromQuery] int? branchId)
        {
            DateOnly? from = null;
            if (!string.IsNullOrEmpty(fromDate) && DateOnly.TryParse(fromDate, out var f)) from = f;

            DateOnly? to = null;
            if (!string.IsNullOrEmpty(toDate) && DateOnly.TryParse(toDate, out var t)) to = t;

            var result = await _shiftRepository.GetShiftsForResetAsync(from, to, branchId);
            return Ok(result);
        }

        /// <summary>
        /// Xóa vĩnh viễn các ca làm việc trong khoảng thời gian (Yêu cầu mật khẩu Admin)
        /// </summary>
        [HttpPost("reset-shifts")]
        public async Task<ActionResult<ResetShiftsResponseDTO>> ResetShifts([FromBody] ResetShiftsRequestDTO req)
        {
            var result = await _shiftRepository.ResetShiftsAsync(req);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}

