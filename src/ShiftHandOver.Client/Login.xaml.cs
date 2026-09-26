using ShiftHandOver.Share;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace ShiftHandOver.Client
{
    public partial class Login : Window
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        public Login()
        {
            InitializeComponent();
            if (dpWorkDate != null)
            {
                dpWorkDate.SelectedDate = DateTime.Today;
            }
        }

        #region Chuyển đổi giữa Đăng nhập Nhân viên & Quản trị viên (Admin)
        private void BtnToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (pnlAdminLogin.Visibility == Visibility.Visible)
            {
                // Đang ở Admin -> Chuyển về Nhân viên
                pnlAdminLogin.Visibility = Visibility.Collapsed;
                pnlEmployeeLogin.Visibility = Visibility.Visible;
                btnToggleAdmin.Content = "🔑 Đăng nhập quyền Admin";
            }
            else
            {
                // Đang ở Nhân viên -> Chuyển sang Admin
                pnlEmployeeLogin.Visibility = Visibility.Collapsed;
                pnlAdminLogin.Visibility = Visibility.Visible;
                btnToggleAdmin.Content = "👤 Đăng nhập Nhân viên";
            }
        }

        private void BtnBackToEmployee_Click(object sender, RoutedEventArgs e)
        {
            pnlAdminLogin.Visibility = Visibility.Collapsed;
            pnlEmployeeLogin.Visibility = Visibility.Visible;
            btnToggleAdmin.Content = "🔑 Đăng nhập quyền Admin";
        }
        #endregion

        #region Xử Lý Vào Ca Bàn Giao
        private void BtnLoginEmployee_Click(object sender, RoutedEventArgs e)
        {
            string branch = (cboBranch.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cơ sở 96";
            string shift = (cboShift.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ca Tối";
            DateTime workDate = dpWorkDate.SelectedDate ?? DateTime.Today;

            // Mở trang chốt và nhận ca của nhân viên
            var handoverReportWindow = new Employee.ShiftHandoverReport("Nguyễn Văn Duy", branch, shift, workDate);
            handoverReportWindow.Show();

            // Đóng cửa sổ đăng nhập
            this.Close();
        }
        #endregion

        #region Xử Lý Đăng Nhập Quản Trị Viên (Admin)
        private async void BtnLoginAdmin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtAdminUser.Text.Trim();
            string password = txtAdminPass.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tài khoản và mật khẩu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var loginData = new LoginDTO
                {
                    UserName = username,
                    Password = password
                };
                //Gui lennn api
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/User/login",loginData);
                if (response.IsSuccessStatusCode)
                {
                    var userDtoLog = await response.Content.ReadFromJsonAsync<UserDTO>();
                    var adminDash = new Admin.AdminDashBoard();
                    adminDash.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Thông tin cung cấp sai !");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi:"+ex.ToString());
            }
        }
        #endregion
    }
}
