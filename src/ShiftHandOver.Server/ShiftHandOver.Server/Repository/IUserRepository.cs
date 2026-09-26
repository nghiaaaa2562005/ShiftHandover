using ShiftHandOver.Server.Models;
using ShiftHandOver.Share;

namespace ShiftHandOver.Server.Repository
{
    public interface IUserRepository
    {
        public  Task<List<UserDTO>> GetAllUser();
        public  Task<UserDTO> GetUserById(int id);
        public Task UpdateUser(int id,UserDTO userDTO);
        public Task DeleteUser(int id);
        public Task AddUser(UserDTO user);
        public Task<UserDTO?> CheckLogin(string name, string password);
    }
}
