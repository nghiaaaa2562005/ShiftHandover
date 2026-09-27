using System;
using System.Globalization;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShiftHandOver.Client.Services;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Employee
{
    public partial class ShiftHandoverReport : Window
    {
        private readonly CultureInfo _vnCulture = new CultureInfo("vi-VN");
        private decimal _currentGrandCashTotal = 2739000m;

        public const string StatusNConfirm = "NConfirm";
        public const string StatusChanged = "Changed";
        public const string StatusConfirmStart = "ConfirmStart";
        public const string StatusClosed = "Closed";

        public string CurrentShiftStatus { get; private set; } = StatusNConfirm;
        public bool IsReadOnlyMode => CurrentShiftStatus == StatusClosed;

        private System.Windows.Threading.DispatcherTimer? _editCountdownTimer;
        private int _secondsRemaining = 120; // 2 phút đếm ngược

        private int _currentShiftId = 0;
        private int _branchId = 1;
        private string _shiftCode = "MORNING";
        private DateTime _workDate = DateTime.Today;
        private int _userId = 2;

        // Lưu vết số liệu ban đầu để kiểm tra phần nào đã bị thay đổi
        private decimal _originalCashOpening = 0;
        private decimal _originalPos1Opening = 0;
        private decimal _originalPos2Opening = 0;
        private decimal _originalBank1Opening = 0;
        private decimal _originalBank2Opening = 0;
        // Ghi chú hệ thống ẩn — nhân viên không nhìn thấy trên giao diện, không thể sửa đổi
        private string _hiddenAuditChangeLog = "";

        public ShiftHandoverReport()
        {
            InitializeComponent();
            AddExpenseRow("Tiền chi trong ca", "0");
            CalculateCashCount(null, null);
            CalculateAll(null, null);

            // Mặc định ban đầu vào ca là NConfirm
            ApplyShiftStatus(StatusNConfirm);
        }

        public ShiftHandoverReport(string employeeName, string branch, string shift, DateTime date, bool isReadOnly = false, 
                                   int branchId = 1, string shiftCode = "MORNING", int userId = 2) : this()
        {
            _branchId = branchId;
            _shiftCode = shiftCode;
            _workDate = date;
            _userId = userId;

            if (txtHandoverUser != null && !string.IsNullOrWhiteSpace(employeeName))
            {
                txtHandoverUser.Text = employeeName;
            }

            if (lblShiftInfo != null)
            {
                lblShiftInfo.Text = $"BIÊN BẢN GIAO NHẬN VÀ CHỐT CA — {branch} | {shift} ({date:dd/MM/yyyy})";
            }

            // Gọi API nạp dữ liệu từ Cơ sở dữ liệu
            Loaded += async (s, e) =>
            {
                await LoadShiftFromApiAsync(isReadOnly);
            };
        }

        /// <summary>
        /// Tải dữ liệu ca làm việc từ API Server / Database:
        /// - Tự động kế thừa số liệu đầu ca từ ca trước
        /// - Trạng thái ban đầu: NConfirm (khóa toàn bộ nhập liệu)
        /// </summary>
        private async Task LoadShiftFromApiAsync(bool isReadOnly)
        {
            try
            {
                var req = new InitShiftRequestDTO
                {
                    BranchId = _branchId,
                    ShiftDate = _workDate,
                    ShiftType = _shiftCode,
                    UserId = _userId
                };

                var response = await ApiService.Client.PostAsJsonAsync("api/Shift/init", req);
                if (response.IsSuccessStatusCode)
                {
                    var shiftDetail = await response.Content.ReadFromJsonAsync<ShiftHandoverDetailDTO>();
                    if (shiftDetail != null)
                    {
                        _currentShiftId = shiftDetail.ShiftId;

                        // 1. Điền các thông tin đầu ca (cố định, lấy từ DB / ca trước)
                        if (txtPos1Opening != null) txtPos1Opening.Text = FormatMoney(shiftDetail.Pos1Opening);
                        if (txtPos2Opening != null) txtPos2Opening.Text = FormatMoney(shiftDetail.Pos2Opening);
                        if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(shiftDetail.Bank1Opening);
                        if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(shiftDetail.Bank2Opening);
                        if (txtCashOpening != null) txtCashOpening.Text = FormatMoney(shiftDetail.CashOpening);

                        // Lưu lại số liệu gốc ban đầu
                        _originalPos1Opening = shiftDetail.Pos1Opening;
                        _originalPos2Opening = shiftDetail.Pos2Opening;
                        _originalBank1Opening = shiftDetail.Bank1Opening;
                        _originalBank2Opening = shiftDetail.Bank2Opening;
                        _originalCashOpening = shiftDetail.CashOpening;

                        // 2. Điền các thông tin cuối ca nếu đã có dữ liệu trước đó
                        if (shiftDetail.Pos1Closing.HasValue && txtPos1Closing != null)
                            txtPos1Closing.Text = FormatMoney(shiftDetail.Pos1Closing.Value);
                        if (shiftDetail.Pos1Night.HasValue && txtPos1Night != null)
                            txtPos1Night.Text = FormatMoney(shiftDetail.Pos1Night.Value);

                        if (shiftDetail.Pos2Closing.HasValue && txtPos2Closing != null)
                            txtPos2Closing.Text = FormatMoney(shiftDetail.Pos2Closing.Value);
                        if (shiftDetail.Pos2Night.HasValue && txtPos2Night != null)
                            txtPos2Night.Text = FormatMoney(shiftDetail.Pos2Night.Value);

                        if (shiftDetail.Bank1Closing.HasValue && txtBank1Closing != null)
                            txtBank1Closing.Text = FormatMoney(shiftDetail.Bank1Closing.Value);
                        if (shiftDetail.Bank1Night.HasValue && txtBank1Night != null)
                            txtBank1Night.Text = FormatMoney(shiftDetail.Bank1Night.Value);

                        if (shiftDetail.Bank2Closing.HasValue && txtBank2Closing != null)
                            txtBank2Closing.Text = FormatMoney(shiftDetail.Bank2Closing.Value);
                        if (shiftDetail.Bank2Night.HasValue && txtBank2Night != null)
                            txtBank2Night.Text = FormatMoney(shiftDetail.Bank2Night.Value);

                        if (shiftDetail.CashClosing.HasValue && txtCashClosing != null)
                            txtCashClosing.Text = FormatMoney(shiftDetail.CashClosing.Value);

                        if (!string.IsNullOrEmpty(shiftDetail.Note))
                        {
                            string rawNote = shiftDetail.Note;
                            int idxUserNote = rawNote.IndexOf("Ghi chú nhân viên:");
                            if (idxUserNote >= 0)
                            {
                                _hiddenAuditChangeLog = rawNote.Substring(0, idxUserNote).Trim();
                                if (txtNote != null)
                                    txtNote.Text = rawNote.Substring(idxUserNote + "Ghi chú nhân viên:".Length).Trim();
                            }
                            else if (rawNote.StartsWith("Nhân viên đã thay đổi") || rawNote.Contains("Đã sửa đầu ca"))
                            {
                                _hiddenAuditChangeLog = rawNote.Trim();
                                if (txtNote != null) txtNote.Text = ""; // Ẩn hoàn toàn khỏi ô ghi chú của nhân viên
                            }
                            else
                            {
                                if (txtNote != null) txtNote.Text = rawNote;
                            }
                        }

                        // Điền các khoản chi nếu có
                        if (shiftDetail.Expenses != null && shiftDetail.Expenses.Count > 0 && pnlExpenseItems != null)
                        {
                            pnlExpenseItems.Children.Clear();
                            foreach (var exp in shiftDetail.Expenses)
                            {
                                AddExpenseRow(exp.Description, FormatMoney(exp.Amount));
                            }
                        }

                        CalculateAll(null, null);

                        // 3. Áp dụng trạng thái ca
                        if (isReadOnly || shiftDetail.IsReadOnly)
                        {
                            ApplyShiftStatus(StatusClosed);
                        }
                        else
                        {
                            ApplyShiftStatus(shiftDetail.Status);
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi nạp dữ liệu từ API Shift/init: " + ex.Message);
            }

            if (isReadOnly)
            {
                ApplyShiftStatus(StatusClosed);
            }
            else
            {
                ApplyShiftStatus(StatusNConfirm);
            }
        }

        /// <summary>
        /// Điền số liệu đầu ca được kế thừa từ cuối ca gần nhất (cố định, không thể sửa)
        /// </summary>
        public void SetOpeningData(decimal pos1Opening, decimal pos2Opening, decimal bank1Opening, decimal bank2Opening, decimal cashOpening)
        {
            if (txtPos1Opening != null) txtPos1Opening.Text = FormatMoney(pos1Opening);
            if (txtPos2Opening != null) txtPos2Opening.Text = FormatMoney(pos2Opening);
            if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(bank1Opening);
            if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(bank2Opening);
            if (txtCashOpening != null) txtCashOpening.Text = FormatMoney(cashOpening);

            CalculateAll(null, null);
        }

        #region Quản lý Danh Sách Thu Chi Khác
        private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShiftStatus == StatusNConfirm)
            {
                MessageBox.Show("Vui lòng nhấn 'Xác nhận dữ liệu đầu ca' trước khi thêm khoản chi trong ca!", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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

            if (IsReadOnlyMode || CurrentShiftStatus == StatusNConfirm)
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
            if (CurrentShiftStatus == StatusNConfirm)
            {
                MessageBox.Show("Vui lòng nhấn 'Xác nhận dữ liệu đầu ca' trước khi kiểm đếm tiền mặt cuối ca!", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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

        #region Nút Thao Tác Chốt Ca & Xác Nhận Đầu Ca
        private void BtnChangeInitialData_Click(object sender, RoutedEventArgs e)
        {
            // Lưu lại thông số gốc ban đầu nếu chưa có
            if (_originalPos1Opening == 0 && _originalCashOpening == 0)
            {
                _originalCashOpening = ParseMoney(txtCashOpening?.Text ?? "0");
                _originalPos1Opening = ParseMoney(txtPos1Opening?.Text ?? "0");
                _originalPos2Opening = ParseMoney(txtPos2Opening?.Text ?? "0");
                _originalBank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
                _originalBank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
            }

            // Hiển thị 2 thông báo theo đúng yêu cầu:
            string warningMsg = "📸 Chụp và gửi lại các bằng chứng thông số sai gửi cho anh Hùng !\n\n" +
                                "⏳ Nhân viên có 2 phút để thay đổi thông tin đầu ca.";

            MessageBox.Show(warningMsg, "Yêu cầu thay đổi thông tin đầu ca — Plus Mart", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);

            // Chuyển sang trạng thái Changed để mở khóa sửa đầu ca và đếm ngược 2 phút
            ApplyShiftStatus(StatusChanged);
        }

        private async void BtnSaveHandover_Click(object sender, RoutedEventArgs e)
        {
            // =========================================================================
            // GIAI ĐOẠN 0: Changed -> Nhân viên bấm "XÁC NHẬN THAY ĐỔI" sau khi chỉnh sửa
            // =========================================================================
            if (CurrentShiftStatus == StatusChanged)
            {
                StopCountdownTimer();

                decimal newCash  = ParseMoney(txtCashOpening?.Text ?? "0");
                decimal newPos1  = ParseMoney(txtPos1Opening?.Text ?? "0");
                decimal newPos2  = ParseMoney(txtPos2Opening?.Text ?? "0");
                decimal newBank1 = ParseMoney(txtBank1Opening?.Text ?? "0");
                decimal newBank2 = ParseMoney(txtBank2Opening?.Text ?? "0");

                // Phát hiện chính xác phần nào đã bị thay đổi
                var changedDetails = new List<string>();
                if (newPos1 != _originalPos1Opening)
                    changedDetails.Add($"Sapo POS ({FormatMoney(_originalPos1Opening)} đ -> {FormatMoney(newPos1)} đ)");
                if (newPos2 != _originalPos2Opening)
                    changedDetails.Add($"KiotViet ({FormatMoney(_originalPos2Opening)} đ -> {FormatMoney(newPos2)} đ)");
                if (newBank1 != _originalBank1Opening)
                    changedDetails.Add($"TingTing ({FormatMoney(_originalBank1Opening)} đ -> {FormatMoney(newBank1)} đ)");
                if (newBank2 != _originalBank2Opening)
                    changedDetails.Add($"Zalo Pay ({FormatMoney(_originalBank2Opening)} đ -> {FormatMoney(newBank2)} đ)");
                if (newCash != _originalCashOpening)
                    changedDetails.Add($"Tiền mặt ({FormatMoney(_originalCashOpening)} đ -> {FormatMoney(newCash)} đ)");

                if (changedDetails.Count > 0)
                {
                    _hiddenAuditChangeLog = $"Nhân viên đã thay đổi : {string.Join(", ", changedDetails)}.";
                }

                // TUYỆT ĐỐI KHÔNG GHI VÀO txtNote.Text: Nhân viên không nhìn thấy và không thể đổi được!

                try
                {
                    var changeReq = new ChangeInitialDataRequestDTO
                    {
                        ShiftId = _currentShiftId,
                        UserId = _userId,
                        CashOpening = newCash,
                        Pos1Opening = newPos1,
                        Pos2Opening = newPos2,
                        Bank1Opening = newBank1,
                        Bank2Opening = newBank2,
                        Note = _hiddenAuditChangeLog
                    };

                    var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/confirm-change", changeReq);
                    if (apiRes.IsSuccessStatusCode)
                    {
                        ApplyShiftStatus(StatusConfirmStart);
                        MessageBox.Show("✅ Đã xác nhận thay đổi thông tin đầu ca và lưu vào Database thành công!\n" +
                                        "Các thông tin đầu ca đã được KHÓA LẠI. Ca làm việc chính thức bắt đầu.", 
                                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi API ConfirmChange: " + ex.Message);
                }

                // Fallback giao diện nếu không có mạng
                ApplyShiftStatus(StatusConfirmStart);
                MessageBox.Show("✅ Đã xác nhận thay đổi thông tin đầu ca thành công!\n" +
                                "Các thông tin đầu ca đã được KHÓA LẠI. Ca làm việc chính thức bắt đầu.", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // =========================================================================
            // GIAI ĐOẠN 1: NConfirm -> Nhân viên bấm xác nhận nhận ca và số liệu đầu ca
            // =========================================================================
            if (CurrentShiftStatus == StatusNConfirm)
            {
                string confirmMsg = "XÁC NHẬN SỐ LIỆU BÀN GIAO ĐẦU CA:\n\n" +
                                    $"• Tiền mặt đầu ca: {txtCashOpening?.Text ?? "0"} đ\n" +
                                    $"• Sapo POS đầu ca: {txtPos1Opening?.Text ?? "0"} đ\n" +
                                    $"• KiotViet đầu ca: {txtPos2Opening?.Text ?? "0"} đ\n" +
                                    $"• TingTing đầu ca: {txtBank1Opening?.Text ?? "0"} đ\n" +
                                    $"• Zalo Pay đầu ca: {txtBank2Opening?.Text ?? "0"} đ\n\n" +
                                    "Bạn đã kiểm đếm và xác nhận khớp số liệu bàn giao từ ca trước chứ?\n" +
                                    "📌 Sau khi bấm 'Đồng ý', hệ thống sẽ ghi nhận trạng thái 'ConfirmStart' vào Database và mở khóa các ô nhập liệu.";

                var confirmResult = MessageBox.Show(confirmMsg, "Xác nhận dữ liệu đầu ca — Plus Mart", 
                                                    MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirmResult == MessageBoxResult.OK)
                {
                    try
                    {
                        var confirmReq = new ConfirmStartRequestDTO
                        {
                            ShiftId = _currentShiftId,
                            UserId = _userId
                        };
                        var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/confirm-start", confirmReq);
                        if (apiRes.IsSuccessStatusCode)
                        {
                            ApplyShiftStatus(StatusConfirmStart);
                            MessageBox.Show("✅ Đã xác nhận dữ liệu đầu ca thành công lên Database!\nCác ô nhập liệu đã được mở khóa để bạn làm việc trong ca.", 
                                            "Thông báo nhận ca", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Lỗi API ConfirmStart: " + ex.Message);
                    }

                    // Fallback giao diện
                    ApplyShiftStatus(StatusConfirmStart);
                    MessageBox.Show("✅ Đã xác nhận dữ liệu đầu ca thành công!\nCác ô nhập liệu đã được mở khóa để bạn làm việc trong ca.", 
                                    "Thông báo nhận ca", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            // =========================================================================
            // GIAI ĐOẠN 2: ConfirmStart -> Nhân viên bấm Hoàn tất & Chốt ca khi hết ca
            // =========================================================================
            if (CurrentShiftStatus == StatusConfirmStart)
            {
                string receiver = (cboReceiverUser.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                string diff = txtCashDifference.Text;

                string message = $"XÁC NHẬN HOÀN TẤT & CHỐT CA:\n\n" +
                                 $"- Người giao: {txtHandoverUser.Text}\n" +
                                 $"- Người nhận: {receiver}\n" +
                                 $"- Tiền mặt kiểm đếm kết ca: {txtCashClosing.Text} đ\n" +
                                 $"- Doanh số POS: {txtTotalPosRevenue.Text} đ\n" +
                                 $"- Chuyển khoản ngân hàng: {txtTotalBankRevenue?.Text ?? "0"} đ\n" +
                                 $"- Chênh lệch tiền mặt: {diff}\n\n" +
                                 $"Sau khi bấm 'Đồng ý', số liệu ca này sẽ được KHÓA và lưu vào Database làm số liệu đầu ca cho ca kế tiếp!";

                var result = MessageBox.Show(message, "Xác nhận chốt ca — Plus Mart", 
                                             MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.OK)
                {
                    try
                    {
                        // Ghép chuỗi ghi chú khi kết ca:
                        // "Nhân viên đã thay đổi :....Phần nào đã bị thay đổi.... sau đó thêm chuỗi ghi chú mà nhân viên note"
                        string userNote = txtNote?.Text?.Trim() ?? "";
                        string finalNote = "";
                        if (!string.IsNullOrEmpty(_hiddenAuditChangeLog))
                        {
                            if (!string.IsNullOrEmpty(userNote))
                                finalNote = $"{_hiddenAuditChangeLog} Ghi chú nhân viên: {userNote}";
                            else
                                finalNote = _hiddenAuditChangeLog;
                        }
                        else
                        {
                            finalNote = userNote;
                        }

                        var closeReq = new CloseShiftRequestDTO
                        {
                            ShiftId = _currentShiftId,
                            ClosedByUserId = _userId,
                            CashClosing = ParseMoney(txtCashClosing?.Text ?? "0"),
                            CashDifference = ParseMoney(txtCashDifference?.Text ?? "0"),
                            Pos1Closing = ParseMoney(txtPos1Closing?.Text ?? "0"),
                            Pos1Night = ParseMoney(txtPos1Night?.Text ?? "0"),
                            Pos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0"),
                            Pos2Night = ParseMoney(txtPos2Night?.Text ?? "0"),
                            Bank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0"),
                            Bank1Night = ParseMoney(txtBank1Night?.Text ?? "0"),
                            Bank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0"),
                            Bank2Night = ParseMoney(txtBank2Night?.Text ?? "0"),
                            Note = finalNote
                        };

                        if (pnlExpenseItems != null)
                        {
                            foreach (UIElement child in pnlExpenseItems.Children)
                            {
                                if (child is Grid g && g.Children.Count >= 2 && g.Children[0] is TextBox tbDesc && g.Children[1] is TextBox tbAmt)
                                {
                                    if (!string.IsNullOrWhiteSpace(tbDesc.Text))
                                    {
                                        closeReq.Expenses.Add(new ShiftExpenseItemDTO
                                        {
                                            Description = tbDesc.Text.Trim(),
                                            Amount = ParseMoney(tbAmt.Text)
                                        });
                                    }
                                }
                            }
                        }

                        var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/close", closeReq);
                        if (apiRes.IsSuccessStatusCode)
                        {
                            ApplyShiftStatus(StatusClosed);
                            MessageBox.Show("✅ Đã chốt ca và lưu dữ liệu bàn giao vào Database thành công!", 
                                            "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Lỗi API CloseShift: " + ex.Message);
                    }

                    // Fallback giao diện
                    ApplyShiftStatus(StatusClosed);
                    MessageBox.Show("✅ Đã chốt ca và lưu dữ liệu bàn giao thành công!", 
                                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShiftStatus != StatusConfirmStart) return;

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

        #region Quản lý State Machine Trạng Thái Ca Làm Việc (NConfirm -> Changed -> ConfirmStart -> Closed)
        /// <summary>
        /// Áp dụng trạng thái ca làm việc:
        /// - NConfirm: Vừa mở ca, các ô đầu ca lấy từ ca trước và cố định; TẤT CẢ các ô nhập bị khóa; Nút 'Xác nhận dữ liệu đầu ca' & 'Thay đổi thông tin'.
        /// - Changed: Đang sửa thông tin đầu ca (2 phút đếm ngược); Các ô đầu ca ĐƯỢC PHÉP SỬA; Nút 'XÁC NHẬN THAY ĐỔI'.
        /// - ConfirmStart: Đã xác nhận đầu ca; TẤT CẢ các ô nhập được mở; Các ô đầu ca VẪN BỊ KHÓA; Nút là 'HOÀN TẤT & CHỐT CA'.
        /// - Closed: Ca đã chốt hoặc ca quá khứ; Khóa toàn bộ form chỉ xem.
        /// </summary>
        public void ApplyShiftStatus(string status)
        {
            CurrentShiftStatus = status;

            if (status == StatusClosed)
            {
                StopCountdownTimer();
                SetReadOnlyMode();
                return;
            }

            if (status == StatusNConfirm)
            {
                StopCountdownTimer();

                // 1. Hiển thị banner NConfirm
                if (bdrNConfirmNotice != null) bdrNConfirmNotice.Visibility = Visibility.Visible;
                if (bdrChangedNotice != null) bdrChangedNotice.Visibility = Visibility.Collapsed;
                if (bdrReadOnlyNotice != null) bdrReadOnlyNotice.Visibility = Visibility.Collapsed;

                // 2. Nút Thay đổi thông tin hiển thị
                if (btnChangeInitialData != null)
                {
                    btnChangeInitialData.Visibility = Visibility.Visible;
                    btnChangeInitialData.IsEnabled = true;
                }

                // 3. Nút hành động hiển thị: Xác nhận dữ liệu đầu ca
                if (btnSaveHandover != null)
                {
                    btnSaveHandover.IsEnabled = true;
                    btnSaveHandover.Content = "✔ XÁC NHẬN DỮ LIỆU ĐẦU CA";
                    btnSaveHandover.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D4ED8")); // Blue nổi bật
                }

                // 4. Khóa toàn bộ các ô nhập liệu & ô đầu ca
                SetInputsEditable(false);
                SetOpeningInputsEditable(false);

                // 5. Các nút tác vụ bổ sung bị khóa
                if (btnOpenCashPopup != null) btnOpenCashPopup.IsEnabled = false;
                if (btnCalculateCashHeader != null) btnCalculateCashHeader.IsEnabled = false;
                if (btnAddExpense != null) btnAddExpense.IsEnabled = false;
                if (cboReceiverUser != null) cboReceiverUser.IsEnabled = false;
            }
            else if (status == StatusChanged)
            {
                // 1. Hiển thị banner Changed có đếm ngược 2 phút
                if (bdrNConfirmNotice != null) bdrNConfirmNotice.Visibility = Visibility.Collapsed;
                if (bdrChangedNotice != null) bdrChangedNotice.Visibility = Visibility.Visible;
                if (bdrReadOnlyNotice != null) bdrReadOnlyNotice.Visibility = Visibility.Collapsed;

                // 2. Ẩn nút 'Thay đổi thông tin'
                if (btnChangeInitialData != null) btnChangeInitialData.Visibility = Visibility.Collapsed;

                // 3. Nút hành động đổi thành: 'XÁC NHẬN THAY ĐỔI'
                if (btnSaveHandover != null)
                {
                    btnSaveHandover.IsEnabled = true;
                    btnSaveHandover.Content = "✔ XÁC NHẬN THAY ĐỔI";
                    btnSaveHandover.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D")); // Green nổi bật
                }

                // 4. Khóa các ô cuối ca trong lúc sửa đầu ca
                SetInputsEditable(false);

                // 5. MỞ KHÓA CÁC Ô ĐẦU CA để nhân viên sửa
                SetOpeningInputsEditable(true);

                // 5. Bắt đầu đếm ngược 2 phút
                StartCountdownTimer();
            }
            else if (status == StatusConfirmStart)
            {
                StopCountdownTimer();

                // 1. Ẩn tất cả banner khóa
                if (bdrNConfirmNotice != null) bdrNConfirmNotice.Visibility = Visibility.Collapsed;
                if (bdrChangedNotice != null) bdrChangedNotice.Visibility = Visibility.Collapsed;
                if (bdrReadOnlyNotice != null) bdrReadOnlyNotice.Visibility = Visibility.Collapsed;

                // 2. Ẩn nút 'Thay đổi thông tin'
                if (btnChangeInitialData != null) btnChangeInitialData.Visibility = Visibility.Collapsed;

                // 3. Nút hành động chuyển sang: HOÀN TẤT & CHỐT CA
                if (btnSaveHandover != null)
                {
                    btnSaveHandover.IsEnabled = true;
                    btnSaveHandover.Content = "💾 HOÀN TẤT & CHỐT CA";
                    btnSaveHandover.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));
                }

                // 4. Khóa vĩnh viễn các ô đầu ca & Mở khóa các ô nhập trong ca
                SetOpeningInputsEditable(false);
                SetInputsEditable(true);

                // 5. Các nút tác vụ bổ sung được mở
                if (btnOpenCashPopup != null) btnOpenCashPopup.IsEnabled = true;
                if (btnCalculateCashHeader != null) btnCalculateCashHeader.IsEnabled = true;
                if (btnAddExpense != null) btnAddExpense.IsEnabled = true;
                if (cboReceiverUser != null) cboReceiverUser.IsEnabled = true;
            }
        }

        #region Bộ Đếm Thời Gian 2 Phút Chỉnh Sửa Đầu Ca
        private void StartCountdownTimer()
        {
            StopCountdownTimer();
            _secondsRemaining = 120; // 2 phút
            UpdateCountdownDisplay();

            _editCountdownTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _editCountdownTimer.Tick += (s, e) =>
            {
                _secondsRemaining--;
                UpdateCountdownDisplay();

                if (_secondsRemaining <= 0)
                {
                    StopCountdownTimer();
                    MessageBox.Show("⏰ Đã hết thời gian 2 phút thay đổi thông tin!\nHệ thống sẽ khóa lại các ô thông tin đầu ca.", 
                                    "Hết giờ chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ApplyShiftStatus(StatusNConfirm);
                }
            };
            _editCountdownTimer.Start();
        }

        private void StopCountdownTimer()
        {
            if (_editCountdownTimer != null)
            {
                _editCountdownTimer.Stop();
                _editCountdownTimer = null;
            }
        }

        private void UpdateCountdownDisplay()
        {
            int mins = _secondsRemaining / 60;
            int secs = _secondsRemaining % 60;
            if (lblCountdownTimer != null)
            {
                lblCountdownTimer.Text = $"Thời gian còn lại: {mins:D2}:{secs:D2}";
            }
        }
        #endregion

        /// <summary>
        /// Khóa hoặc Mở khóa các ô thông tin đầu ca:
        /// - Khi mở sửa: Đổi sang viền cam và nền vàng nhạt để nhân viên nhận diện rõ ràng.
        /// - Khi khóa: Trở về màu xám nhạt cố định.
        /// </summary>
        private void SetOpeningInputsEditable(bool isEditable)
        {
            var normalBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
            var editBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")); // Vàng nhạt cảnh báo đang sửa
            var editBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")); // Viền cam đậm
            var normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB"));

            TextBox?[] openingBoxes = { txtPos1Opening, txtPos2Opening, txtBank1Opening, txtBank2Opening, txtCashOpening };
            foreach (var tb in openingBoxes)
            {
                if (tb == null) continue;
                tb.IsReadOnly = !isEditable;
                tb.Focusable = true;
                tb.IsHitTestVisible = true;
                tb.Foreground = isEditable ? Brushes.Black : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563"));
                tb.Background = isEditable ? editBg : normalBg;
                tb.BorderBrush = isEditable ? editBorder : normalBorder;
                tb.BorderThickness = isEditable ? new Thickness(1.5) : new Thickness(1);
                tb.Cursor = isEditable ? System.Windows.Input.Cursors.IBeam : System.Windows.Input.Cursors.Arrow;
            }

            if (isEditable && txtCashOpening != null)
            {
                txtCashOpening.Focus();
                txtCashOpening.SelectAll();
            }
        }

        private void SetInputsEditable(bool isEditable)
        {
            // 1. Ô cuối ca & đêm của DOANH SỐ APP BÁN HÀNG
            if (txtPos1Closing != null) txtPos1Closing.IsReadOnly = !isEditable;
            if (txtPos1Night != null)   txtPos1Night.IsReadOnly   = !isEditable;
            if (txtPos2Closing != null) txtPos2Closing.IsReadOnly = !isEditable;
            if (txtPos2Night != null)   txtPos2Night.IsReadOnly   = !isEditable;

            // 2. Ô cuối ca & đêm của CHUYỂN KHOẢN
            if (txtBank1Closing != null) txtBank1Closing.IsReadOnly = !isEditable;
            if (txtBank1Night != null)   txtBank1Night.IsReadOnly   = !isEditable;
            if (txtBank2Closing != null) txtBank2Closing.IsReadOnly = !isEditable;
            if (txtBank2Night != null)   txtBank2Night.IsReadOnly   = !isEditable;

            // 3. Ô cuối ca của TIỀN MẶT
            if (txtCashClosing != null) txtCashClosing.IsReadOnly = !isEditable;

            // 4. Ô ghi chú bàn giao ca
            if (txtNote != null) txtNote.IsReadOnly = !isEditable;

            // 5. Cập nhật các dòng thu chi động
            if (pnlExpenseItems != null)
            {
                foreach (UIElement child in pnlExpenseItems.Children)
                {
                    if (child is Grid g)
                    {
                        foreach (UIElement elem in g.Children)
                        {
                            if (elem is TextBox tb) tb.IsReadOnly = !isEditable;
                            if (elem is Button btn)
                            {
                                btn.IsEnabled = isEditable;
                                btn.Visibility = isEditable ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
        }

        public void SetReadOnlyMode()
        {
            CurrentShiftStatus = StatusClosed;

            if (bdrReadOnlyNotice != null)
                bdrReadOnlyNotice.Visibility = Visibility.Visible;
            if (bdrNConfirmNotice != null)
                bdrNConfirmNotice.Visibility = Visibility.Collapsed;

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

            if (btnCalculateCashHeader != null)
            {
                btnCalculateCashHeader.IsEnabled = false;
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
            SetInputsEditable(false);

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
        }
        #endregion
    }
}
