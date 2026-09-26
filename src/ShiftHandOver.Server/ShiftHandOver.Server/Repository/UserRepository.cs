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
        
    }
}
