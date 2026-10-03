using ShiftHandOver.Client.Services;
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
        public Login()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (dpWorkDate != null)
            {
                dpWorkDate.SelectedDate = DateTime.Today;
            }

            await LoadBranchesAsync();
            await LoadShiftsAsync();
        }

        private async System.Threading.Tasks.Task LoadBranchesAsync()
        {
            try
            {
                // Lấy danh sách cơ sở thực tế từ bảng Branches trong Database
                var branches = await ApiService.Client.GetFromJsonAsync<System.Collections.Generic.List<BranchDTO>>("api/Branch");
                if (branches != null && branches.Count > 0)
                {
                    cboBranch.ItemsSource = branches;
                    cboBranch.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể kết nối tới Server/CSDL để lấy dữ liệu Cơ sở!\n\nLỗi: " + ex.Message + "\n\nVui lòng đảm bảo Server (ShiftHandOver.Server) đang chạy ở http://localhost:5000.", 
                                "Lỗi Kết Nối CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadShiftsAsync()
        {
            try
            {
                // Lấy danh sách 4 ca làm việc từ Server
                var shifts = await ApiService.Client.GetFromJsonAsync<System.Collections.Generic.List<ShiftTypeDTO>>("api/Shift/types");
                if (shifts != null && shifts.Count > 0)
                {
                    cboShift.ItemsSource = shifts;
                    cboShift.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi tải ca: " + ex.Message);
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
                btnToggleAdmin.Content = "Đăng nhập quyền Admin";
            }
            else
            {
                // Đang ở Nhân viên -> Chuyển sang Admin
                pnlEmployeeLogin.Visibility = Visibility.Collapsed;
                pnlAdminLogin.Visibility = Visibility.Visible;
                btnToggleAdmin.Content = "Đăng nhập Nhân viên";
            }
        }

        private void BtnBackToEmployee_Click(object sender, RoutedEventArgs e)
        {
            pnlAdminLogin.Visibility = Visibility.Collapsed;
            pnlEmployeeLogin.Visibility = Visibility.Visible;
            btnToggleAdmin.Content = "Đăng nhập quyền Admin";
        }
        #endregion

        #region Xử Lý Vào Ca Bàn Giao
        private async void BtnLoginEmployee_Click(object sender, RoutedEventArgs e)
        {
            var selectedBranch = cboBranch.SelectedItem as BranchDTO;
            if (selectedBranch == null)
            {
                MessageBox.Show("Vui lòng chọn cơ sở làm việc!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedShift = cboShift.SelectedItem as ShiftTypeDTO;
            if (selectedShift == null)
            {
                MessageBox.Show("Vui lòng chọn ca làm việc!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!dpWorkDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng chọn ngày làm việc!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime workDate = dpWorkDate.SelectedDate.Value.Date;
            DateTime today = DateTime.Today;
            string shiftCode = selectedShift.Code.ToUpper().Trim();

            // 1. Tuyệt đối không tạo ca tương lai
            if (workDate > today)
            {
                MessageBox.Show("Không thể mở ca làm việc của ngày tương lai.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Gọi Server kiểm tra / khởi tạo ca:
            // - Quy tắc 1: Muốn mở ca mới / ca tiếp theo thì bắt buộc ca trước phải chốt và có người ký tên chịu trách nhiệm
            // - Quy tắc 2: Chốt ca không giới hạn thời gian (có thể để quá giờ bàn giao sang ca khác mới chốt)
            try
            {
                var req = new InitShiftRequestDTO
                {
                    BranchId = selectedBranch.Id,
                    ShiftDate = workDate,
                    ShiftType = shiftCode,
                    UserId = 2
                };

                var response = await ApiService.Client.PostAsJsonAsync("api/Shift/init", req);
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    string message = errorContent;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(errorContent);
                        if (doc.RootElement.TryGetProperty("message", out var msgProp))
                        {
                            message = msgProp.GetString() ?? errorContent;
                        }
                    }
                    catch { }

                    MessageBox.Show(message, "Yêu cầu chốt ca trước", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var shiftDetail = await response.Content.ReadFromJsonAsync<ShiftHandoverDetailDTO>();
                if (shiftDetail == null)
                {
                    MessageBox.Show("Không thể nạp dữ liệu chi tiết ca từ máy chủ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3. Mở biên bản chốt ca:
                // - isReadOnly = true nếu ca này ĐÃ CHỐT trước đó
                // - isReadOnly = false nếu ca này CHƯA CHỐT (nhân viên được nhập liệu & ký tên chốt ca bất kỳ lúc nào)
                var handoverReportWindow = new Employee.ShiftHandoverReport(
                    shiftDetail.OpenedByUser,
                    selectedBranch.Name,
                    selectedShift.Name,
                    workDate,
                    isReadOnly: shiftDetail.IsReadOnly,
                    branchId: selectedBranch.Id,
                    shiftCode: selectedShift.Code,
                    userId: 2
                );
                handoverReportWindow.Show();

                // Đóng cửa sổ đăng nhập
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối đến máy chủ: " + ex.Message, "Lỗi mạng", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExitApp_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn thoát ứng dụng không?", "Xác nhận thoát", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
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
                // Gửi lên API qua ApiService.Client
                HttpResponseMessage response = await ApiService.Client.PostAsJsonAsync("api/User/login", loginData);
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
