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
        public async Task<ActionResult<List<BranchDTO>>> GetActiveBranches()
        {
            var branches = await _branchRepository.GetActiveBranchesAsync();
            return Ok(branches);
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
    }
}
