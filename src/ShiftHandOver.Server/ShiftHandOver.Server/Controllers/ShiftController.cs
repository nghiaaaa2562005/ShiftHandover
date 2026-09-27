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

        /// <summary>
        /// Khởi tạo ca hoặc lấy chi tiết ca hiện tại.
        /// Kế thừa số liệu đầu ca từ ca trước, trạng thái ban đầu là NConfirm.
        /// </summary>
        [HttpPost("init")]
        public async Task<ActionResult<ShiftHandoverDetailDTO>> InitShift([FromBody] InitShiftRequestDTO req)
        {
            if (req.BranchId <= 0 || req.UserId <= 0)
            {
                return BadRequest("BranchId hoặc UserId không hợp lệ.");
            }

            var result = await _shiftRepository.GetOrCreateShiftAsync(req);
            return Ok(result);
        }

        /// <summary>
        /// Nhân viên bấm 'Xác nhận dữ liệu đầu ca' -> chuyển status từ NConfirm sang ConfirmStart
        /// </summary>
        [HttpPost("confirm-start")]
        public async Task<IActionResult> ConfirmStart([FromBody] ConfirmStartRequestDTO req)
        {
            var success = await _shiftRepository.ConfirmStartAsync(req);
            if (!success)
            {
                return NotFound("Không tìm thấy ca làm việc cần xác nhận.");
            }
            return Ok(new { message = "Xác nhận dữ liệu đầu ca thành công", status = "ConfirmStart" });
        }

        /// <summary>
        /// Nhân viên bấm 'Hoàn tất & Chốt ca' -> chuyển status sang Closed
        /// </summary>
        [HttpPost("close")]
        public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequestDTO req)
        {
            var success = await _shiftRepository.CloseShiftAsync(req);
            if (!success)
            {
                return NotFound("Không tìm thấy ca làm việc cần chốt.");
            }
            return Ok(new { message = "Đã chốt ca thành công", status = "Closed" });
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
            var success = await _shiftRepository.ConfirmChangeInitialDataAsync(req);
            if (!success)
            {
                return NotFound("Không tìm thấy ca làm việc để cập nhật.");
            }
            return Ok(new { message = "Đã cập nhật thông tin đầu ca thành công", status = "ConfirmStart" });
        }
    }
}

