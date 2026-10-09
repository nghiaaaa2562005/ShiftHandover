using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Validations;
using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ShiftHandoverDbContext _context;
        public UserRepository(ShiftHandoverDbContext context)
        {
            _context = context;
        }
        public async Task AddUser(UserDTO user)
        {
            var u = new User
            {
                Id = 0,
                Username = user.Username,
                PasswordHash = user.PasswordHash,
                FullName = user.FullName,
                Role = user.Role,
                ShiftCount = user.ShiftCount,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
            };
            await _context.Users.AddAsync(u);
            await _context.SaveChangesAsync();
        }

        public async Task<UserDTO?> CheckLogin(string name, string password)
        {
            name = name.Trim();
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username==name && u.PasswordHash==password);
            if (user != null) 
            {
                return new UserDTO
                {
                    Username = user.Username,
                    PasswordHash = user.PasswordHash,
                    FullName = user.FullName,
                    Role = user.Role,
                    ShiftCount = user.ShiftCount,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    Id = user.Id
                };
            }
            return null;
        }

        public async Task DeleteUser(int id)
        {
            var user = await _context.Users.SingleOrDefaultAsync(c => c.Id == id);
            if (user != null)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new KeyNotFoundException($"Không tìm thấy sản phẩm có id = {id}!");
            }
        }

        public async Task<List<UserDTO>> GetAllUser()
        {
            var usesrs = await _context.Users.Where(p => p.IsActive==true).Select(p => new UserDTO
            {
                Id = p.Id,
                FullName = p.FullName,
                Role = p.Role,
                ShiftCount = p.ShiftCount,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                PasswordHash = p.PasswordHash,
                Username = p.Username
            }).ToListAsync();
            return usesrs;
        }

        public async Task<UserDTO> GetUserById(int id)
        {
            var p = await _context.Users.SingleOrDefaultAsync(p => p.Id == id);
            if (p != null)
            {
                return new UserDTO
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    Role = p.Role,
                    ShiftCount = p.ShiftCount,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    PasswordHash = p.PasswordHash,
                    Username = p.Username
                };
            }
            throw new KeyNotFoundException($"Không tìm thấy sản phẩm nào có Id là {id}!");
        }

        public async Task UpdateUser(int id, UserDTO userDTO)
        {
            var p = await _context.Users.SingleOrDefaultAsync(p => p.Id == id);
            if (p != null)
            {
            p.FullName = userDTO.FullName;
                p.Username = userDTO.Username;
                p.Role = userDTO.Role;
                p.ShiftCount = userDTO.ShiftCount;
                p.IsActive = userDTO.IsActive;
                p.PasswordHash = userDTO.PasswordHash;
                p.CreatedAt = userDTO.CreatedAt;
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new KeyNotFoundException($"Không tìm thấy sản phẩm có Id = {id}");
            }
        }

        public async Task<(bool Success, string Message)> ChangeAdminAccountAsync(ChangeAdminAccountDTO dto)
        {
            if (dto == null)
            {
                return (false, "Dữ liệu yêu cầu không hợp lệ!");
            }

            string curUser = (dto.CurrentUsername ?? "").Trim();
            string curPass = (dto.CurrentPassword ?? "").Trim();
            string newUser = (dto.NewUsername ?? "").Trim();
            string newPass = (dto.NewPassword ?? "").Trim();
            string newName = (dto.NewFullName ?? "").Trim();

            if (string.IsNullOrEmpty(curUser) || string.IsNullOrEmpty(curPass))
            {
                return (false, "Vui lòng nhập tài khoản và mật khẩu Quản trị viên hiện tại!");
            }

            if (string.IsNullOrEmpty(newUser) || string.IsNullOrEmpty(newPass))
            {
                return (false, "Vui lòng nhập tên tài khoản mới và mật khẩu mới!");
            }

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == curUser && u.PasswordHash == curPass);
            if (admin == null || !string.Equals(admin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Tài khoản hoặc mật khẩu Quản trị viên (Admin) hiện tại không chính xác!");
            }

            // Nếu đổi tên đăng nhập thì kiểm tra trùng lặp
            if (!string.Equals(curUser, newUser, StringComparison.OrdinalIgnoreCase))
            {
                bool exists = await _context.Users.AnyAsync(u => u.Id != admin.Id && u.Username.ToLower() == newUser.ToLower());
                if (exists)
                {
                    return (false, $"Tên tài khoản '{newUser}' đã tồn tại trong hệ thống. Vui lòng chọn tên khác!");
                }
            }

            admin.Username = newUser;
            admin.PasswordHash = newPass;
            if (!string.IsNullOrWhiteSpace(newName))
            {
                admin.FullName = newName;
            }

            await _context.SaveChangesAsync();
            return (true, "Thay đổi tài khoản và mật khẩu Quản trị viên thành công!");
        }
    }
}
