using ShiftHandOver.Client.Services;
using ShiftHandOver.Share;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace ShiftHandOver.Client.Admin
{
    public partial class AdminSecurityConfirmDialog : Window
    {
        public bool Confirmed { get; private set; } = false;
        public bool AutoBackup { get; private set; } = true;
        public string AdminUsername { get; private set; } = string.Empty;
        public string AdminPassword { get; private set; } = string.Empty;

        private readonly int _shiftCount;
        private readonly string _dateRangeText;

        public AdminSecurityConfirmDialog(int shiftCount, string dateRangeText)
        {
            InitializeComponent();
            _shiftCount = shiftCount;
            _dateRangeText = dateRangeText;

            TxtWarningMessage.Text = $"Bạn chuẩn bị xóa vĩnh viễn {_shiftCount} ca làm việc ({_dateRangeText}). Toàn bộ doanh thu, tiền két, biến động ngân hàng và phiếu chi của các ca này sẽ bị xóa sạch.";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            string username = (TxtAdminUser.Text ?? "").Trim();
            string password = TxtAdminPass.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tài khoản và mật khẩu Quản trị viên!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnConfirm.IsEnabled = false;
            try
            {
                var loginData = new LoginDTO
                {
                    UserName = username,
                    Password = password
                };

                var response = await ApiService.Client.PostAsJsonAsync("api/User/login", loginData);
                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Tài khoản hoặc mật khẩu Quản trị viên (Admin) không chính xác!", "Xác Thực Thất Bại", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var user = await response.Content.ReadFromJsonAsync<UserDTO>();
                if (user == null || !string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Tài khoản này không có quyền Quản trị viên (Admin)!", "Không Có Quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Nếu xác thực đúng tài khoản Admin -> Hiện thông báo xác nhận lần cuối
                var finalConfirm = MessageBox.Show(
                    $"XÁC NHẬN XÓA LẦN CUỐI:\n\n" +
                    $"Bạn có CHẮC CHẮN muốn xóa vĩnh viễn {_shiftCount} ca làm việc không?\n\n" +
                    $"• Khoảng thời gian: {_dateRangeText}\n" +
                    $"• Dữ liệu sẽ bị xóa: Toàn bộ chốt két, doanh thu POS, biến động ngân hàng và các phiếu chi liên quan.\n" +
                    $"• Thao tác này KHÔNG THỂ KHÔI PHỤC!\n\n" +
                    $"Bấm 'Yes' để xóa ngay, hoặc 'No' để hủy bỏ.",
                    "Xác Nhận Xóa Dữ Liệu",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (finalConfirm == MessageBoxResult.Yes)
                {
                    Confirmed = true;
                    AutoBackup = ChkAutoBackup?.IsChecked == true;
                    AdminUsername = username;
                    AdminPassword = password;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Người dùng bấm No -> Trở lại mà không làm gì
                    Confirmed = false;
                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối máy chủ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnConfirm.IsEnabled = true;
            }
        }
    }
}
