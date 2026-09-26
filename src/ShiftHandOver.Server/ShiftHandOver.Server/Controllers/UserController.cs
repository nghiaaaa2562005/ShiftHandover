using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftHandOver.Server.Repository;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        [HttpPost]
        public async Task<IActionResult> AddUser(UserDTO u)
        {
            await _userRepository.AddUser(u);
            return Ok();
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _userRepository.DeleteUser(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetAllUser()
        {
            var user = await _userRepository.GetAllUser();
            return Ok(user);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var user = await _userRepository.GetUserById(id);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id ,UserDTO u)
        {
            try
            {
               await _userRepository.UpdateUser(id, u);
                return StatusCode(200,u);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });

            }
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDTO){
            var user = await _userRepository.CheckLogin(loginDTO.UserName, loginDTO.Password);
            if(user == null)
            {
                return Unauthorized(new { message = "Tài khoản của bạn không chính xác!" });
            }
            if (user.Role != "Admin")
            {
                return StatusCode(403, new { message = "Tài khoản Admin mới được vào đây" });
            }
            return Ok(user);
        }

    }
}