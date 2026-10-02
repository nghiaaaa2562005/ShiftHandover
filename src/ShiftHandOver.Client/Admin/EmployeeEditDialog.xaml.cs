using System;
using System.Windows;
using System.Windows.Controls;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Admin
{
    public partial class EmployeeEditDialog : Window
    {
        public bool IsEditMode { get; private set; }
        public UserDTO? ResultUser { get; private set; }
        private readonly UserDTO? _originalUser;

        public EmployeeEditDialog(UserDTO? existingUser = null)
        {
            InitializeComponent();
            _originalUser = existingUser;
            IsEditMode = (existingUser != null);

            if (IsEditMode && existingUser != null)
            {
                TxtDialogTitle.Text = "CẬP NHẬT THÔNG TIN NHÂN VIÊN";
                TxtDialogSubtitle.Text = $"Chỉnh sửa tài khoản và quyền hạn của {existingUser.FullName}";
                BtnSave.Content = "Cập nhật";

                TxtFullName.Text = existingUser.FullName;
                TxtUsername.Text = existingUser.Username;
                TxtPasswordHint.Visibility = Visibility.Visible;
                LblPassword.Text = "Đổi mật khẩu mới (Tùy chọn):";

                // Chọn Role
                foreach (ComboBoxItem item in CbRole.Items)
                {
                    if (item.Tag?.ToString()?.Equals(existingUser.Role, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }

                ChkIsActive.IsChecked = existingUser.IsActive;
            }
            else
            {
                TxtDialogTitle.Text = "THÊM NHÂN VIÊN MỚI";
                TxtDialogSubtitle.Text = "Nhập thông tin nhân sự để phân quyền trực ca và bán hàng";
                BtnSave.Content = "Lưu thông tin";
                TxtPasswordHint.Visibility = Visibility.Collapsed;
                LblPassword.Text = "Mật khẩu đăng nhập:";
                ChkIsActive.IsChecked = true;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string fullName = TxtFullName.Text.Trim();
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Vui lòng nhập Họ và tên nhân viên!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFullName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Vui lòng nhập Tên tài khoản (Username)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUsername.Focus();
                return;
            }

            if (!IsEditMode && string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Vui lòng nhập Mật khẩu đăng nhập cho nhân viên mới!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPassword.Focus();
                return;
            }

            string selectedRole = "Employee";
            if (CbRole.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                selectedRole = selectedItem.Tag.ToString() ?? "Employee";
            }

            bool isActive = ChkIsActive.IsChecked ?? true;

            if (IsEditMode && _originalUser != null)
            {
                ResultUser = new UserDTO
                {
                    Id = _originalUser.Id,
                    FullName = fullName,
                    Username = username,
                    PasswordHash = !string.IsNullOrWhiteSpace(password) ? password : _originalUser.PasswordHash,
                    Role = selectedRole,
                    ShiftCount = _originalUser.ShiftCount,
                    IsActive = isActive,
                    CreatedAt = _originalUser.CreatedAt
                };
            }
            else
            {
                ResultUser = new UserDTO
                {
                    Id = 0,
                    FullName = fullName,
                    Username = username,
                    PasswordHash = password,
                    Role = selectedRole,
                    ShiftCount = 0,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                };
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
