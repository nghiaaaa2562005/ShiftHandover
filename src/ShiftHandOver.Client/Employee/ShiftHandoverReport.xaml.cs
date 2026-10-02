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
        private decimal _currentGrandCashTotal = 0m;

        public const string StatusNConfirm = "NConfirm";
        public const string StatusChanged = "Changed";
        public const string StatusConfirmStart = "ConfirmStart";
        public const string StatusClosed = "Closed";
        public const string StatusConfirmStartNC = "ConfirmStartNC";
        public const string StatusClosedNC = "ClosedNC";

        private bool _hasChangedInitialData = false;

        public string CurrentShiftStatus { get; private set; } = StatusNConfirm;
        public bool IsReadOnlyMode => CurrentShiftStatus == StatusClosed || CurrentShiftStatus == StatusClosedNC;

        public static bool IsShiftModified(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            if (status.Equals("NConfirm", StringComparison.OrdinalIgnoreCase)) return false;
            return status.EndsWith("NC", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Changed", StringComparison.OrdinalIgnoreCase);
        }

        private System.Windows.Threading.DispatcherTimer? _editCountdownTimer;
        private int _secondsRemaining = 120; // 2 phút đếm ngược

        private int _currentShiftId = 0;
        private int _branchId = 1;
        private string _branchName = "";
        private string _shiftCode = "MORNING";
        private string _shiftName = "";
        private string _employeeName = "";
        private DateTime _workDate = DateTime.Today;
        private int _userId = 2;
        private System.Collections.Generic.List<string> _closingEmployeeNames = new();

        private bool IsNightShift => string.Equals(_shiftCode, "NIGHT", StringComparison.OrdinalIgnoreCase)
                                     || (!string.IsNullOrEmpty(_shiftName) && _shiftName.IndexOf("đêm", StringComparison.OrdinalIgnoreCase) >= 0);

        private void ApplyNightShiftVisibility()
        {
            bool isNight = IsNightShift;

            var nightVisibility = isNight ? Visibility.Visible : Visibility.Collapsed;
            var colLabelWidth = isNight ? new GridLength(45) : new GridLength(0);
            var colValueWidth = isNight ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            // Sapo POS
            if (colPos1OpenL != null) colPos1OpenL.Width = colLabelWidth;
            if (colPos1OpenV != null) colPos1OpenV.Width = colValueWidth;
            if (colPos1CloseL != null) colPos1CloseL.Width = colLabelWidth;
            if (colPos1CloseV != null) colPos1CloseV.Width = colValueWidth;
            if (lblPos1Night != null) lblPos1Night.Visibility = nightVisibility;
            if (txtPos1Night != null)
            {
                txtPos1Night.Visibility = nightVisibility;
                if (!isNight) txtPos1Night.Text = "0";
            }

            // KiotViet
            if (colPos2OpenL != null) colPos2OpenL.Width = colLabelWidth;
            if (colPos2OpenV != null) colPos2OpenV.Width = colValueWidth;
            if (colPos2CloseL != null) colPos2CloseL.Width = colLabelWidth;
            if (colPos2CloseV != null) colPos2CloseV.Width = colValueWidth;
            if (lblPos2Night != null) lblPos2Night.Visibility = nightVisibility;
            if (txtPos2Night != null)
            {
                txtPos2Night.Visibility = nightVisibility;
                if (!isNight) txtPos2Night.Text = "0";
            }

            // TingTing
            if (colBank1OpenL != null) colBank1OpenL.Width = colLabelWidth;
            if (colBank1OpenV != null) colBank1OpenV.Width = colValueWidth;
            if (colBank1CloseL != null) colBank1CloseL.Width = colLabelWidth;
            if (colBank1CloseV != null) colBank1CloseV.Width = colValueWidth;
            if (lblBank1Night != null) lblBank1Night.Visibility = nightVisibility;
            if (txtBank1Night != null)
            {
                txtBank1Night.Visibility = nightVisibility;
                if (!isNight) txtBank1Night.Text = "0";
            }

            // Zalo Pay
            if (colBank2OpenL != null) colBank2OpenL.Width = colLabelWidth;
            if (colBank2OpenV != null) colBank2OpenV.Width = colValueWidth;
            if (colBank2CloseL != null) colBank2CloseL.Width = colLabelWidth;
            if (colBank2CloseV != null) colBank2CloseV.Width = colValueWidth;
            if (lblBank2Night != null) lblBank2Night.Visibility = nightVisibility;
            if (txtBank2Night != null)
            {
                txtBank2Night.Visibility = nightVisibility;
                if (!isNight) txtBank2Night.Text = "0";
            }
        }

        // Lưu vết số liệu ban đầu để kiểm tra phần nào đã bị thay đổi
        private decimal _originalCashOpening = 0;
        private decimal _originalCashClosing = 0;
        private decimal _originalPos1Opening = 0;
        private decimal _originalPos1Closing = 0;
        private decimal _originalPos1Night = 0;
        private decimal _originalPos2Opening = 0;
        private decimal _originalPos2Closing = 0;
        private decimal _originalPos2Night = 0;
        private decimal _originalBank1Opening = 0;
        private decimal _originalBank1Closing = 0;
        private decimal _originalBank1Night = 0;
        private decimal _originalBank2Opening = 0;
        private decimal _originalBank2Closing = 0;
        private decimal _originalBank2Night = 0;
        private string _originalNote = "";

        // Trạng thái sửa đổi ca đã chốt (cần xác thực 1 nhân viên phụ trách ca)
        private bool _isEditingClosedShift = false;
        private int _editorUserId = 0;
        private string _editorFullName = "";

        // Ghi chú hệ thống ẩn — nhân viên không nhìn thấy trên giao diện, không thể sửa đổi
        private string _hiddenAuditChangeLog = "";

        public ShiftHandoverReport()
        {
            InitializeComponent();
            ApplyNightShiftVisibility();
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
            _branchName = branch ?? "";
            _shiftCode = shiftCode;
            _shiftName = shift;
            _employeeName = employeeName ?? "";
            _workDate = date;
            _userId = userId;

            ApplyNightShiftVisibility();

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

                        if (!string.IsNullOrEmpty(shiftDetail.BranchName))
                        {
                            _branchName = shiftDetail.BranchName;
                        }
                        if (!string.IsNullOrEmpty(shiftDetail.ShiftType))
                        {
                            _shiftCode = shiftDetail.ShiftType;
                        }
                        if (!string.IsNullOrEmpty(shiftDetail.ShiftTypeName))
                        {
                            _shiftName = shiftDetail.ShiftTypeName;
                        }
                        if (shiftDetail.EmployeeNames != null && shiftDetail.EmployeeNames.Count > 0)
                        {
                            _closingEmployeeNames = new System.Collections.Generic.List<string>(shiftDetail.EmployeeNames);
                        }
                        ApplyNightShiftVisibility();

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

                        // 2. Điền các thông tin cuối ca nếu đã có dữ liệu trước đó (nếu chưa có hoặc mới mở ca thì mặc định bằng đầu ca để chênh lệch bắt đầu từ 0)
                        if (shiftDetail.Pos1Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Pos1Closing.Value > 0) && txtPos1Closing != null)
                            txtPos1Closing.Text = FormatMoney(shiftDetail.Pos1Closing.Value);
                        else if (txtPos1Closing != null)
                            txtPos1Closing.Text = FormatMoney(shiftDetail.Pos1Opening);

                        if (shiftDetail.Pos1Night.HasValue && txtPos1Night != null && IsNightShift)
                            txtPos1Night.Text = FormatMoney(shiftDetail.Pos1Night.Value);

                        if (shiftDetail.Pos2Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Pos2Closing.Value > 0) && txtPos2Closing != null)
                            txtPos2Closing.Text = FormatMoney(shiftDetail.Pos2Closing.Value);
                        else if (txtPos2Closing != null)
                            txtPos2Closing.Text = FormatMoney(shiftDetail.Pos2Opening);

                        if (shiftDetail.Pos2Night.HasValue && txtPos2Night != null && IsNightShift)
                            txtPos2Night.Text = FormatMoney(shiftDetail.Pos2Night.Value);

                        if (shiftDetail.Bank1Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Bank1Closing.Value > 0) && txtBank1Closing != null)
                            txtBank1Closing.Text = FormatMoney(shiftDetail.Bank1Closing.Value);
                        else if (txtBank1Closing != null)
                            txtBank1Closing.Text = FormatMoney(shiftDetail.Bank1Opening);

                        if (shiftDetail.Bank1Night.HasValue && txtBank1Night != null && IsNightShift)
                            txtBank1Night.Text = FormatMoney(shiftDetail.Bank1Night.Value);

                        if (shiftDetail.Bank2Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Bank2Closing.Value > 0) && txtBank2Closing != null)
                            txtBank2Closing.Text = FormatMoney(shiftDetail.Bank2Closing.Value);
                        else if (txtBank2Closing != null)
                            txtBank2Closing.Text = FormatMoney(shiftDetail.Bank2Opening);

                        if (shiftDetail.Bank2Night.HasValue && txtBank2Night != null && IsNightShift)
                            txtBank2Night.Text = FormatMoney(shiftDetail.Bank2Night.Value);

                        if (shiftDetail.CashClosing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.CashClosing.Value > 0) && txtCashClosing != null)
                            txtCashClosing.Text = FormatMoney(shiftDetail.CashClosing.Value);
                        else if (txtCashClosing != null)
                            txtCashClosing.Text = FormatMoney(shiftDetail.CashOpening);

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

                        // Đánh dấu nếu ca đã qua chỉnh sửa (NC)
                        if (shiftDetail.Status != null && IsShiftModified(shiftDetail.Status))
                        {
                            _hasChangedInitialData = true;
                        }
                        else
                        {
                            _hasChangedInitialData = false;
                        }

                        // 3. Áp dụng trạng thái ca
                        if (isReadOnly || shiftDetail.IsReadOnly)
                        {
                            string targetStatus = (shiftDetail.Status != null && IsShiftModified(shiftDetail.Status)) ? StatusClosedNC : StatusClosed;
                            ApplyShiftStatus(targetStatus);
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
                Content = "Xóa",
                Height = 30,
                Width = 32,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                BorderBrush = Brushes.Black,
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

        private void HighlightInvalidClosing(TextBox? textBox, bool isInvalid)
        {
            if (textBox == null) return;
            if (isInvalid)
            {
                textBox.BorderBrush = Brushes.Black;
                textBox.BorderThickness = new Thickness(2.5);
                textBox.ToolTip = "Giá trị Cuối ca phải lớn hơn hoặc bằng giá trị Đầu ca!";
            }
            else
            {
                textBox.ClearValue(Border.BorderBrushProperty);
                textBox.ClearValue(Border.BorderThicknessProperty);
                textBox.ToolTip = null;
            }
        }

        #region Tính Toán Toàn Bộ Biên Bản Giao Nhận Ca
        private void CalculateAll(object? sender, TextChangedEventArgs? e)
        {
            if (!IsLoaded && sender != null) return;

            // 1. SAPO POS: (Kết ca - Đầu ca) + Đêm (chỉ tính đêm khi vào ca đêm)
            decimal pos1Opening = ParseMoney(txtPos1Opening?.Text ?? "0");
            decimal pos1Closing = ParseMoney(txtPos1Closing?.Text ?? "0");
            decimal pos1Night   = IsNightShift ? ParseMoney(txtPos1Night?.Text ?? "0") : 0;
            decimal pos1Diff    = pos1Closing - pos1Opening;
            decimal pos1Revenue = (pos1Diff >= 0 ? pos1Diff : 0) + pos1Night;

            // 2. KIOT VIET: (Kết ca - Đầu ca) + Đêm
            decimal pos2Opening = ParseMoney(txtPos2Opening?.Text ?? "0");
            decimal pos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0");
            decimal pos2Night   = IsNightShift ? ParseMoney(txtPos2Night?.Text ?? "0") : 0;
            decimal pos2Diff    = pos2Closing - pos2Opening;
            decimal pos2Revenue = (pos2Diff >= 0 ? pos2Diff : 0) + pos2Night;

            // 3. TỔNG DOANH SỐ APP TRONG CA (DOANH THU)
            decimal totalPosRevenue = pos1Revenue + pos2Revenue;
            if (txtTotalPosRevenue != null) txtTotalPosRevenue.Text = FormatMoney(totalPosRevenue);

            // 4. CHUYỂN KHOẢN TINGTING: (Kết ca - Đầu ca) + Đêm
            decimal bank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
            decimal bank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0");
            decimal bank1Night   = IsNightShift ? ParseMoney(txtBank1Night?.Text ?? "0") : 0;
            decimal bank1Diff    = bank1Closing - bank1Opening;
            decimal bank1Revenue = (bank1Diff >= 0 ? bank1Diff : 0) + bank1Night;

            // 5. CHUYỂN KHOẢN ZALO PAY: (Kết ca - Đầu ca) + Đêm
            decimal bank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
            decimal bank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0");
            decimal bank2Night   = IsNightShift ? ParseMoney(txtBank2Night?.Text ?? "0") : 0;
            decimal bank2Diff    = bank2Closing - bank2Opening;
            decimal bank2Revenue = (bank2Diff >= 0 ? bank2Diff : 0) + bank2Night;

            // Cảnh báo viền đậm trực quan nếu cuối ca < đầu ca
            HighlightInvalidClosing(txtPos1Closing, pos1Closing < pos1Opening);
            HighlightInvalidClosing(txtPos2Closing, pos2Closing < pos2Opening);
            HighlightInvalidClosing(txtBank1Closing, bank1Closing < bank1Opening);
            HighlightInvalidClosing(txtBank2Closing, bank2Closing < bank2Opening);

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

            // 7. Tiền mặt chênh lệch kết ca = cuối ca của tiền mặt trừ đi đầu ca của tiền mặt
            decimal cashDiffClosing = cashClosing - cashOpening;
            if (txtSummaryCashClosing != null)
            {
                txtSummaryCashClosing.Text = FormatMoney(cashDiffClosing);
            }

            // 8. TIỀN CHÊNH LỆCH (tiền âm hay dương của ca đó)
            // = Tiền mặt chênh lệch kết ca + Chuyển khoản + Chi phí - Doanh thu
            decimal cashDifference = cashDiffClosing + totalBankRevenue + otherExpenses - totalPosRevenue;

            // Hiển thị âm/dương (chỉ dùng màu đen và trắng)
            if (txtCashDifference != null)
            {
                bdrCashDiff.Background = Brushes.White;
                txtCashDifference.Foreground = Brushes.Black;
                txtCashDifference.BorderBrush = Brushes.Black;
                lblCashDiffStatus.Foreground = Brushes.White;

                if (cashDifference < 0)
                {
                    // Âm tiền
                    txtCashDifference.Text = $"-{FormatMoney(Math.Abs(cashDifference))} đ";
                    lblCashDiffStatus.Text = $"ÂM TIỀN: -{FormatMoney(Math.Abs(cashDifference))} đ";
                }
                else if (cashDifference > 0)
                {
                    // Dương tiền
                    txtCashDifference.Text = $"+{FormatMoney(cashDifference)} đ";
                    lblCashDiffStatus.Text = $"DƯƠNG TIỀN: +{FormatMoney(cashDifference)} đ";
                }
                else
                {
                    // Khớp đủ (0 đ)
                    txtCashDifference.Text = "0 đ";
                    lblCashDiffStatus.Text = "KHỚP ĐỦ (0 đ)";
                }
            }
        }
        #endregion

        #region Nút Thao Tác Chốt Ca & Xác Nhận Đầu Ca
        private void SnapshotAllValues()
        {
            _originalCashOpening = ParseMoney(txtCashOpening?.Text ?? "0");
            _originalCashClosing = ParseMoney(txtCashClosing?.Text ?? "0");

            _originalPos1Opening = ParseMoney(txtPos1Opening?.Text ?? "0");
            _originalPos1Closing = ParseMoney(txtPos1Closing?.Text ?? "0");
            _originalPos1Night   = ParseMoney(txtPos1Night?.Text ?? "0");

            _originalPos2Opening = ParseMoney(txtPos2Opening?.Text ?? "0");
            _originalPos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0");
            _originalPos2Night   = ParseMoney(txtPos2Night?.Text ?? "0");

            _originalBank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
            _originalBank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0");
            _originalBank1Night   = ParseMoney(txtBank1Night?.Text ?? "0");

            _originalBank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
            _originalBank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0");
            _originalBank2Night   = ParseMoney(txtBank2Night?.Text ?? "0");

            _originalNote = txtNote?.Text ?? "";
        }

        /// <summary>
        /// Hoàn tác toàn bộ số liệu về trạng thái trước khi nhân viên bấm Thay đổi thông tin
        /// </summary>
        private void RestoreOriginalValues()
        {
            if (txtCashOpening != null) txtCashOpening.Text = FormatMoney(_originalCashOpening);
            if (txtCashClosing != null) txtCashClosing.Text = FormatMoney(_originalCashClosing);

            if (txtPos1Opening != null) txtPos1Opening.Text = FormatMoney(_originalPos1Opening);
            if (txtPos1Closing != null) txtPos1Closing.Text = FormatMoney(_originalPos1Closing);
            if (txtPos1Night != null) txtPos1Night.Text = FormatMoney(_originalPos1Night);

            if (txtPos2Opening != null) txtPos2Opening.Text = FormatMoney(_originalPos2Opening);
            if (txtPos2Closing != null) txtPos2Closing.Text = FormatMoney(_originalPos2Closing);
            if (txtPos2Night != null) txtPos2Night.Text = FormatMoney(_originalPos2Night);

            if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(_originalBank1Opening);
            if (txtBank1Closing != null) txtBank1Closing.Text = FormatMoney(_originalBank1Closing);
            if (txtBank1Night != null) txtBank1Night.Text = FormatMoney(_originalBank1Night);

            if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(_originalBank2Opening);
            if (txtBank2Closing != null) txtBank2Closing.Text = FormatMoney(_originalBank2Closing);
            if (txtBank2Night != null) txtBank2Night.Text = FormatMoney(_originalBank2Night);

            if (txtNote != null && _originalNote != null) txtNote.Text = _originalNote;

            CalculateAll(null, null);
        }

        private async void BtnChangeInitialData_Click(object sender, RoutedEventArgs e)
        {
            // 0. Kiểm tra ràng buộc: Ca làm việc chỉ được thay đổi duy nhất một lần
            if (CurrentShiftStatus == StatusClosedNC || IsShiftModified(CurrentShiftStatus) || _hasChangedInitialData)
            {
                MessageBox.Show("Ca làm việc này chỉ được thay đổi một lần thôi và không thể điều chỉnh thêm nữa!", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Đề phòng nhân viên ấn nhầm: Hiện thông báo Bạn có muốn hủy không? với 2 option Cancel và OK
            var confirmResult = MessageBox.Show(
                "Bạn có muốn hủy không?\n\n" +
                "• Bấm Cancel: Hoàn tác và hủy bỏ để không bị ấn nhầm.\n" +
                "• Bấm OK: Bắt buộc thay đổi thông tin (không thể hoàn tác).",
                "Thông báo",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.Cancel)
            {
                RestoreOriginalValues();
                return; // Hoàn tác để không ấn nhầm
            }

            // TÌNH HUỐNG 1: Ca đã xong (StatusClosed hoặc IsReadOnlyMode) -> Bắt buộc xác thực chữ ký của 1 người phụ trách ca
            if (CurrentShiftStatus == StatusClosed || IsReadOnlyMode)
            {
                var verifyDialog = new VerifyShiftOwnerDialog("")
                {
                    Owner = this
                };

                bool? dialogRes = verifyDialog.ShowDialog();
                if (dialogRes != true)
                {
                    RestoreOriginalValues();
                    return; // Người dùng hủy bỏ
                }

                // Gửi yêu cầu xác thực lên Server
                try
                {
                    var verifyReq = new VerifyShiftOwnerRequestDTO
                    {
                        ShiftId = _currentShiftId,
                        Username = verifyDialog.Username,
                        Password = verifyDialog.Password
                    };

                    var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/verify-owner", verifyReq);
                    if (!apiRes.IsSuccessStatusCode)
                    {
                        var errObj = await apiRes.Content.ReadFromJsonAsync<VerifyShiftOwnerResponseDTO>();
                        string errMsg = errObj?.Message ?? await apiRes.Content.ReadAsStringAsync();
                        MessageBox.Show(errMsg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var verifyData = await apiRes.Content.ReadFromJsonAsync<VerifyShiftOwnerResponseDTO>();
                    if (verifyData == null || !verifyData.IsAuthorized)
                    {
                        MessageBox.Show(verifyData?.Message ?? "Bạn không phụ trách ca này", 
                                        "Từ chối", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _editorUserId = verifyData.UserId;
                    _editorFullName = verifyData.FullName;
                    _isEditingClosedShift = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể kết nối đến máy chủ để xác thực quyền sửa: " + ex.Message, 
                                    "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Chụp lại toàn bộ giá trị gốc để lập log kiểm toán
                SnapshotAllValues();

                // Mở khóa cho phép sửa ca đã chốt (Bắt buộc thay đổi)
                EnterEditClosedShiftMode();
                return;
            }

            // TÌNH HUỐNG 2: Ca mới mở (NConfirm) sửa đầu ca -> Bắt buộc thay đổi
            _isEditingClosedShift = false;
            SnapshotAllValues();
            ApplyShiftStatus(StatusChanged);
        }

        private void EnterEditClosedShiftMode()
        {
            CurrentShiftStatus = StatusChanged;

            // 1. Banner
            if (bdrNConfirmNotice != null) bdrNConfirmNotice.Visibility = Visibility.Collapsed;
            if (bdrReadOnlyNotice != null) bdrReadOnlyNotice.Visibility = Visibility.Collapsed;
            if (bdrChangedNotice != null)
            {
                bdrChangedNotice.Visibility = Visibility.Visible;
                if (lblCountdownTimer != null) lblCountdownTimer.Text = "Thời gian còn lại: 02:00";
            }

            // 2. Ẩn nút 'Thay đổi thông tin'
            if (btnChangeInitialData != null) btnChangeInitialData.Visibility = Visibility.Collapsed;

            // 3. Nút hành động đổi thành: 'XÁC NHẬN THAY ĐỔI'
            if (btnSaveHandover != null)
            {
                btnSaveHandover.IsEnabled = true;
                btnSaveHandover.Content = "XÁC NHẬN THAY ĐỔI";
                btnSaveHandover.Background = Brushes.Black;
                btnSaveHandover.Foreground = Brushes.White;
            }

            // 4. Mở khóa toàn bộ các ô để sửa
            SetOpeningInputsEditable(true);
            SetInputsEditable(true);
            if (btnOpenCashPopup != null) btnOpenCashPopup.IsEnabled = true;
            if (btnCalculateCashHeader != null) btnCalculateCashHeader.IsEnabled = true;
            if (btnAddExpense != null) { btnAddExpense.IsEnabled = true; btnAddExpense.Visibility = Visibility.Visible; }

            // 5. Bắt đầu đếm ngược 2 phút
            StartCountdownTimer();
        }

        private async Task SaveChangedShiftDataAsync(bool isTimeoutAutoSave = false)
        {
            if (_isEditingClosedShift)
            {
                // 1. Thu thập số liệu sau khi sửa
                decimal newCashOpening = ParseMoney(txtCashOpening?.Text ?? "0");
                decimal newCashClosing = ParseMoney(txtCashClosing?.Text ?? "0");
                decimal newCashDiff    = ParseMoney(txtCashDifference?.Text ?? "0");

                decimal newPos1Open  = ParseMoney(txtPos1Opening?.Text ?? "0");
                decimal newPos1Close = ParseMoney(txtPos1Closing?.Text ?? "0");
                decimal newPos1Night = IsNightShift ? ParseMoney(txtPos1Night?.Text ?? "0") : 0m;

                decimal newPos2Open  = ParseMoney(txtPos2Opening?.Text ?? "0");
                decimal newPos2Close = ParseMoney(txtPos2Closing?.Text ?? "0");
                decimal newPos2Night = IsNightShift ? ParseMoney(txtPos2Night?.Text ?? "0") : 0m;

                decimal newBank1Open  = ParseMoney(txtBank1Opening?.Text ?? "0");
                decimal newBank1Close = ParseMoney(txtBank1Closing?.Text ?? "0");
                decimal newBank1Night = IsNightShift ? ParseMoney(txtBank1Night?.Text ?? "0") : 0m;

                decimal newBank2Open  = ParseMoney(txtBank2Opening?.Text ?? "0");
                decimal newBank2Close = ParseMoney(txtBank2Closing?.Text ?? "0");
                decimal newBank2Night = IsNightShift ? ParseMoney(txtBank2Night?.Text ?? "0") : 0m;

                // 2. Phát hiện chi tiết các trường bị thay đổi
                var changedList = new List<string>();
                if (newCashOpening != _originalCashOpening)
                    changedList.Add($"Tiền két đầu ({FormatMoney(_originalCashOpening)} -> {FormatMoney(newCashOpening)})");
                if (newCashClosing != _originalCashClosing)
                    changedList.Add($"Tiền két cuối ({FormatMoney(_originalCashClosing)} -> {FormatMoney(newCashClosing)})");

                if (newPos1Open != _originalPos1Opening)
                    changedList.Add($"Sapo POS đầu ({FormatMoney(_originalPos1Opening)} -> {FormatMoney(newPos1Open)})");
                if (newPos1Close != _originalPos1Closing)
                    changedList.Add($"Sapo POS cuối ({FormatMoney(_originalPos1Closing)} -> {FormatMoney(newPos1Close)})");
                if (newPos1Night != _originalPos1Night)
                    changedList.Add($"Sapo POS đêm ({FormatMoney(_originalPos1Night)} -> {FormatMoney(newPos1Night)})");

                if (newPos2Open != _originalPos2Opening)
                    changedList.Add($"KiotViet đầu ({FormatMoney(_originalPos2Opening)} -> {FormatMoney(newPos2Open)})");
                if (newPos2Close != _originalPos2Closing)
                    changedList.Add($"KiotViet cuối ({FormatMoney(_originalPos2Closing)} -> {FormatMoney(newPos2Close)})");
                if (newPos2Night != _originalPos2Night)
                    changedList.Add($"KiotViet đêm ({FormatMoney(_originalPos2Night)} -> {FormatMoney(newPos2Night)})");

                if (newBank1Open != _originalBank1Opening)
                    changedList.Add($"TingTing đầu ({FormatMoney(_originalBank1Opening)} -> {FormatMoney(newBank1Open)})");
                if (newBank1Close != _originalBank1Closing)
                    changedList.Add($"TingTing cuối ({FormatMoney(_originalBank1Closing)} -> {FormatMoney(newBank1Close)})");
                if (newBank1Night != _originalBank1Night)
                    changedList.Add($"TingTing đêm ({FormatMoney(_originalBank1Night)} -> {FormatMoney(newBank1Night)})");

                if (newBank2Open != _originalBank2Opening)
                    changedList.Add($"Zalo Pay đầu ({FormatMoney(_originalBank2Opening)} -> {FormatMoney(newBank2Open)})");
                if (newBank2Close != _originalBank2Closing)
                    changedList.Add($"Zalo Pay cuối ({FormatMoney(_originalBank2Closing)} -> {FormatMoney(newBank2Close)})");
                if (newBank2Night != _originalBank2Night)
                    changedList.Add($"Zalo Pay đêm ({FormatMoney(_originalBank2Night)} -> {FormatMoney(newBank2Night)})");

                string changeSummary = changedList.Count > 0 ? string.Join(", ", changedList) : "Không có thay đổi số liệu";

                var updateReq = new UpdateClosedShiftRequestDTO
                {
                    ShiftId = _currentShiftId,
                    EditorUserId = _editorUserId,
                    EditorFullName = _editorFullName,
                    CashOpening = newCashOpening,
                    CashClosing = newCashClosing,
                    CashDifference = newCashDiff,
                    Pos1Opening = newPos1Open,
                    Pos1Closing = newPos1Close,
                    Pos1Night = newPos1Night,
                    Pos2Opening = newPos2Open,
                    Pos2Closing = newPos2Close,
                    Pos2Night = newPos2Night,
                    Bank1Opening = newBank1Open,
                    Bank1Closing = newBank1Close,
                    Bank1Night = newBank1Night,
                    Bank2Opening = newBank2Open,
                    Bank2Closing = newBank2Close,
                    Bank2Night = newBank2Night,
                    ChangeLog = changeSummary,
                    UserNote = txtNote?.Text?.Trim() ?? ""
                };

                if (pnlExpenseItems != null)
                {
                    foreach (UIElement child in pnlExpenseItems.Children)
                    {
                        if (child is Grid g && g.Children.Count >= 2 && g.Children[0] is TextBox tbDesc && g.Children[1] is TextBox tbAmt)
                        {
                            decimal amt = ParseMoney(tbAmt.Text);
                            if (!string.IsNullOrWhiteSpace(tbDesc.Text) && amt > 0)
                            {
                                updateReq.Expenses.Add(new ShiftExpenseItemDTO
                                {
                                    Description = tbDesc.Text.Trim(),
                                    Amount = amt
                                });
                            }
                        }
                    }
                }

                try
                {
                    var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/update-closed-shift", updateReq);
                    if (apiRes.IsSuccessStatusCode)
                    {
                        _isEditingClosedShift = false;
                        _hasChangedInitialData = true;
                        ApplyShiftStatus(StatusClosedNC);
                        string title = isTimeoutAutoSave ? "Hết giờ — Tự động lưu" : "Thành công";
                        string prefix = isTimeoutAutoSave ? "ĐÃ HẾT THỜI GIAN 2 PHÚT!\nHệ thống đã tự động lưu thay đổi ca làm việc:\n\n" : "ĐÃ HOÀN TẤT VÀ LƯU THAY ĐỔI CA LÀM VIỆC!\n\n";
                        MessageBox.Show($"{prefix}" +
                                        $"• Người thực hiện: {_editorFullName}\n" +
                                        $"• Trạng thái ca: Đã chốt (Có chỉnh sửa - ClosedNC)\n" +
                                        $"• Nội dung sửa: {changeSummary}\n" +
                                        $"• Ca làm việc đã được khóa lại an toàn.",
                                        title, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    else
                    {
                        string err = await apiRes.Content.ReadAsStringAsync();
                        MessageBox.Show($"Lỗi lưu thay đổi: {err}", "Lỗi cập nhật", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi API UpdateClosedShift: " + ex.Message);
                }

                _isEditingClosedShift = false;
                _hasChangedInitialData = true;
                ApplyShiftStatus(StatusClosedNC);
                MessageBox.Show("Đã lưu thay đổi thông tin ca thành công (Chế độ Ngoại tuyến)!\nCa làm việc đã được khóa lại.", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TÌNH HUỐNG 2: Ca mới mở NConfirm sửa đầu ca
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
                    _hasChangedInitialData = true;
                    ApplyShiftStatus(StatusConfirmStartNC);
                    string msg = isTimeoutAutoSave
                        ? "ĐÃ HẾT THỜI GIAN 2 PHÚT!\nHệ thống đã tự động lưu thông tin đầu ca đã thay đổi vào Database và khóa lại.\nTrạng thái ca chuyển thành 'ConfirmStartNC'. Ca làm việc chính thức bắt đầu."
                        : "Đã xác nhận thay đổi thông tin đầu ca và lưu vào Database thành công!\nTrạng thái ca chuyển thành 'ConfirmStartNC'. Ca làm việc chính thức bắt đầu.";
                    MessageBox.Show(msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi API ConfirmChange: " + ex.Message);
            }

            // Fallback giao diện nếu không có mạng
            _hasChangedInitialData = true;
            ApplyShiftStatus(StatusConfirmStartNC);
            string fallbackMsg = isTimeoutAutoSave
                ? "ĐÃ HẾT THỜI GIAN 2 PHÚT!\nHệ thống đã tự động lưu thông tin đầu ca (Ngoại tuyến).\nTrạng thái ca chuyển thành 'ConfirmStartNC'. Ca làm việc chính thức bắt đầu."
                : "Đã xác nhận thay đổi thông tin đầu ca thành công!\nTrạng thái ca chuyển thành 'ConfirmStartNC'. Ca làm việc chính thức bắt đầu.";
            MessageBox.Show(fallbackMsg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSaveHandover_Click(object sender, RoutedEventArgs e)
        {
            // =========================================================================
            // GIAI ĐOẠN 0: Changed -> Nhân viên bấm "XÁC NHẬN THAY ĐỔI" sau khi chỉnh sửa
            // =========================================================================
            if (CurrentShiftStatus == StatusChanged)
            {
                StopCountdownTimer();
                await SaveChangedShiftDataAsync(isTimeoutAutoSave: false);
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
                                    "Sau khi bấm 'Đồng ý', hệ thống sẽ ghi nhận trạng thái 'ConfirmStart' vào Database và mở khóa các ô nhập liệu.";

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
                            MessageBox.Show("Đã xác nhận dữ liệu đầu ca thành công lên Database!\nCác ô nhập liệu đã được mở khóa để bạn làm việc trong ca.", 
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
                    MessageBox.Show("Đã xác nhận dữ liệu đầu ca thành công!\nCác ô nhập liệu đã được mở khóa để bạn làm việc trong ca.", 
                                    "Thông báo nhận ca", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            // =========================================================================
            // GIAI ĐOẠN 2: ConfirmStart / ConfirmStartNC -> Nhân viên bấm Hoàn tất & Chốt ca khi hết ca
            // =========================================================================
            if (CurrentShiftStatus == StatusConfirmStart || CurrentShiftStatus == StatusConfirmStartNC)
            {
                // Kiểm tra validation cho Doanh số App bán hàng và Chuyển khoản:
                // Số cuối ca phải lớn hơn hoặc bằng số đầu ca
                decimal pos1Opening = ParseMoney(txtPos1Opening?.Text ?? "0");
                decimal pos1Closing = ParseMoney(txtPos1Closing?.Text ?? "0");

                decimal pos2Opening = ParseMoney(txtPos2Opening?.Text ?? "0");
                decimal pos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0");

                decimal bank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
                decimal bank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0");

                decimal bank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
                decimal bank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0");

                var validationErrors = new List<string>();

                if (pos1Closing < pos1Opening)
                {
                    validationErrors.Add($"• Sapo POS (App bán hàng): Cuối ca ({FormatMoney(pos1Closing)} đ) < Đầu ca ({FormatMoney(pos1Opening)} đ)");
                }

                if (pos2Closing < pos2Opening)
                {
                    validationErrors.Add($"• KiotViet (App bán hàng): Cuối ca ({FormatMoney(pos2Closing)} đ) < Đầu ca ({FormatMoney(pos2Opening)} đ)");
                }

                if (bank1Closing < bank1Opening)
                {
                    validationErrors.Add($"• TingTing (Chuyển khoản): Cuối ca ({FormatMoney(bank1Closing)} đ) < Đầu ca ({FormatMoney(bank1Opening)} đ)");
                }

                if (bank2Closing < bank2Opening)
                {
                    validationErrors.Add($"• Zalo Pay (Chuyển khoản): Cuối ca ({FormatMoney(bank2Closing)} đ) < Đầu ca ({FormatMoney(bank2Opening)} đ)");
                }

                if (validationErrors.Count > 0)
                {
                    string errorMsg = "KHÔNG THỂ HOÀN TẤT VÀ CHỐT CA!\n\n" +
                                      "Giá trị Cuối ca của 'Doanh số App bán hàng' và 'Chuyển khoản' phải lớn hơn hoặc bằng giá trị Đầu ca.\n\n" +
                                      "Các mục đang bị sai lệch:\n" +
                                      string.Join("\n", validationErrors) + "\n\n" +
                                      "Vui lòng kiểm tra và nhập lại số liệu cuối ca trước khi chốt!";

                    MessageBox.Show(errorMsg, "Lỗi số liệu chốt ca — Plus Mart", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Focus vào ô sai đầu tiên
                    if (pos1Closing < pos1Opening) txtPos1Closing?.Focus();
                    else if (pos2Closing < pos2Opening) txtPos2Closing?.Focus();
                    else if (bank1Closing < bank1Opening) txtBank1Closing?.Focus();
                    else if (bank2Closing < bank2Opening) txtBank2Closing?.Focus();

                    return;
                }

                string diff = txtCashDifference?.Text ?? "0 đ";
                string closingCash = (txtCashClosing?.Text ?? "0") + " đ";
                string totalPos = (txtTotalPosRevenue?.Text ?? "0") + " đ";
                string totalBank = (txtTotalBankRevenue?.Text ?? "0") + " đ";

                // Mở Dialog Pop-up ký chữ ký điện tử (tài khoản & mật khẩu) cho 1 hoặc 2 nhân viên
                var signDialog = new ShiftClosingSignatureDialog("", closingCash, totalPos, totalBank, diff)
                {
                    Owner = this
                };

                bool? dialogResult = signDialog.ShowDialog();
                if (dialogResult != true || signDialog.Signatures.Count == 0)
                {
                    return; // Người dùng hủy bỏ hoặc không nhập
                }

                try
                {
                    // Ghép chuỗi ghi chú khi kết ca
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
                        Pos1Night = IsNightShift ? ParseMoney(txtPos1Night?.Text ?? "0") : 0,
                        Pos2Closing = ParseMoney(txtPos2Closing?.Text ?? "0"),
                        Pos2Night = IsNightShift ? ParseMoney(txtPos2Night?.Text ?? "0") : 0,
                        Bank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0"),
                        Bank1Night = IsNightShift ? ParseMoney(txtBank1Night?.Text ?? "0") : 0,
                        Bank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0"),
                        Bank2Night = IsNightShift ? ParseMoney(txtBank2Night?.Text ?? "0") : 0,
                        Note = finalNote,
                        Signatures = signDialog.Signatures
                    };

                    if (pnlExpenseItems != null)
                    {
                        foreach (UIElement child in pnlExpenseItems.Children)
                        {
                            if (child is Grid g && g.Children.Count >= 2 && g.Children[0] is TextBox tbDesc && g.Children[1] is TextBox tbAmt)
                            {
                                decimal amt = ParseMoney(tbAmt.Text);
                                if (!string.IsNullOrWhiteSpace(tbDesc.Text) && amt > 0)
                                {
                                    closeReq.Expenses.Add(new ShiftExpenseItemDTO
                                    {
                                        Description = tbDesc.Text.Trim(),
                                        Amount = amt
                                    });
                                }
                            }
                        }
                    }

                    var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/close", closeReq);
                    if (apiRes.IsSuccessStatusCode)
                    {
                        var closeRes = await apiRes.Content.ReadFromJsonAsync<CloseShiftResponseDTO>();
                        if (closeRes?.ConfirmedEmployeeNames != null && closeRes.ConfirmedEmployeeNames.Count > 0)
                        {
                            _closingEmployeeNames = new System.Collections.Generic.List<string>(closeRes.ConfirmedEmployeeNames);
                        }
                        string empList = _closingEmployeeNames.Count > 0
                                         ? string.Join(", ", _closingEmployeeNames)
                                         : "Nhân viên trực ca";

                        string finalStatus = _hasChangedInitialData ? StatusClosedNC : StatusClosed;
                        ApplyShiftStatus(finalStatus);
                        MessageBox.Show($"ĐÃ CHỐT CA VÀ KÝ XÁC NHẬN THÀNH CÔNG!\n\n" +
                                        $"• Nhân sự tham gia trực ca: {empList}\n" +
                                        $"• Trạng thái ca: {(_hasChangedInitialData ? "Đã chốt (Có chỉnh sửa - ClosedNC)" : "Đã chốt chuẩn (Closed)")}\n" +
                                        $"• Số ca làm và kết quả âm dương ({diff}) đã được ghi nhận cho tất cả nhân sự tham gia.\n" +
                                        $"• Số liệu đã được khóa an toàn trong hệ thống.", 
                                        "Chốt ca hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    else
                    {
                        var errContent = await apiRes.Content.ReadAsStringAsync();
                        MessageBox.Show($"KHÔNG THỂ CHỐT CA:\n{errContent}", 
                                        "Lỗi xác thực chữ ký", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi API CloseShift: " + ex.Message);
                }

                // Fallback giao diện nếu mất kết nối mạng
                string fallbackStatus = _hasChangedInitialData ? StatusClosedNC : StatusClosed;
                ApplyShiftStatus(fallbackStatus);
                MessageBox.Show("Đã chốt ca thành công (Chế độ Ngoại tuyến)!", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShiftStatus != StatusConfirmStart && CurrentShiftStatus != StatusConfirmStartNC) return;

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
            var result = MessageBox.Show("Bạn có chắc chắn muốn thoát khỏi trang chốt ca không?\n\nLưu ý: Mọi số liệu chưa bấm Chốt ca sẽ không được lưu.", 
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

            if (status == StatusClosed || status == StatusClosedNC)
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
                    btnSaveHandover.Content = "XÁC NHẬN DỮ LIỆU ĐẦU CA";
                    btnSaveHandover.Background = Brushes.Black;
                    btnSaveHandover.Foreground = Brushes.White;
                }

                // 4. Khóa toàn bộ các ô nhập liệu & ô đầu ca
                SetInputsEditable(false);
                SetOpeningInputsEditable(false);

                // 5. Các nút tác vụ bổ sung bị khóa
                if (btnOpenCashPopup != null) btnOpenCashPopup.IsEnabled = false;
                if (btnCalculateCashHeader != null) btnCalculateCashHeader.IsEnabled = false;
                if (btnAddExpense != null) btnAddExpense.IsEnabled = false;
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
                    btnSaveHandover.Content = "XÁC NHẬN THAY ĐỔI";
                    btnSaveHandover.Background = Brushes.Black;
                    btnSaveHandover.Foreground = Brushes.White;
                }

                // 4. Khóa các ô cuối ca trong lúc sửa đầu ca
                SetInputsEditable(false);

                // 5. MỞ KHÓA CÁC Ô ĐẦU CA để nhân viên sửa
                SetOpeningInputsEditable(true);

                // 5. Bắt đầu đếm ngược 2 phút
                StartCountdownTimer();
            }
            else if (status == StatusConfirmStart || status == StatusConfirmStartNC)
            {
                if (status == StatusConfirmStartNC)
                {
                    _hasChangedInitialData = true;
                }
                StopCountdownTimer();

                // 1. Ẩn tất cả banner khóa
                if (bdrNConfirmNotice != null) bdrNConfirmNotice.Visibility = Visibility.Collapsed;
                if (bdrChangedNotice != null) bdrChangedNotice.Visibility = Visibility.Collapsed;
                if (bdrReadOnlyNotice != null) bdrReadOnlyNotice.Visibility = Visibility.Collapsed;

                // 2. Ẩn nút 'Thay đổi thông tin'
                if (btnChangeInitialData != null) btnChangeInitialData.Visibility = Visibility.Collapsed;

                // 3. Nút hành động chuyển sang: HOÀN TẤT VÀ CHỐT CA
                if (btnSaveHandover != null)
                {
                    btnSaveHandover.IsEnabled = true;
                    btnSaveHandover.Content = "HOÀN TẤT VÀ CHỐT CA";
                    btnSaveHandover.Background = Brushes.Black;
                    btnSaveHandover.Foreground = Brushes.White;
                }

                // 4. Khóa vĩnh viễn các ô đầu ca & Mở khóa các ô nhập trong ca
                SetOpeningInputsEditable(false);
                SetInputsEditable(true);

                // 5. Các nút tác vụ bổ sung được mở
                if (btnOpenCashPopup != null) btnOpenCashPopup.IsEnabled = true;
                if (btnCalculateCashHeader != null) btnCalculateCashHeader.IsEnabled = true;
                if (btnAddExpense != null) btnAddExpense.IsEnabled = true;
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
                    if (popCashCounter != null) popCashCounter.Visibility = Visibility.Collapsed;
                    _ = SaveChangedShiftDataAsync(isTimeoutAutoSave: true);
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
        /// Khóa hoặc Mở khóa các ô thông tin đầu ca (Đen Trắng)
        /// </summary>
        private void SetOpeningInputsEditable(bool isEditable)
        {
            var normalBg = Brushes.White;
            var editBg = Brushes.White;
            var editBorder = Brushes.Black;
            var normalBorder = Brushes.Black;

            TextBox?[] openingBoxes = { txtPos1Opening, txtPos2Opening, txtBank1Opening, txtBank2Opening, txtCashOpening };
            foreach (var tb in openingBoxes)
            {
                if (tb == null) continue;
                tb.IsReadOnly = !isEditable;
                tb.Focusable = true;
                tb.IsHitTestVisible = true;
                tb.Foreground = Brushes.Black;
                tb.Background = isEditable ? editBg : normalBg;
                tb.BorderBrush = isEditable ? editBorder : normalBorder;
                tb.BorderThickness = isEditable ? new Thickness(2) : new Thickness(1);
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
            if (CurrentShiftStatus != StatusClosedNC && _hasChangedInitialData)
            {
                CurrentShiftStatus = StatusClosedNC;
            }
            else if (CurrentShiftStatus != StatusClosedNC)
            {
                CurrentShiftStatus = StatusClosed;
            }

            if (bdrReadOnlyNotice != null)
            {
                bdrReadOnlyNotice.Visibility = Visibility.Visible;
                UpdateReadOnlyBannerInfo();
            }
            if (bdrNConfirmNotice != null)
                bdrNConfirmNotice.Visibility = Visibility.Collapsed;
            if (bdrChangedNotice != null)
                bdrChangedNotice.Visibility = Visibility.Collapsed;

            // Kiểm soát nút "Thay đổi thông tin" (chỉ được thay đổi duy nhất một lần)
            if (btnChangeInitialData != null)
            {
                if (CurrentShiftStatus == StatusClosedNC || _hasChangedInitialData)
                {
                    btnChangeInitialData.Visibility = Visibility.Visible;
                    btnChangeInitialData.IsEnabled = false;
                    btnChangeInitialData.Content = "Đã sửa (Chỉ đổi 1 lần)";
                    btnChangeInitialData.Background = Brushes.White;
                    btnChangeInitialData.Foreground = Brushes.Black;
                    btnChangeInitialData.BorderBrush = Brushes.Black;
                    btnChangeInitialData.ToolTip = "Ca làm việc này đã được điều chỉnh 1 lần trước đó, không thể thay đổi thêm nữa.";
                }
                else
                {
                    btnChangeInitialData.Visibility = Visibility.Visible;
                    btnChangeInitialData.IsEnabled = true;
                    btnChangeInitialData.Content = "Thay đổi thông tin";
                    btnChangeInitialData.Background = Brushes.White;
                    btnChangeInitialData.Foreground = Brushes.Black;
                    btnChangeInitialData.BorderBrush = Brushes.Black;
                    btnChangeInitialData.ToolTip = "Chỉ người phụ trách ca mới được điều chỉnh thông tin (duy nhất 1 lần).";
                }
            }

            // 1. Khóa các nút hành động
            if (btnSaveHandover != null)
            {
                btnSaveHandover.IsEnabled = false;
                if (CurrentShiftStatus == StatusClosedNC)
                {
                    btnSaveHandover.Content = "CA ĐÃ ĐÓNG (ĐÃ SỬA - NC)";
                    btnSaveHandover.Background = Brushes.Black;
                    btnSaveHandover.Foreground = Brushes.White;
                }
                else
                {
                    btnSaveHandover.Content = "CA ĐÃ ĐÓNG / CHỈ XEM";
                    btnSaveHandover.Background = Brushes.Black;
                    btnSaveHandover.Foreground = Brushes.White;
                }
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

            // 2. Khóa tất cả các ô nhập liệu & ô đầu ca
            SetInputsEditable(false);
            SetOpeningInputsEditable(false);

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

        /// <summary>
        /// Cập nhật thông tin chi tiết trên banner ca đã chốt: Tên cơ sở, Ca nào, Ngày nào, Nhân viên nào chốt ca
        /// </summary>
        public void UpdateReadOnlyBannerInfo()
        {
            if (lblReadOnlyBranch != null)
                lblReadOnlyBranch.Text = !string.IsNullOrEmpty(_branchName) ? _branchName : $"Cơ sở {_branchId}";

            if (lblReadOnlyShift != null)
                lblReadOnlyShift.Text = !string.IsNullOrEmpty(_shiftName) ? _shiftName : _shiftCode;

            if (lblReadOnlyDate != null)
                lblReadOnlyDate.Text = _workDate.ToString("dd/MM/yyyy");

            if (lblReadOnlyEmployees != null)
            {
                if (_closingEmployeeNames != null && _closingEmployeeNames.Count > 0)
                {
                    lblReadOnlyEmployees.Text = string.Join(", ", _closingEmployeeNames);
                }
                else if (!string.IsNullOrEmpty(_employeeName))
                {
                    lblReadOnlyEmployees.Text = _employeeName;
                }
                else
                {
                    lblReadOnlyEmployees.Text = "Nhân viên trực ca";
                }
            }
        }
        #endregion
    }
}
