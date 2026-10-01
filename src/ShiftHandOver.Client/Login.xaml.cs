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
        private void BtnLoginEmployee_Click(object sender, RoutedEventArgs e)
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
            TimeSpan nowTime = DateTime.Now.TimeOfDay;
            string shiftCode = selectedShift.Code.ToUpper().Trim();

            // ==============================================================
            // 1. CHECK NGÀY: Tuyệt đối không tạo ca tương lai
            // ==============================================================
            if (workDate > today)
            {
                MessageBox.Show("Không tạo được ca tương lai.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ==============================================================
            // 2. CHECK CA QUÁ KHỨ (Ngày đã qua) -> KHÓA HẾT NÚT, CHỈ XEM
            // ==============================================================
            if (workDate < today)
            {
                // Ngoại lệ: Nếu hiện tại từ 23:00 đến 02:30 sáng, ca đêm hôm qua vẫn đang diễn ra
                if (nowTime < new TimeSpan(2, 30, 0) && workDate == today.AddDays(-1) && shiftCode == "NIGHT")
                {
                    // Ca đêm đang chạy bình thường
                    var currentNightWindow = new Employee.ShiftHandoverReport("Nguyễn Văn Duy", selectedBranch.Name, selectedShift.Name, workDate, isReadOnly: false, branchId: selectedBranch.Id, shiftCode: selectedShift.Code, userId: 2);
                    currentNightWindow.Show();
                    this.Close();
                    return;
                }

                // Tra cứu các ca trong quá khứ -> Khóa hết nút ấn, chỉ cho phép xem
                var pastWindow = new Employee.ShiftHandoverReport("Nguyễn Văn Duy", selectedBranch.Name, selectedShift.Name, workDate, isReadOnly: true, branchId: selectedBranch.Id, shiftCode: selectedShift.Code, userId: 2);
                pastWindow.Show();
                this.Close();
                return;
            }

            // ==============================================================
            // 3. CHECK GIỜ KHI CHỌN NGÀY HÔM NAY (workDate == today)
            // Khung giờ:
            //   - Sáng: 07:00 - 12:00
            //   - Chiều: 12:00 - 18:00
            //   - Tối:   18:00 - 23:00
            //   - Đêm:   23:00 - 02:30 hôm sau
            // Quy tắc:
            //   - Ca sáng: không mở chiều, tối, đêm
            //   - Ca chiều: không mở tối, đêm (ca sáng đã qua -> chỉ xem)
            //   - Ca tối: không mở đêm (ca sáng, chiều đã qua -> chỉ xem)
            //   - Ca đêm: không mở ca sáng hôm sau
            // ==============================================================
            bool isReadOnly = false;

            // Khung 1: 00:00 - 02:30 (Đang là ca đêm của ngày hôm qua)
            if (nowTime < new TimeSpan(2, 30, 0))
            {
                // Đang trong ca đêm, không thể mở bất kỳ ca nào của ngày hôm nay
                MessageBox.Show("Hiện tại đang là ca đêm (23:00 - 02:30), không thể mở ca sáng của ngày hôm sau!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Khung 2: 02:30 - 07:00 (Nghỉ giữa ca)
            else if (nowTime < new TimeSpan(7, 0, 0))
            {
                MessageBox.Show("Ca sáng bắt đầu từ 07:00, hiện tại chưa đến giờ mở ca!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Khung 3: 07:00 - 12:00 (Đang là Ca Sáng)
            else if (nowTime >= new TimeSpan(7, 0, 0) && nowTime < new TimeSpan(12, 0, 0))
            {
                if (shiftCode == "AFTERNOON" || shiftCode == "EVENING" || shiftCode == "NIGHT")
                {
                    MessageBox.Show("Hiện tại đang trong ca sáng (07:00 - 12:00), không thể mở ca chiều, ca tối hoặc ca đêm!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                isReadOnly = false; // Ca sáng mở bình thường
            }
            // Khung 4: 12:00 - 18:00 (Đang là Ca Chiều)
            else if (nowTime >= new TimeSpan(12, 0, 0) && nowTime < new TimeSpan(18, 0, 0))
            {
                if (shiftCode == "EVENING" || shiftCode == "NIGHT")
                {
                    MessageBox.Show("Hiện tại đang trong ca chiều (12:00 - 18:00), không thể mở ca tối hoặc ca đêm!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (shiftCode == "MORNING")
                {
                    isReadOnly = true; // Ca sáng hôm nay đã kết thúc -> chỉ xem
                }
                else
                {
                    isReadOnly = false; // Ca chiều mở bình thường
                }
            }
            // Khung 5: 18:00 - 23:00 (Đang là Ca Tối)
            else if (nowTime >= new TimeSpan(18, 0, 0) && nowTime < new TimeSpan(23, 0, 0))
            {
                if (shiftCode == "NIGHT")
                {
                    MessageBox.Show("Hiện tại đang trong ca tối (18:00 - 23:00), không thể mở ca đêm!", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (shiftCode == "MORNING" || shiftCode == "AFTERNOON")
                {
                    isReadOnly = true; // Ca sáng, chiều đã kết thúc -> chỉ xem
                }
                else
                {
                    isReadOnly = false; // Ca tối mở bình thường
                }
            }
            // Khung 6: 23:00 - 23:59:59 (Đang là Ca Đêm của ngày hôm nay)
            else
            {
                if (shiftCode == "NIGHT")
                {
                    isReadOnly = false; // Ca đêm mở bình thường
                }
                else
                {
                    isReadOnly = true; // Các ca sáng, chiều, tối trước đó -> chỉ xem
                }
            }

            // ==============================================================
            // 4. MỞ BIÊN BẢN CHỐT CA VỚI CHẾ ĐỘ PHÙ HỢP
            // ==============================================================
            var handoverReportWindow = new Employee.ShiftHandoverReport("Nguyễn Văn Duy", selectedBranch.Name, selectedShift.Name, workDate, isReadOnly, branchId: selectedBranch.Id, shiftCode: selectedShift.Code, userId: 2);
            handoverReportWindow.Show();

            // Đóng cửa sổ đăng nhập
            this.Close();
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
