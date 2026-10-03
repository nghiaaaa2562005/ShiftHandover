using System;
using System.Windows;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Admin
{
    public partial class BranchEditDialog : Window
    {
        public BranchDTO? ResultBranch { get; private set; }
        private readonly int _branchId;

        public BranchEditDialog(BranchDTO? existingBranch = null)
        {
            InitializeComponent();

            if (existingBranch != null)
            {
                _branchId = existingBranch.Id;
                TxtDialogTitle.Text = "CẬP NHẬT THÔNG TIN CƠ SỞ";
                TxtDialogSubtitle.Text = $"Chỉnh sửa cấu hình cho cơ sở #{existingBranch.Id} - {existingBranch.Name}";
                TxtBranchName.Text = existingBranch.Name;
                TxtCashOpening.Text = existingBranch.DefaultCashOpening.ToString("N0");
                ChkIsActive.IsChecked = existingBranch.IsActive;
            }
            else
            {
                _branchId = 0;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtBranchName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Vui lòng nhập tên cơ sở bán hàng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtBranchName.Focus();
                return;
            }

            string cashText = (TxtCashOpening.Text ?? "0").Replace(",", "").Replace(".", "").Trim();
            if (!decimal.TryParse(cashText, out decimal cash) || cash < 0)
            {
                MessageBox.Show("Mức tiền két mặc định không hợp lệ!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCashOpening.Focus();
                return;
            }

            ResultBranch = new BranchDTO
            {
                Id = _branchId,
                Name = name,
                DefaultCashOpening = cash,
                IsActive = ChkIsActive.IsChecked == true
            };

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
