using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftHandOver.Share
{
    public class UserDTO
    {

        public int Id { get; set; }
        [Required(ErrorMessage = "Username không được để trống.")]
        [StringLength(50, ErrorMessage = "Username tối đa 50 ký tự.")]
        public string Username { get; set; } = null!;
        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [StringLength(256, ErrorMessage = "Mật khẩu tối đa 256 ký tự.")]
        public string PasswordHash { get; set; } = null!;
        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string FullName { get; set; } = null!;
        [Required(ErrorMessage = "Role không được để trống.")]
        [RegularExpression("^(Admin|Employee)$", ErrorMessage = "Role chỉ được là 'Admin' hoặc 'Employee'.")]
        public string Role { get; set; } = null!;
        [Range(0, int.MaxValue, ErrorMessage = "ShiftCount không được là số âm.")]
        public int ShiftCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

