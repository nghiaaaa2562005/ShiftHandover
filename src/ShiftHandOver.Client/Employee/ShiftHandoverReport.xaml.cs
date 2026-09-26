using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShiftHandOver.Client.Employee
{
    public partial class ShiftHandoverReport : Window
    {
        private readonly CultureInfo _vnCulture = new CultureInfo("vi-VN");
        private decimal _currentGrandCashTotal = 2739000m;

        public ShiftHandoverReport()
        {
            InitializeComponent();
            AddExpenseRow("Tiền chi trong ca", "0");
            CalculateCashCount(null, null);
            CalculateAll(null, null);
        }

        public bool IsReadOnlyMode { get; private set; } = false;

        public ShiftHandoverReport(string employeeName, string branch, string shift, DateTime date, bool isReadOnly = false) : this()
        {
            if (txtHandoverUser != null && !string.IsNullOrWhiteSpace(employeeName))
            {
                txtHandoverUser.Text = employeeName;
            }

            if (lblShiftInfo != null)
            {
                lblShiftInfo.Text = $"BIÊN BẢN GIAO NHẬN VÀ CHỐT CA — {branch} | {shift} ({date:dd/MM/yyyy})";
            }

            if (isReadOnly)
            {
                SetReadOnlyMode();
            }
        }

        #region Quản lý Danh Sách Thu Chi Khác
        private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
        {
            AddExpenseRow("", "0");
        }

        private void AddExpenseRow(string description, string amount)
        {
            if (pnlExpenseItems == null) return;

            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            // 1. Ô nhập thông tin sản phẩm / lý do chi
            var txtDesc = new TextBox
            {
                Text = description,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12
            };
            Grid.SetColumn(txtDesc, 0);

            // 2. Ô nhập số tiền chi
            var txtAmt = new TextBox
            {
                Text = amount,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(6, 0, 6, 0),
                FontWeight = FontWeights.Bold,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12
            };
            txtAmt.TextChanged += (s, e) => CalculateAll(s, e);
            Grid.SetColumn(txtAmt, 1);

            // 3. Nút xóa khoản chi
            var btnDel = new Button
            {
                Content = "✕",
                Height = 30,
                Width = 26,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")),
                FontWeight = FontWeights.Black,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Xóa khoản chi này"
            };
            btnDel.Click += (s, e) =>
            {
                pnlExpenseItems.Children.Remove(rowGrid);
                CalculateAll(null, null);
            };
            Grid.SetColumn(btnDel, 2);

            if (IsReadOnlyMode)
            {
                txtDesc.IsReadOnly = true;
                txtAmt.IsReadOnly = true;
                btnDel.IsEnabled = false;
                btnDel.Visibility = Visibility.Collapsed;
            }

            rowGrid.Children.Add(txtDesc);
            rowGrid.Children.Add(txtAmt);
            rowGrid.Children.Add(btnDel);

            pnlExpenseItems.Children.Add(rowGrid);
            CalculateAll(null, null);
        }
        #endregion

        #region Helper Parse & Format
        private decimal ParseMoney(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string clean = text.Replace(".", "")
                               .Replace(",", "")
                               .Replace("đ", "")
                               .Replace(" ", "")
                               .Trim();
            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return 0;
        }

        private string FormatMoney(decimal amount)
        {
            return amount.ToString("N0", _vnCulture);
        }
        #endregion

        #region Popup Modal Tính Tiền Mặt Theo Mệnh Giá
        private void BtnOpenCashPopup_Click(object sender, RoutedEventArgs e)
        {
            popCashCounter.Visibility = Visibility.Visible;
        }

        private void BtnCloseCashPopup_Click(object sender, RoutedEventArgs e)
        {
            popCashCounter.Visibility = Visibility.Collapsed;
        }

        private void BtnApplyCashPopup_Click(object sender, RoutedEventArgs e)
        {
            // Điền số tiền đã kiểm đếm vào ô [4] Tiền mặt kết ca
            if (txtCashClosing != null)
            {
                txtCashClosing.Text = FormatMoney(_currentGrandCashTotal);
            }
            CalculateAll(null, null);
            popCashCounter.Visibility = Visibility.Collapsed;
        }

        private void CalculateCashCount(object? sender, TextChangedEventArgs? e)
        {
            if (!IsLoaded && sender != null) return;

            int c500 = int.TryParse(txtCount500k?.Text, out int v1) ? v1 : 0;
            int c200 = int.TryParse(txtCount200k?.Text, out int v2) ? v2 : 0;
            int c100 = int.TryParse(txtCount100k?.Text, out int v3) ? v3 : 0;
            int c50  = int.TryParse(txtCount50k?.Text,  out int v4) ? v4 : 0;
            int c20  = int.TryParse(txtCount20k?.Text,  out int v5) ? v5 : 0;
            int c10  = int.TryParse(txtCount10k?.Text,  out int v6) ? v6 : 0;
            int c5   = int.TryParse(txtCount5k?.Text,   out int v7) ? v7 : 0;
            int c2   = int.TryParse(txtCount2k?.Text,   out int v8) ? v8 : 0;
            int c1   = int.TryParse(txtCount1k?.Text,   out int v9) ? v9 : 0;

            decimal t500 = c500 * 500000m;
            decimal t200 = c200 * 200000m;
            decimal t100 = c100 * 100000m;
            decimal t50  = c50  * 50000m;
            decimal t20  = c20  * 20000m;
            decimal t10  = c10  * 10000m;
            decimal t5   = c5   * 5000m;
            decimal t2   = c2   * 2000m;
            decimal t1   = c1   * 1000m;

            if (lblTotal500k != null) lblTotal500k.Text = FormatMoney(t500) + " đ";
            if (lblTotal200k != null) lblTotal200k.Text = FormatMoney(t200) + " đ";
            if (lblTotal100k != null) lblTotal100k.Text = FormatMoney(t100) + " đ";
            if (lblTotal50k  != null) lblTotal50k.Text  = FormatMoney(t50)  + " đ";
            if (lblTotal20k  != null) lblTotal20k.Text  = FormatMoney(t20)  + " đ";
            if (lblTotal10k  != null) lblTotal10k.Text  = FormatMoney(t10)  + " đ";
            if (lblTotal5k   != null) lblTotal5k.Text   = FormatMoney(t5)   + " đ";
            if (lblTotal2k   != null) lblTotal2k.Text   = FormatMoney(t2)   + " đ";
            if (lblTotal1k   != null) lblTotal1k.Text   = FormatMoney(t1)   + " đ";

            _currentGrandCashTotal = t500 + t200 + t100 + t50 + t20 + t10 + t5 + t2 + t1;

            if (lblGrandTotalCash != null)
                lblGrandTotalCash.Text = FormatMoney(_currentGrandCashTotal) + " đ";
        }
        #endregion

        #region Tính Toán Toàn Bộ Biên Bản Giao Nhận Ca
        private void CalculateAll(object? sender, TextChangedEventArgs? e)
        {
            if (!IsLoaded && sender != null) return;

            // 1. SAPO POS: (Kết ca - Đầu ca) + Đêm
            decimal pos1Opening = ParseMoney(txtPos1Opening?.Text ?? "0");
            decimal pos1Closing = ParseMoney(txtPos1Closing?.Text ?? "0");
            decimal pos1Night   = ParseMoney(txtPos1Night?.Text ?? "0");
            decimal pos1Revenue = (pos1Closing - pos1Opening) + pos1Night;

            // 2. KIOT VIET: (Kết ca - Đầu ca) + Đêm
            decimal pos2Opening = ParseMoney(txtPos2Opening?.Text ?? "0");
            decimal pos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0");
            decimal pos2Night   = ParseMoney(txtPos2Night?.Text ?? "0");
            decimal pos2Revenue = (pos2Closing - pos2Opening) + pos2Night;

            // 3. TỔNG DOANH SỐ APP TRONG CA (DOANH THU)
            decimal totalPosRevenue = pos1Revenue + pos2Revenue;
            if (txtTotalPosRevenue != null) txtTotalPosRevenue.Text = FormatMoney(totalPosRevenue);

            // 4. CHUYỂN KHOẢN TINGTING: (Kết ca - Đầu ca) + Đêm
            decimal bank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
            decimal bank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0");
            decimal bank1Night   = ParseMoney(txtBank1Night?.Text ?? "0");
            decimal bank1Revenue = (bank1Closing - bank1Opening) + bank1Night;

            // 5. CHUYỂN KHOẢN ZALO PAY: (Kết ca - Đầu ca) + Đêm
            decimal bank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
            decimal bank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0");
            decimal bank2Night   = ParseMoney(txtBank2Night?.Text ?? "0");
            decimal bank2Revenue = (bank2Closing - bank2Opening) + bank2Night;

            // 6. TỔNG TIỀN CHUYỂN KHOẢN TRONG CA
            decimal totalBankRevenue = bank1Revenue + bank2Revenue;
            if (txtTotalBankRevenue != null) txtTotalBankRevenue.Text = FormatMoney(totalBankRevenue);

            // 7. TIỀN MẶT ĐẦU CA, TIỀN MẶT KẾT CA, THU CHI KHÁC
            decimal cashOpening = ParseMoney(txtCashOpening?.Text ?? "0");
            decimal cashClosing = ParseMoney(txtCashClosing?.Text ?? "0");

            // Tính tổng các khoản chi động từ danh sách
            decimal otherExpenses = 0;
            if (pnlExpenseItems != null)
            {
                foreach (UIElement child in pnlExpenseItems.Children)
                {
                    if (child is Grid g && g.Children.Count >= 2 && g.Children[1] is TextBox txtAmt)
                    {
                        otherExpenses += ParseMoney(txtAmt.Text);
                    }
                }
            }
            if (txtOtherExpenses != null)
            {
                txtOtherExpenses.Text = FormatMoney(otherExpenses);
            }

            if (txtSummaryCashClosing != null) txtSummaryCashClosing.Text = FormatMoney(cashClosing);

            // 8. CHÊNH LỆCH TIỀN MẶT
            decimal cashDifference = cashOpening + totalPosRevenue - cashClosing - otherExpenses - bank1Revenue - bank2Revenue;

            // Hiển thị âm/dương
            if (txtCashDifference != null)
            {
                txtCashDifference.Text = FormatMoney(cashDifference) + " đ";

                if (cashDifference < 0)
                {
                    // Âm tiền (Thiếu hụt) — Đen trắng
                    bdrCashDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                    txtCashDifference.Foreground = Brushes.Black;
                    txtCashDifference.BorderBrush = Brushes.Black;
                    lblCashDiffStatus.Text = $"THIẾU TIỀN: -{FormatMoney(Math.Abs(cashDifference))} đ";
                    lblCashDiffStatus.Foreground = Brushes.Black;
                }
                else if (cashDifference > 0)
                {
                    // Dương tiền (Dư thừa) — Đen trắng
                    bdrCashDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                    txtCashDifference.Foreground = Brushes.Black;
                    txtCashDifference.BorderBrush = Brushes.Black;
                    lblCashDiffStatus.Text = $"THỪA TIỀN: +{FormatMoney(cashDifference)} đ";
                    lblCashDiffStatus.Foreground = Brushes.Black;
                }
                else
                {
                    // Khớp đủ (0 đ) — Đen trắng
                    bdrCashDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                    txtCashDifference.Foreground = Brushes.Black;
                    txtCashDifference.BorderBrush = Brushes.Black;
                    lblCashDiffStatus.Text = "KHỚP ĐỦ 100%";
                    lblCashDiffStatus.Foreground = Brushes.Black;
                }
            }
        }
        #endregion

        #region Nút Thao Tác Chốt Ca & Làm Mới
        private void BtnSaveHandover_Click(object sender, RoutedEventArgs e)
        {
            string receiver = (cboReceiverUser.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string diff = txtCashDifference.Text;

            string message = $"Xác nhận chốt và bàn giao ca:\n\n" +
                             $"- Người giao: {txtHandoverUser.Text}\n" +
                             $"- Người nhận: {receiver}\n" +
                             $"- Tiền mặt kiểm đếm kết ca: {txtCashClosing.Text} đ\n" +
                             $"- Doanh số POS: {txtTotalPosRevenue.Text} đ\n" +
                             $"- Chuyển khoản ngân hàng: {txtTotalBankRevenue?.Text ?? "0"} đ\n" +
                             $"- Chênh lệch tiền mặt: {diff}\n\n" +
                             $"Sau khi bấm 'Đồng ý', số liệu ca này sẽ được KHÓA và chuyển làm số liệu đầu ca cho nhân viên tiếp theo!";

            var result = MessageBox.Show(message, "Xác nhận chốt ca — Plus Mart", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK)
            {
                MessageBox.Show("✅ Đã chốt ca và lưu dữ liệu bàn giao thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            txtCount500k.Text = "0";
            txtCount200k.Text = "0";
            txtCount100k.Text = "0";
            txtCount50k.Text  = "0";
            txtCount20k.Text  = "0";
            txtCount10k.Text  = "0";
            txtCount5k.Text   = "0";
            txtCount2k.Text   = "0";
            txtCount1k.Text   = "0";
            txtOtherExpenses.Text = "0";
            txtNote.Text = "";
            CalculateCashCount(null, null);
            CalculateAll(null, null);
        }

        private void BtnExitReport_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn thoát khỏi trang chốt ca không?\n\n⚠️ Lưu ý: Mọi số liệu chưa bấm Chốt ca sẽ không được lưu.", 
                                         "Xác nhận thoát", 
                                         MessageBoxButton.YesNo, 
                                         MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var loginWindow = new Login();
                loginWindow.Show();
                this.Close();
            }
        }
        #endregion

        #region Chế độ Chỉ Xem (Khóa toàn bộ nút thao tác khi xem ca quá khứ hoặc ca đã chốt)
        public void SetReadOnlyMode()
        {
            IsReadOnlyMode = true;

            if (bdrReadOnlyNotice != null)
                bdrReadOnlyNotice.Visibility = Visibility.Visible;

            // 1. Khóa các nút hành động
            if (btnSaveHandover != null)
            {
                btnSaveHandover.IsEnabled = false;
                btnSaveHandover.Content = "🔒 CA ĐÃ ĐÓNG / CHỈ XEM";
                btnSaveHandover.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            }

            if (btnAddExpense != null)
            {
                btnAddExpense.IsEnabled = false;
                btnAddExpense.Visibility = Visibility.Collapsed;
            }

            if (btnOpenCashPopup != null)
            {
                btnOpenCashPopup.IsEnabled = false;
            }

            if (btnApplyCashPopup != null)
            {
                btnApplyCashPopup.IsEnabled = false;
            }

            if (cboReceiverUser != null)
            {
                cboReceiverUser.IsEnabled = false;
            }

            // 2. Khóa tất cả các ô nhập liệu
            if (txtPos1Closing != null) txtPos1Closing.IsReadOnly = true;
            if (txtPos1Night != null) txtPos1Night.IsReadOnly = true;
            if (txtPos2Closing != null) txtPos2Closing.IsReadOnly = true;
            if (txtPos2Night != null) txtPos2Night.IsReadOnly = true;
            if (txtBank1Closing != null) txtBank1Closing.IsReadOnly = true;
            if (txtBank1Night != null) txtBank1Night.IsReadOnly = true;
            if (txtBank2Closing != null) txtBank2Closing.IsReadOnly = true;
            if (txtBank2Night != null) txtBank2Night.IsReadOnly = true;
            if (txtCashClosing != null) txtCashClosing.IsReadOnly = true;
            if (txtNote != null) txtNote.IsReadOnly = true;

            // 3. Khóa ô đếm tiền trong modal kiểm đếm
            if (txtCount500k != null) txtCount500k.IsReadOnly = true;
            if (txtCount200k != null) txtCount200k.IsReadOnly = true;
            if (txtCount100k != null) txtCount100k.IsReadOnly = true;
            if (txtCount50k != null) txtCount50k.IsReadOnly = true;
            if (txtCount20k != null) txtCount20k.IsReadOnly = true;
            if (txtCount10k != null) txtCount10k.IsReadOnly = true;
            if (txtCount5k != null) txtCount5k.IsReadOnly = true;
            if (txtCount2k != null) txtCount2k.IsReadOnly = true;
            if (txtCount1k != null) txtCount1k.IsReadOnly = true;

            // 4. Khóa các dòng chi phí đã thêm trong danh sách
            if (pnlExpenseItems != null)
            {
                foreach (UIElement child in pnlExpenseItems.Children)
                {
                    if (child is Grid g)
                    {
                        foreach (UIElement elem in g.Children)
                        {
                            if (elem is TextBox tb) tb.IsReadOnly = true;
                            if (elem is Button btn)
                            {
                                btn.IsEnabled = false;
                                btn.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }
}
