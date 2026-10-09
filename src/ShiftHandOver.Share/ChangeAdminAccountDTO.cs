using System.ComponentModel.DataAnnotations;

namespace ShiftHandOver.Share
{
    public class ChangeAdminAccountDTO
    {
        [Required(ErrorMessage = "Tên đăng nhập hiện tại không được để trống.")]
        public string CurrentUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu hiện tại không được để trống.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên đăng nhập mới không được để trống.")]
        [StringLength(50, ErrorMessage = "Tên đăng nhập mới tối đa 50 ký tự.")]
        public string NewUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [StringLength(256, ErrorMessage = "Mật khẩu mới tối đa 256 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string? NewFullName { get; set; }
    }
}
