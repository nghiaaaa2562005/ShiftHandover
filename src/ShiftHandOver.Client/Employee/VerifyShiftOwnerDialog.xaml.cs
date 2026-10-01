using System.Windows;

namespace ShiftHandOver.Client.Employee
{
    /// <summary>
    /// Interaction logic for VerifyShiftOwnerDialog.xaml
    /// Pop-up xác thực tài khoản & mật khẩu của 1 người phụ trách ca khi muốn sửa đổi ca đã chốt
    /// </summary>
    public partial class VerifyShiftOwnerDialog : Window
    {
        public string Username { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;

        public VerifyShiftOwnerDialog(string defaultUsername = "")
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(defaultUsername))
            {
                txtUsername.Text = defaultUsername;
                txtPassword.Focus();
            }
            else
            {
                txtUsername.Focus();
            }
        }

        private void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string u = txtUsername.Text.Trim();
            string p = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(u))
            {
                MessageBox.Show("Vui lòng nhập tên tài khoản của người phụ trách ca!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(p))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu xác nhận!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }

            Username = u;
            Password = p;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
