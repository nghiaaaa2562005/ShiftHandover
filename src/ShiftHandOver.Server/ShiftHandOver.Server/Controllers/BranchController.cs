using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShiftHandOver.Server.Repository;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchController : ControllerBase
    {
        private readonly IBranchRepository _branchRepository;

        public BranchController(IBranchRepository branchRepository)
        {
            _branchRepository = branchRepository;
        }

        [HttpGet]
        public async Task<ActionResult<List<BranchDTO>>> GetBranches([FromQuery] bool? activeOnly = null)
        {
            if (activeOnly == true)
            {
                var active = await _branchRepository.GetActiveBranchesAsync();
                return Ok(active);
            }
            var branches = await _branchRepository.GetAllBranchesAsync();
            return Ok(branches);
        }

        [HttpGet("handover-configs")]
        public async Task<ActionResult<List<BranchHandoverConfigDTO>>> GetAllBranchHandoverConfigs()
        {
            var configs = await _branchRepository.GetAllBranchHandoverConfigsAsync();
            return Ok(configs);
        }

        [HttpGet("{id}/handover-config")]
        public async Task<ActionResult<BranchHandoverConfigDTO>> GetBranchHandoverConfig(int id)
        {
            var config = await _branchRepository.GetBranchHandoverConfigAsync(id);
            if (config == null) return NotFound(new { message = "Không tìm thấy cấu hình cơ sở!" });
            return Ok(config);
        }

        [HttpPost("handover-config")]
        public async Task<IActionResult> SaveFullBranchHandoverConfig([FromBody] BranchHandoverConfigDTO req)
        {
            if (string.IsNullOrWhiteSpace(req.BranchName))
            {
                return BadRequest(new { message = "Tên cơ sở bán hàng không được để trống!" });
            }
            var success = await _branchRepository.SaveFullBranchHandoverConfigAsync(req);
            return Ok(new { message = "Lưu cấu hình biên bản chốt ca cơ sở thành công!" });
        }

        [HttpPost]
        public async Task<ActionResult<BranchDTO>> CreateBranch([FromBody] BranchDTO req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return BadRequest(new { message = "Tên cơ sở không được để trống!" });
            }
            var created = await _branchRepository.CreateBranchAsync(req);
            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(int id, [FromBody] BranchDTO req)
        {
            req.Id = id;
            var success = await _branchRepository.UpdateBranchAsync(req);
            if (!success) return NotFound(new { message = "Không tìm thấy cơ sở!" });
            return Ok(new { message = "Cập nhật thông tin cơ sở thành công!" });
        }

        [HttpPut("{id}/cash-opening")]
        public async Task<IActionResult> UpdateCashOpening(int id, [FromBody] UpdateCashOpeningDTO req)
        {
            var success = await _branchRepository.UpdateCashOpeningAsync(id, req.DefaultCashOpening);
            if (!success) return NotFound(new { message = "Không tìm thấy cơ sở!" });
            return Ok(new { message = "Cập nhật mức tiền mặt đầu ca thành công!" });
        }

        [HttpGet("{id}/banks")]
        public async Task<ActionResult<List<BranchBankSettingDTO>>> GetBranchBanks(int id)
        {
            var banks = await _branchRepository.GetBranchBanksAsync(id);
            return Ok(banks);
        }

        [HttpPost("{id}/banks")]
        public async Task<IActionResult> SaveBranchBanks(int id, [FromBody] List<BranchBankSettingDTO> banks)
        {
            var success = await _branchRepository.SaveBranchBanksAsync(id, banks);
            return Ok(new { message = "Lưu cấu hình ngân hàng cơ sở thành công!" });
        }

        [HttpDelete("{id}/banks/{bankId}")]
        public async Task<IActionResult> DeleteBank(int id, int bankId)
        {
            var success = await _branchRepository.DeleteBankAsync(id, bankId);
            if (!success) return NotFound(new { message = "Không tìm thấy ngân hàng để xóa!" });
            return Ok(new { message = "Đã xóa ngân hàng cơ sở thành công!" });
        }

        [HttpGet("{id}/pos")]
        public async Task<ActionResult<List<PosConfigSettingDTO>>> GetBranchPos(int id)
        {
            var pos = await _branchRepository.GetBranchPosConfigsAsync(id);
            return Ok(pos);
        }

        [HttpPost("{id}/pos")]
        public async Task<IActionResult> SaveBranchPos(int id, [FromBody] List<PosConfigSettingDTO> posList)
        {
            var success = await _branchRepository.SaveBranchPosConfigsAsync(id, posList);
            return Ok(new { message = "Lưu cấu hình POS cơ sở thành công!" });
        }

        [HttpDelete("{id}/pos/{posId}")]
        public async Task<IActionResult> DeletePos(int id, int posId)
        {
            var success = await _branchRepository.DeletePosAsync(id, posId);
            if (!success) return NotFound(new { message = "Không tìm thấy POS để xóa!" });
            return Ok(new { message = "Đã xóa POS cơ sở thành công!" });
        }

        /// <summary>
        /// Tải lên ảnh / logo cho Ngân hàng hoặc App POS
        /// </summary>
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn tệp ảnh để tải lên!" });
            }

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg", ".ico" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(new { message = "Định dạng ảnh không hợp lệ! Vui lòng chọn ảnh .png, .jpg, .webp" });
            }

            var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logos");
            if (!Directory.Exists(webRoot))
            {
                Directory.CreateDirectory(webRoot);
            }

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(webRoot, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/uploads/logos/{fileName}";
            return Ok(new { success = true, url = relativeUrl, fileName = fileName });
        }

        /// <summary>
        /// Lấy danh sách các logo ngân hàng & POS có sẵn
        /// </summary>
        [HttpGet("preset-logos")]
        public IActionResult GetPresetLogos()
        {
            var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logos");
            if (!Directory.Exists(webRoot)) return Ok(new List<string>());

            var files = Directory.GetFiles(webRoot)
                .Where(f => !f.Contains("Screenshot") && (f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".webp")))
                .Select(f => $"/uploads/logos/{Path.GetFileName(f)}")
                .ToList();

            return Ok(files);
        }
    }
}
