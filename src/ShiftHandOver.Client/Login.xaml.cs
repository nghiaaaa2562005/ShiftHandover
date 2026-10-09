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
                // Chỉ lấy danh sách cơ sở đang hoạt động (không hiển thị cơ sở đã bị khóa)
                var branches = await ApiService.Client.GetFromJsonAsync<System.Collections.Generic.List<BranchDTO>>("api/Branch?activeOnly=true");
                var activeBranches = branches?.FindAll(b => b.IsActive) ?? new System.Collections.Generic.List<BranchDTO>();
                cboBranch.ItemsSource = activeBranches;
                if (activeBranches.Count > 0)
                {
                    cboBranch.SelectedIndex = 0;
                }
                else
                {
                    cboBranch.SelectedIndex = -1;
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

                    // Tự động nhận diện và chọn ca làm việc tương ứng với khung giờ hiện tại
                    DateTime now = DateTime.Now;
                    string currentShiftCode = GetCurrentShiftCode(now);
                    var matchedShift = shifts.Find(s => s.Code.Equals(currentShiftCode, StringComparison.OrdinalIgnoreCase));
                    if (matchedShift != null)
                    {
                        cboShift.SelectedItem = matchedShift;
                    }
                    else
                    {
                        cboShift.SelectedIndex = 0;
                    }

                    // Nếu đang trong rạng sáng (00:00 – 02:30) của Ca Đêm, ngày làm việc là ngày hôm trước
                    if (now.TimeOfDay < new TimeSpan(2, 30, 0) && dpWorkDate != null)
                    {
                        dpWorkDate.SelectedDate = DateTime.Today.AddDays(-1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi tải ca: " + ex.Message);
            }
        }

        private static string GetCurrentShiftCode(DateTime now)
        {
            var t = now.TimeOfDay;
            if (t >= TimeSpan.FromHours(7) && t < TimeSpan.FromHours(12)) return "MORNING";
            if (t >= TimeSpan.FromHours(12) && t < TimeSpan.FromHours(18)) return "AFTERNOON";
            if (t >= TimeSpan.FromHours(18) && t < TimeSpan.FromHours(23)) return "EVENING";
            return "NIGHT";
        }

        private static string GetCurrentActiveShiftName(DateTime now)
        {
            var t = now.TimeOfDay;
            if (t >= TimeSpan.FromHours(7) && t < TimeSpan.FromHours(12)) return "Đang trong Ca Sáng (07:00 – 12:00)";
            if (t >= TimeSpan.FromHours(12) && t < TimeSpan.FromHours(18)) return "Đang trong Ca Chiều (12:00 – 18:00)";
            if (t >= TimeSpan.FromHours(18) && t < TimeSpan.FromHours(23)) return "Đang trong Ca Tối (18:00 – 23:00)";
            if (t >= TimeSpan.FromHours(23) || t < TimeSpan.FromHours(2.5)) return "Đang trong Ca Đêm (23:00 – 02:30)";
            return "Khung giờ nghỉ cửa hàng (02:30 – 07:00)";
        }

        private static DateTime GetShiftStartTime(DateTime workDate, string shiftCode)
        {
            return shiftCode.ToUpper().Trim() switch
            {
                "MORNING" => workDate.Date.AddHours(7),
                "AFTERNOON" => workDate.Date.AddHours(12),
                "EVENING" => workDate.Date.AddHours(18),
                "NIGHT" => workDate.Date.AddHours(23),
                _ => workDate.Date
            };
        }

        #region Chuyển đổi giữa Đăng nhập Nhân viên & Quản trị viên (Admin)
        private void BtnToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (pnlAdminLogin.Visibility == Visibility.Visible)
            {
                // Đang ở Admin -> Chuyển về Nhân viên
                pnlAdminLogin.Visibility = Visibility.Collapsed;
                pnlEmployeeLogin.Visibility = Visibility.Visible;
                if (btnToggleAdmin != null) btnToggleAdmin.Content = "Đăng nhập quyền Admin";
            }
            else
            {
                // Đang ở Nhân viên -> Chuyển sang Admin
                pnlEmployeeLogin.Visibility = Visibility.Collapsed;
                pnlAdminLogin.Visibility = Visibility.Visible;
                if (btnToggleAdmin != null) btnToggleAdmin.Content = "Đăng nhập quyền Admin";
            }
        }

        private void BtnBackToEmployee_Click(object sender, RoutedEventArgs e)
        {
            pnlAdminLogin.Visibility = Visibility.Collapsed;
            pnlEmployeeLogin.Visibility = Visibility.Visible;
            if (btnToggleAdmin != null) btnToggleAdmin.Content = "Đăng nhập quyền Admin";
        }
        #endregion

        #region Xử Lý Vào Ca Bàn Giao
        private bool _isEnteringShift = false;

        private async void BtnLoginEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (_isEnteringShift || !this.IsLoaded) return;
            _isEnteringShift = true;
            if (btnLoginEmployee != null) btnLoginEmployee.IsEnabled = false;

            try
            {
                var selectedBranch = cboBranch.SelectedItem as BranchDTO;
                if (selectedBranch == null)
                {
                    MessageBox.Show("Vui lòng chọn cơ sở làm việc!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!selectedBranch.IsActive)
                {
                    MessageBox.Show("Cơ sở này hiện đang tạm khóa hoặc ngừng hoạt động!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                string shiftCode = selectedShift.Code.ToUpper().Trim();
                DateTime now = DateTime.Now;
                DateTime shiftStart = GetShiftStartTime(workDate, shiftCode);

                // 1. Tuyệt đối không cho phép mở ca trong tương lai (theo ngày và giờ bắt đầu ca)
                if (now < shiftStart)
                {
                    string currentShiftDesc = GetCurrentActiveShiftName(now);
                    MessageBox.Show(
                        $"Không thể mở ca làm việc trong tương lai!\n\n" +
                        $"• Ca bạn chọn: {selectedShift.Name} ngày {workDate:dd/MM/yyyy} (bắt đầu lúc {shiftStart:HH:mm})\n" +
                        $"• Thời điểm hiện tại: {now:HH:mm} ({currentShiftDesc})\n\n" +
                        $"Theo quy định hệ thống: Chỉ có thể mở ca làm việc khi đã đến đúng khung giờ làm việc của ca đó.",
                        "Cảnh Báo Ca Tương Lai", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Chặn mở ca mới trong khung giờ nghỉ giữa ca (02:30 – 07:00 sáng)
                if (now.TimeOfDay >= new TimeSpan(2, 30, 0) && now.TimeOfDay < new TimeSpan(7, 0, 0))
                {
                    MessageBox.Show(
                        "Cửa hàng đang trong khung giờ đóng cửa nghỉ giữa ca (02:30 – 07:00 sáng).\n" +
                        "Hệ thống không cho phép mở ca làm việc mới vào thời điểm này!",
                        "Cửa Hàng Đóng Cửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Gọi Server kiểm tra / khởi tạo ca:
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

                    MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var shiftDetail = await response.Content.ReadFromJsonAsync<ShiftHandoverDetailDTO>();
                if (shiftDetail == null)
                {
                    MessageBox.Show("Không thể nạp dữ liệu chi tiết ca từ máy chủ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3. Kiểm tra xem ca làm việc có phải là ca MỚI VÀO (trạng thái NConfirm) hay không:
                bool isNewShift = (string.IsNullOrEmpty(shiftDetail.Status) || 
                                   string.Equals(shiftDetail.Status, "NConfirm", StringComparison.OrdinalIgnoreCase)) 
                                  && !shiftDetail.IsReadOnly;

                ShiftChannelSelection? channelSelection = null;

                if (isNewShift)
                {
                    // Lấy cấu hình các App POS và Ngân hàng do Admin đã thiết lập đối với cơ sở này
                    BranchHandoverConfigDTO? branchConfig = null;
                    try
                    {
                        branchConfig = await ApiService.Client.GetFromJsonAsync<BranchHandoverConfigDTO>($"api/Branch/{selectedBranch.Id}/handover-config");
                    }
                    catch { }

                    // Hiển thị màn hình Pop-up cho phép chọn App POS và Ngân hàng bán hàng
                    var channelDialog = new Employee.SelectSalesChannelsDialog(
                        selectedBranch.Name,
                        selectedShift.Name,
                        workDate,
                        shiftDetail,
                        branchConfig
                    );
                    if (this.IsLoaded)
                    {
                        try { channelDialog.Owner = this; } catch { }
                    }

                    bool? dialogResult = channelDialog.ShowDialog();
                    if (dialogResult != true || !channelDialog.Confirmed)
                    {
                        // Người dùng bấm Quay lại hoặc đóng cửa sổ pop-up, không vào biên bản
                        return;
                    }

                    channelSelection = channelDialog.ResultSelection;

                    // Lưu cố định cấu hình kênh đã chọn của ca lên Server Database
                    try
                    {
                        await ApiService.Client.PostAsJsonAsync($"api/Shift/{shiftDetail.ShiftId}/channels", channelSelection);
                    }
                    catch { }
                }

                // 5. Mở biên bản chốt ca:
                var handoverReportWindow = new Employee.ShiftHandoverReport(
                    shiftDetail.OpenedByUser,
                    selectedBranch.Name,
                    selectedShift.Name,
                    workDate,
                    isReadOnly: shiftDetail.IsReadOnly,
                    branchId: selectedBranch.Id,
                    shiftCode: selectedShift.Code,
                    userId: 2,
                    channelSelection: channelSelection
                );
                handoverReportWindow.Show();

                // Đóng cửa sổ đăng nhập
                this.Close();
            }
            catch (Exception ex)
            {
                if (this.IsLoaded)
                {
                    MessageBox.Show("Lỗi kết nối đến máy chủ: " + ex.Message, "Lỗi mạng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isEnteringShift = false;
                if (this.IsLoaded && btnLoginEmployee != null)
                {
                    btnLoginEmployee.IsEnabled = true;
                }
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
                    var adminDash = new Admin.AdminDashBoard(userDtoLog);
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
