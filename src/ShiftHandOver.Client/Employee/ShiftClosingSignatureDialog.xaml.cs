using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Employee
{
    /// <summary>
    /// Interaction logic for ShiftClosingSignatureDialog.xaml
    /// Pop-up ký chữ ký điện tử xác nhận chốt ca cho 1 hoặc nhiều nhân viên
    /// </summary>
    public partial class ShiftClosingSignatureDialog : Window
    {
        private readonly List<EmployeeSignRow> _rows = new List<EmployeeSignRow>();

        public List<ShiftEmployeeSignDTO> Signatures { get; private set; } = new List<ShiftEmployeeSignDTO>();

        public ShiftClosingSignatureDialog(string defaultUsername, string cashClosing, string totalPos, string totalBank, string cashDiff)
        {
            InitializeComponent();

            lblCashClosing.Text = cashClosing;
            lblTotalPos.Text = totalPos;
            lblTotalBank.Text = totalBank;
            lblCashDiff.Text = cashDiff;

            // Thêm nhân viên đầu tiên (không cho xóa)
            AddEmployeeRow(defaultUsername, canRemove: false);
        }

        private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count >= 4)
            {
                MessageBox.Show("Mỗi ca trực hỗ trợ tối đa 4 nhân viên ký nhận cùng lúc!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddEmployeeRow("", canRemove: true);
        }

        private void AddEmployeeRow(string initialUsername, bool canRemove)
        {
            int index = _rows.Count + 1;

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            if (canRemove)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            }

            // Cột 0: Nhãn nhân viên
            var lblTitle = new TextBlock
            {
                Text = $"Nhân viên {index}:",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black
            };
            Grid.SetColumn(lblTitle, 0);
            grid.Children.Add(lblTitle);

            // Cột 1: Username
            var pnlUser = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            var lblU = new TextBlock { Text = "Tài khoản (Username):", FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 0, 0, 2) };
            var txtUser = new TextBox
            {
                Text = initialUsername,
                Height = 30,
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1)
            };
            pnlUser.Children.Add(lblU);
            pnlUser.Children.Add(txtUser);
            Grid.SetColumn(pnlUser, 1);
            grid.Children.Add(pnlUser);

            // Cột 2: Password
            var pnlPass = new StackPanel { Margin = new Thickness(0, 0, canRemove ? 6 : 0, 0) };
            var lblP = new TextBlock { Text = "Mật khẩu xác nhận:", FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 0, 0, 2) };
            var txtPass = new PasswordBox
            {
                Height = 30,
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1)
            };
            pnlPass.Children.Add(lblP);
            pnlPass.Children.Add(txtPass);
            Grid.SetColumn(pnlPass, 2);
            grid.Children.Add(pnlPass);

            // Cột 3: Nút Xóa (nếu là nhân viên thứ 2 trở lên)
            Button? btnRemove = null;
            if (canRemove)
            {
                btnRemove = new Button
                {
                    Content = "Xóa",
                    Width = 32,
                    Height = 28,
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Xóa nhân viên này"
                };
                Grid.SetColumn(btnRemove, 3);
                grid.Children.Add(btnRemove);
            }

            border.Child = grid;
            pnlSignatures.Children.Add(border);

            var rowItem = new EmployeeSignRow
            {
                Container = border,
                TxtUsername = txtUser,
                TxtPassword = txtPass
            };

            if (btnRemove != null)
            {
                btnRemove.Click += (s, e) =>
                {
                    pnlSignatures.Children.Remove(border);
                    _rows.Remove(rowItem);
                    ReindexRows();
                };
            }

            _rows.Add(rowItem);
        }

        private void ReindexRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Container.Child is Grid g && g.Children.Count > 0 && g.Children[0] is TextBlock tb)
                {
                    tb.Text = $"Nhân viên {i + 1}:";
                }
            }
        }

        private void BtnConfirmClose_Click(object sender, RoutedEventArgs e)
        {
            Signatures.Clear();
            var setUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _rows.Count; i++)
            {
                string u = _rows[i].TxtUsername.Text.Trim();
                string p = _rows[i].TxtPassword.Password;

                if (string.IsNullOrWhiteSpace(u))
                {
                    MessageBox.Show($"Vui lòng nhập Tên tài khoản của Nhân viên {i + 1}!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _rows[i].TxtUsername.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(p))
                {
                    MessageBox.Show($"Vui lòng nhập Mật khẩu của Nhân viên {i + 1} ({u})!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _rows[i].TxtPassword.Focus();
                    return;
                }

                if (!setUsers.Add(u))
                {
                    MessageBox.Show($"Tài khoản '{u}' bị nhập trùng lặp! Mỗi nhân viên chỉ ký một lần.", "Lỗi trùng lặp", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Signatures.Add(new ShiftEmployeeSignDTO
                {
                    Username = u,
                    Password = p
                });
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class EmployeeSignRow
        {
            public Border Container { get; set; } = null!;
            public TextBox TxtUsername { get; set; } = null!;
            public PasswordBox TxtPassword { get; set; } = null!;
        }
    }
}
