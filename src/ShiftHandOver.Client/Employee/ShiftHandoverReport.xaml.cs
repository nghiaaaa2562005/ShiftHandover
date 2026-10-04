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

            // POS 3
            if (colPos3OpenL != null) colPos3OpenL.Width = colLabelWidth;
            if (colPos3OpenV != null) colPos3OpenV.Width = colValueWidth;
            if (colPos3CloseL != null) colPos3CloseL.Width = colLabelWidth;
            if (colPos3CloseV != null) colPos3CloseV.Width = colValueWidth;
            if (lblPos3Night != null) lblPos3Night.Visibility = nightVisibility;
            if (txtPos3Night != null)
            {
                txtPos3Night.Visibility = nightVisibility;
                if (!isNight) txtPos3Night.Text = "0";
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

            // Ngân hàng 3
            if (colBank3OpenL != null) colBank3OpenL.Width = colLabelWidth;
            if (colBank3OpenV != null) colBank3OpenV.Width = colValueWidth;
            if (colBank3CloseL != null) colBank3CloseL.Width = colLabelWidth;
            if (colBank3CloseV != null) colBank3CloseV.Width = colValueWidth;
            if (lblBank3Night != null) lblBank3Night.Visibility = nightVisibility;
            if (txtBank3Night != null)
            {
                txtBank3Night.Visibility = nightVisibility;
                if (!isNight) txtBank3Night.Text = "0";
            }
        }

        // Tên và trạng thái cấu hình cổng POS & Ngân hàng
        private string _pos1Name = "Sapo POS";
        private bool _pos1Active = true;
        private string _pos2Name = "KiotViet";
        private bool _pos2Active = true;
        private string _pos3Name = "";
        private bool _pos3Active = false;

        private string _bank1Name = "TingTing";
        private bool _bank1Active = true;
        private string _bank2Name = "Zalo Pay";
        private bool _bank2Active = true;
        private string _bank3Name = "Ngân hàng 3";
        private bool _bank3Active = false;

        // Lưu vết số liệu ban đầu để kiểm tra phần nào đã bị thay đổi
        private decimal _originalCashOpening = 0;
        private decimal _originalCashClosing = 0;
        private decimal _originalPos1Opening = 0;
        private decimal _originalPos1Closing = 0;
        private decimal _originalPos1Night = 0;
        private decimal _originalPos2Opening = 0;
        private decimal _originalPos2Closing = 0;
        private decimal _originalPos2Night = 0;
        private decimal _originalPos3Opening = 0;
        private decimal _originalPos3Closing = 0;
        private decimal _originalPos3Night = 0;
        private decimal _originalBank1Opening = 0;
        private decimal _originalBank1Closing = 0;
        private decimal _originalBank1Night = 0;
        private decimal _originalBank2Opening = 0;
        private decimal _originalBank2Closing = 0;
        private decimal _originalBank2Night = 0;
        private decimal _originalBank3Opening = 0;
        private decimal _originalBank3Closing = 0;
        private decimal _originalBank3Night = 0;
        private string _originalNote = "";
        private string _rawShiftNote = "";

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
                        // Cập nhật tên và trạng thái cấu hình POS & Ngân hàng
                        _pos1Name = !string.IsNullOrWhiteSpace(shiftDetail.Pos1Name) ? shiftDetail.Pos1Name : "Sapo POS";
                        _pos2Name = !string.IsNullOrWhiteSpace(shiftDetail.Pos2Name) ? shiftDetail.Pos2Name : "KiotViet";
                        _pos3Name = shiftDetail.Pos3Name ?? "";
                        _pos1Active = shiftDetail.Pos1IsActive;
                        _pos2Active = shiftDetail.Pos2IsActive;
                        _pos3Active = shiftDetail.Pos3IsActive;

                        if (lblPos1Name != null) lblPos1Name.Text = _pos1Name;
                        if (lblPos2Name != null) lblPos2Name.Text = _pos2Name;
                        if (lblPos3Name != null) lblPos3Name.Text = _pos3Name;

                        if (pnlPos1Group != null) pnlPos1Group.Visibility = _pos1Active ? Visibility.Visible : Visibility.Collapsed;
                        if (pnlPos2Group != null) pnlPos2Group.Visibility = _pos2Active ? Visibility.Visible : Visibility.Collapsed;
                        if (pnlPos3Group != null) pnlPos3Group.Visibility = _pos3Active ? Visibility.Visible : Visibility.Collapsed;
                        if (sepPos1 != null) sepPos1.Visibility = (_pos1Active && (_pos2Active || _pos3Active)) ? Visibility.Visible : Visibility.Collapsed;
                        if (sepPos2 != null) sepPos2.Visibility = (_pos2Active && _pos3Active) ? Visibility.Visible : Visibility.Collapsed;

                        _bank1Name = !string.IsNullOrWhiteSpace(shiftDetail.Bank1Name) ? shiftDetail.Bank1Name : "TingTing";
                        _bank2Name = !string.IsNullOrWhiteSpace(shiftDetail.Bank2Name) ? shiftDetail.Bank2Name : "Zalo Pay";
                        _bank3Name = !string.IsNullOrWhiteSpace(shiftDetail.Bank3Name) ? shiftDetail.Bank3Name : "Ngân hàng 3";
                        _bank1Active = shiftDetail.Bank1IsActive;
                        _bank2Active = shiftDetail.Bank2IsActive;
                        _bank3Active = shiftDetail.Bank3IsActive;

                        if (lblBank1Name != null) lblBank1Name.Text = _bank1Name;
                        if (lblBank2Name != null) lblBank2Name.Text = _bank2Name;
                        if (lblBank3Name != null) lblBank3Name.Text = _bank3Name;

                        if (pnlBank1Group != null) pnlBank1Group.Visibility = _bank1Active ? Visibility.Visible : Visibility.Collapsed;
                        if (pnlBank2Group != null) pnlBank2Group.Visibility = _bank2Active ? Visibility.Visible : Visibility.Collapsed;
                        if (pnlBank3Group != null) pnlBank3Group.Visibility = _bank3Active ? Visibility.Visible : Visibility.Collapsed;
                        if (sepBank != null) sepBank.Visibility = (_bank1Active && _bank2Active) ? Visibility.Visible : Visibility.Collapsed;
                        if (sepBank2 != null) sepBank2.Visibility = ((_bank1Active || _bank2Active) && _bank3Active) ? Visibility.Visible : Visibility.Collapsed;

                        ApplyNightShiftVisibility();

                        // 1. Điền các thông tin đầu ca (cố định, lấy từ DB / ca trước)
                        if (txtPos1Opening != null) txtPos1Opening.Text = FormatMoney(shiftDetail.Pos1Opening);
                        if (txtPos2Opening != null) txtPos2Opening.Text = FormatMoney(shiftDetail.Pos2Opening);
                        if (txtPos3Opening != null) txtPos3Opening.Text = FormatMoney(shiftDetail.Pos3Opening);
                        if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(shiftDetail.Bank1Opening);
                        if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(shiftDetail.Bank2Opening);
                        if (txtBank3Opening != null) txtBank3Opening.Text = FormatMoney(shiftDetail.Bank3Opening);
                        if (txtCashOpening != null) txtCashOpening.Text = FormatMoney(shiftDetail.CashOpening);

                        // Lưu lại số liệu gốc ban đầu
                        _originalPos1Opening = shiftDetail.Pos1Opening;
                        _originalPos2Opening = shiftDetail.Pos2Opening;
                        _originalPos3Opening = shiftDetail.Pos3Opening;
                        _originalBank1Opening = shiftDetail.Bank1Opening;
                        _originalBank2Opening = shiftDetail.Bank2Opening;
                        _originalBank3Opening = shiftDetail.Bank3Opening;
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

                        if (shiftDetail.Pos3Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Pos3Closing.Value > 0) && txtPos3Closing != null)
                            txtPos3Closing.Text = FormatMoney(shiftDetail.Pos3Closing.Value);
                        else if (txtPos3Closing != null)
                            txtPos3Closing.Text = FormatMoney(shiftDetail.Pos3Opening);

                        if (shiftDetail.Pos3Night.HasValue && txtPos3Night != null && IsNightShift)
                            txtPos3Night.Text = FormatMoney(shiftDetail.Pos3Night.Value);

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

                        if (shiftDetail.Bank3Closing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.Bank3Closing.Value > 0) && txtBank3Closing != null)
                            txtBank3Closing.Text = FormatMoney(shiftDetail.Bank3Closing.Value);
                        else if (txtBank3Closing != null)
                            txtBank3Closing.Text = FormatMoney(shiftDetail.Bank3Opening);

                        if (shiftDetail.Bank3Night.HasValue && txtBank3Night != null && IsNightShift)
                            txtBank3Night.Text = FormatMoney(shiftDetail.Bank3Night.Value);

                        if (shiftDetail.CashClosing.HasValue && (shiftDetail.Status == "Closed" || shiftDetail.CashClosing.Value > 0) && txtCashClosing != null)
                            txtCashClosing.Text = FormatMoney(shiftDetail.CashClosing.Value);
                        else if (txtCashClosing != null)
                            txtCashClosing.Text = FormatMoney(shiftDetail.CashOpening);

                        if (!string.IsNullOrEmpty(shiftDetail.Note))
                        {
                            _rawShiftNote = shiftDetail.Note;
                            string rawNote = shiftDetail.Note;
                            int idxUserNote = rawNote.IndexOf("Ghi chú nhân viên:");
                            if (idxUserNote < 0) idxUserNote = rawNote.IndexOf("Ghi chú:");
                            if (idxUserNote >= 0)
                            {
                                int labelLen = rawNote.IndexOf("Ghi chú nhân viên:") >= 0 ? "Ghi chú nhân viên:".Length : "Ghi chú:".Length;
                                _hiddenAuditChangeLog = rawNote.Substring(0, idxUserNote).Trim().TrimEnd('|', ' ');
                                if (txtNote != null)
                                    txtNote.Text = rawNote.Substring(idxUserNote + labelLen).Trim();
                            }
                            else if (rawNote.StartsWith("Nhân viên đã thay đổi") || rawNote.Contains("Đã sửa đầu ca") || rawNote.Contains("Đầu ca:") || rawNote.Contains("Cuối ca:"))
                            {
                                _hiddenAuditChangeLog = rawNote.Trim();
                                if (txtNote != null) txtNote.Text = ""; // Ẩn hoàn toàn khỏi ô ghi chú của nhân viên
                            }
                            else
                            {
                                if (txtNote != null) txtNote.Text = rawNote;
                            }
                        }
                        else
                        {
                            _rawShiftNote = "";
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
                            ApplyShiftStatus(shiftDetail.Status ?? StatusNConfirm);
                        }
                        return;
                    }
                }
                else
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
                    this.Close();
                    return;
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
        public void SetOpeningData(decimal pos1Opening, decimal pos2Opening, decimal bank1Opening, decimal bank2Opening, decimal cashOpening, decimal pos3Opening = 0, decimal bank3Opening = 0)
        {
            if (txtPos1Opening != null) txtPos1Opening.Text = FormatMoney(pos1Opening);
            if (txtPos2Opening != null) txtPos2Opening.Text = FormatMoney(pos2Opening);
            if (txtPos3Opening != null) txtPos3Opening.Text = FormatMoney(pos3Opening);
            if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(bank1Opening);
            if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(bank2Opening);
            if (txtBank3Opening != null) txtBank3Opening.Text = FormatMoney(bank3Opening);
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

            // 1. POS 1: (Kết ca - Đầu ca) + Đêm
            decimal pos1Opening = _pos1Active ? ParseMoney(txtPos1Opening?.Text ?? "0") : 0;
            decimal pos1Closing = _pos1Active ? ParseMoney(txtPos1Closing?.Text ?? "0") : 0;
            decimal pos1Night   = (_pos1Active && IsNightShift) ? ParseMoney(txtPos1Night?.Text ?? "0") : 0;
            decimal pos1Diff    = pos1Closing - pos1Opening;
            decimal pos1Revenue = _pos1Active ? ((pos1Diff >= 0 ? pos1Diff : 0) + pos1Night) : 0;

            // 2. POS 2: (Kết ca - Đầu ca) + Đêm
            decimal pos2Opening = _pos2Active ? ParseMoney(txtPos2Opening?.Text ?? "0") : 0;
            decimal pos2Closing = _pos2Active ? ParseMoney(txtPos2Closing?.Text ?? "0") : 0;
            decimal pos2Night   = (_pos2Active && IsNightShift) ? ParseMoney(txtPos2Night?.Text ?? "0") : 0;
            decimal pos2Diff    = pos2Closing - pos2Opening;
            decimal pos2Revenue = _pos2Active ? ((pos2Diff >= 0 ? pos2Diff : 0) + pos2Night) : 0;

            // 2b. POS 3: (Kết ca - Đầu ca) + Đêm
            decimal pos3Opening = _pos3Active ? ParseMoney(txtPos3Opening?.Text ?? "0") : 0;
            decimal pos3Closing = _pos3Active ? ParseMoney(txtPos3Closing?.Text ?? "0") : 0;
            decimal pos3Night   = (_pos3Active && IsNightShift) ? ParseMoney(txtPos3Night?.Text ?? "0") : 0;
            decimal pos3Diff    = pos3Closing - pos3Opening;
            decimal pos3Revenue = _pos3Active ? ((pos3Diff >= 0 ? pos3Diff : 0) + pos3Night) : 0;

            // 3. TỔNG DOANH SỐ APP TRONG CA (DOANH THU)
            decimal totalPosRevenue = pos1Revenue + pos2Revenue + pos3Revenue;
            if (txtTotalPosRevenue != null) txtTotalPosRevenue.Text = FormatMoney(totalPosRevenue);

            // 4. CHUYỂN KHOẢN NGÂN HÀNG 1: (Kết ca - Đầu ca) + Đêm
            decimal bank1Opening = _bank1Active ? ParseMoney(txtBank1Opening?.Text ?? "0") : 0;
            decimal bank1Closing = _bank1Active ? ParseMoney(txtBank1Closing?.Text ?? "0") : 0;
            decimal bank1Night   = (_bank1Active && IsNightShift) ? ParseMoney(txtBank1Night?.Text ?? "0") : 0;
            decimal bank1Diff    = bank1Closing - bank1Opening;
            decimal bank1Revenue = _bank1Active ? ((bank1Diff >= 0 ? bank1Diff : 0) + bank1Night) : 0;

            // 5. CHUYỂN KHOẢN NGÂN HÀNG 2: (Kết ca - Đầu ca) + Đêm
            decimal bank2Opening = _bank2Active ? ParseMoney(txtBank2Opening?.Text ?? "0") : 0;
            decimal bank2Closing = _bank2Active ? ParseMoney(txtBank2Closing?.Text ?? "0") : 0;
            decimal bank2Night   = (_bank2Active && IsNightShift) ? ParseMoney(txtBank2Night?.Text ?? "0") : 0;
            decimal bank2Diff    = bank2Closing - bank2Opening;
            decimal bank2Revenue = _bank2Active ? ((bank2Diff >= 0 ? bank2Diff : 0) + bank2Night) : 0;

            // 5b. CHUYỂN KHOẢN NGÂN HÀNG 3: (Kết ca - Đầu ca) + Đêm
            decimal bank3Opening = _bank3Active ? ParseMoney(txtBank3Opening?.Text ?? "0") : 0;
            decimal bank3Closing = _bank3Active ? ParseMoney(txtBank3Closing?.Text ?? "0") : 0;
            decimal bank3Night   = (_bank3Active && IsNightShift) ? ParseMoney(txtBank3Night?.Text ?? "0") : 0;
            decimal bank3Diff    = bank3Closing - bank3Opening;
            decimal bank3Revenue = _bank3Active ? ((bank3Diff >= 0 ? bank3Diff : 0) + bank3Night) : 0;

            // Cảnh báo viền đậm trực quan nếu cuối ca < đầu ca
            HighlightInvalidClosing(txtPos1Closing, _pos1Active && pos1Closing < pos1Opening);
            HighlightInvalidClosing(txtPos2Closing, _pos2Active && pos2Closing < pos2Opening);
            HighlightInvalidClosing(txtPos3Closing, _pos3Active && pos3Closing < pos3Opening);
            HighlightInvalidClosing(txtBank1Closing, _bank1Active && bank1Closing < bank1Opening);
            HighlightInvalidClosing(txtBank2Closing, _bank2Active && bank2Closing < bank2Opening);
            HighlightInvalidClosing(txtBank3Closing, _bank3Active && bank3Closing < bank3Opening);

            // 6. TỔNG TIỀN CHUYỂN KHOẢN TRONG CA
            decimal totalBankRevenue = bank1Revenue + bank2Revenue + bank3Revenue;
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

            _originalPos3Opening = ParseMoney(txtPos3Opening?.Text ?? "0");
            _originalPos3Closing = ParseMoney(txtPos3Closing?.Text ?? "0");
            _originalPos3Night   = ParseMoney(txtPos3Night?.Text ?? "0");

            _originalBank1Opening = ParseMoney(txtBank1Opening?.Text ?? "0");
            _originalBank1Closing = ParseMoney(txtBank1Closing?.Text ?? "0");
            _originalBank1Night   = ParseMoney(txtBank1Night?.Text ?? "0");

            _originalBank2Opening = ParseMoney(txtBank2Opening?.Text ?? "0");
            _originalBank2Closing = ParseMoney(txtBank2Closing?.Text ?? "0");
            _originalBank2Night   = ParseMoney(txtBank2Night?.Text ?? "0");

            _originalBank3Opening = ParseMoney(txtBank3Opening?.Text ?? "0");
            _originalBank3Closing = ParseMoney(txtBank3Closing?.Text ?? "0");
            _originalBank3Night   = ParseMoney(txtBank3Night?.Text ?? "0");

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

            if (txtPos3Opening != null) txtPos3Opening.Text = FormatMoney(_originalPos3Opening);
            if (txtPos3Closing != null) txtPos3Closing.Text = FormatMoney(_originalPos3Closing);
            if (txtPos3Night != null) txtPos3Night.Text = FormatMoney(_originalPos3Night);

            if (txtBank1Opening != null) txtBank1Opening.Text = FormatMoney(_originalBank1Opening);
            if (txtBank1Closing != null) txtBank1Closing.Text = FormatMoney(_originalBank1Closing);
            if (txtBank1Night != null) txtBank1Night.Text = FormatMoney(_originalBank1Night);

            if (txtBank2Opening != null) txtBank2Opening.Text = FormatMoney(_originalBank2Opening);
            if (txtBank2Closing != null) txtBank2Closing.Text = FormatMoney(_originalBank2Closing);
            if (txtBank2Night != null) txtBank2Night.Text = FormatMoney(_originalBank2Night);

            if (txtBank3Opening != null) txtBank3Opening.Text = FormatMoney(_originalBank3Opening);
            if (txtBank3Closing != null) txtBank3Closing.Text = FormatMoney(_originalBank3Closing);
            if (txtBank3Night != null) txtBank3Night.Text = FormatMoney(_originalBank3Night);

            if (txtNote != null && _originalNote != null) txtNote.Text = _originalNote;

            CalculateAll(null, null);
        }

        private async void BtnChangeInitialData_Click(object sender, RoutedEventArgs e)
        {
            bool isClosedShift = CurrentShiftStatus == StatusClosed || CurrentShiftStatus == StatusClosedNC || IsReadOnlyMode;

            // TÌNH HUỐNG 1: Ca đã xong (StatusClosed hoặc StatusClosedNC hoặc IsReadOnlyMode) -> Sửa sau khi chốt ca
            if (isClosedShift)
            {
                // Kiểm tra: Ca này đã từng sửa sau khi chốt ca chưa?
                bool hasEditedClosing = !string.IsNullOrEmpty(_rawShiftNote) &&
                    (_rawShiftNote.IndexOf("Cuối ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     _rawShiftNote.IndexOf("đã chỉnh sửa", StringComparison.OrdinalIgnoreCase) >= 0);

                if (hasEditedClosing)
                {
                    MessageBox.Show("Ca làm việc này đã được chỉnh sửa sau khi chốt ca một lần rồi và không thể điều chỉnh thêm nữa!", 
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
            // Kiểm tra: Chỉ được sửa đầu ca duy nhất 1 lần
            if (_hasChangedInitialData || CurrentShiftStatus == StatusConfirmStartNC || 
                (CurrentShiftStatus != null && CurrentShiftStatus.EndsWith("NC", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Thông tin đầu ca đã được thay đổi một lần, không thể điều chỉnh thêm nữa!", 
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmRes = MessageBox.Show(
                "Bạn có muốn hủy không?\n\n" +
                "• Bấm Cancel: Hoàn tác và hủy bỏ để không bị ấn nhầm.\n" +
                "• Bấm OK: Bắt buộc thay đổi thông tin (không thể hoàn tác).",
                "Thông báo",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirmRes == MessageBoxResult.Cancel)
            {
                RestoreOriginalValues();
                return;
            }

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
                if (lblChangedNoticeTitle != null) lblChangedNoticeTitle.Text = "ĐANG SỬA THÔNG TIN SAU KHI CHỐT CA:";
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

                decimal newPos3Open  = ParseMoney(txtPos3Opening?.Text ?? "0");
                decimal newPos3Close = ParseMoney(txtPos3Closing?.Text ?? "0");
                decimal newPos3Night = IsNightShift ? ParseMoney(txtPos3Night?.Text ?? "0") : 0m;

                decimal newBank1Open  = ParseMoney(txtBank1Opening?.Text ?? "0");
                decimal newBank1Close = ParseMoney(txtBank1Closing?.Text ?? "0");
                decimal newBank1Night = IsNightShift ? ParseMoney(txtBank1Night?.Text ?? "0") : 0m;

                decimal newBank2Open  = ParseMoney(txtBank2Opening?.Text ?? "0");
                decimal newBank2Close = ParseMoney(txtBank2Closing?.Text ?? "0");
                decimal newBank2Night = IsNightShift ? ParseMoney(txtBank2Night?.Text ?? "0") : 0m;

                decimal newBank3Open  = ParseMoney(txtBank3Opening?.Text ?? "0");
                decimal newBank3Close = ParseMoney(txtBank3Closing?.Text ?? "0");
                decimal newBank3Night = IsNightShift ? ParseMoney(txtBank3Night?.Text ?? "0") : 0m;

                // 2. Phát hiện chi tiết các trường bị thay đổi
                var changedList = new List<string>();
                if (newCashOpening != _originalCashOpening)
                    changedList.Add($"Tiền két đầu ({FormatMoney(_originalCashOpening)} -> {FormatMoney(newCashOpening)})");
                if (newCashClosing != _originalCashClosing)
                    changedList.Add($"Tiền két cuối ({FormatMoney(_originalCashClosing)} -> {FormatMoney(newCashClosing)})");

                if (_pos1Active)
                {
                    if (newPos1Open != _originalPos1Opening)
                        changedList.Add($"{_pos1Name} đầu ({FormatMoney(_originalPos1Opening)} -> {FormatMoney(newPos1Open)})");
                    if (newPos1Close != _originalPos1Closing)
                        changedList.Add($"{_pos1Name} cuối ({FormatMoney(_originalPos1Closing)} -> {FormatMoney(newPos1Close)})");
                    if (newPos1Night != _originalPos1Night)
                        changedList.Add($"{_pos1Name} đêm ({FormatMoney(_originalPos1Night)} -> {FormatMoney(newPos1Night)})");
                }

                if (_pos2Active)
                {
                    if (newPos2Open != _originalPos2Opening)
                        changedList.Add($"{_pos2Name} đầu ({FormatMoney(_originalPos2Opening)} -> {FormatMoney(newPos2Open)})");
                    if (newPos2Close != _originalPos2Closing)
                        changedList.Add($"{_pos2Name} cuối ({FormatMoney(_originalPos2Closing)} -> {FormatMoney(newPos2Close)})");
                    if (newPos2Night != _originalPos2Night)
                        changedList.Add($"{_pos2Name} đêm ({FormatMoney(_originalPos2Night)} -> {FormatMoney(newPos2Night)})");
                }

                if (_pos3Active)
                {
                    if (newPos3Open != _originalPos3Opening)
                        changedList.Add($"{_pos3Name} đầu ({FormatMoney(_originalPos3Opening)} -> {FormatMoney(newPos3Open)})");
                    if (newPos3Close != _originalPos3Closing)
                        changedList.Add($"{_pos3Name} cuối ({FormatMoney(_originalPos3Closing)} -> {FormatMoney(newPos3Close)})");
                    if (newPos3Night != _originalPos3Night)
                        changedList.Add($"{_pos3Name} đêm ({FormatMoney(_originalPos3Night)} -> {FormatMoney(newPos3Night)})");
                }

                if (_bank1Active)
                {
                    if (newBank1Open != _originalBank1Opening)
                        changedList.Add($"{_bank1Name} đầu ({FormatMoney(_originalBank1Opening)} -> {FormatMoney(newBank1Open)})");
                    if (newBank1Close != _originalBank1Closing)
                        changedList.Add($"{_bank1Name} cuối ({FormatMoney(_originalBank1Closing)} -> {FormatMoney(newBank1Close)})");
                    if (newBank1Night != _originalBank1Night)
                        changedList.Add($"{_bank1Name} đêm ({FormatMoney(_originalBank1Night)} -> {FormatMoney(newBank1Night)})");
                }

                if (_bank2Active)
                {
                    if (newBank2Open != _originalBank2Opening)
                        changedList.Add($"{_bank2Name} đầu ({FormatMoney(_originalBank2Opening)} -> {FormatMoney(newBank2Open)})");
                    if (newBank2Close != _originalBank2Closing)
                        changedList.Add($"{_bank2Name} cuối ({FormatMoney(_originalBank2Closing)} -> {FormatMoney(newBank2Close)})");
                    if (newBank2Night != _originalBank2Night)
                        changedList.Add($"{_bank2Name} đêm ({FormatMoney(_originalBank2Night)} -> {FormatMoney(newBank2Night)})");
                }

                if (_bank3Active)
                {
                    if (newBank3Open != _originalBank3Opening)
                        changedList.Add($"{_bank3Name} đầu ({FormatMoney(_originalBank3Opening)} -> {FormatMoney(newBank3Open)})");
                    if (newBank3Close != _originalBank3Closing)
                        changedList.Add($"{_bank3Name} cuối ({FormatMoney(_originalBank3Closing)} -> {FormatMoney(newBank3Close)})");
                    if (newBank3Night != _originalBank3Night)
                        changedList.Add($"{_bank3Name} đêm ({FormatMoney(_originalBank3Night)} -> {FormatMoney(newBank3Night)})");
                }

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
                    Pos3Opening = newPos3Open,
                    Pos3Closing = newPos3Close,
                    Pos3Night = newPos3Night,
                    Bank1Opening = newBank1Open,
                    Bank1Closing = newBank1Close,
                    Bank1Night = newBank1Night,
                    Bank2Opening = newBank2Open,
                    Bank2Closing = newBank2Close,
                    Bank2Night = newBank2Night,
                    Bank3Opening = newBank3Open,
                    Bank3Closing = newBank3Close,
                    Bank3Night = newBank3Night,
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
                        _rawShiftNote = (_rawShiftNote ?? "") + " | Cuối ca: " + changeSummary;
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
            decimal newPos3  = ParseMoney(txtPos3Opening?.Text ?? "0");
            decimal newBank1 = ParseMoney(txtBank1Opening?.Text ?? "0");
            decimal newBank2 = ParseMoney(txtBank2Opening?.Text ?? "0");
            decimal newBank3 = ParseMoney(txtBank3Opening?.Text ?? "0");

            // Phát hiện chính xác phần nào đã bị thay đổi
            var changedDetails = new List<string>();
            if (_pos1Active && newPos1 != _originalPos1Opening)
                changedDetails.Add($"{_pos1Name} ({FormatMoney(_originalPos1Opening)} đ -> {FormatMoney(newPos1)} đ)");
            if (_pos2Active && newPos2 != _originalPos2Opening)
                changedDetails.Add($"{_pos2Name} ({FormatMoney(_originalPos2Opening)} đ -> {FormatMoney(newPos2)} đ)");
            if (_pos3Active && newPos3 != _originalPos3Opening)
                changedDetails.Add($"{_pos3Name} ({FormatMoney(_originalPos3Opening)} đ -> {FormatMoney(newPos3)} đ)");
            if (_bank1Active && newBank1 != _originalBank1Opening)
                changedDetails.Add($"{_bank1Name} ({FormatMoney(_originalBank1Opening)} đ -> {FormatMoney(newBank1)} đ)");
            if (_bank2Active && newBank2 != _originalBank2Opening)
                changedDetails.Add($"{_bank2Name} ({FormatMoney(_originalBank2Opening)} đ -> {FormatMoney(newBank2)} đ)");
            if (_bank3Active && newBank3 != _originalBank3Opening)
                changedDetails.Add($"{_bank3Name} ({FormatMoney(_originalBank3Opening)} đ -> {FormatMoney(newBank3)} đ)");
            if (newCash != _originalCashOpening)
                changedDetails.Add($"Tiền mặt ({FormatMoney(_originalCashOpening)} đ -> {FormatMoney(newCash)} đ)");

            if (changedDetails.Count > 0)
            {
                _hiddenAuditChangeLog = $"Đầu ca: {string.Join(", ", changedDetails)}";
            }
            else
            {
                _hiddenAuditChangeLog = "";
            }

            string userNoteText = txtNote?.Text?.Trim() ?? "";
            string combinedOpeningNote = "";
            if (!string.IsNullOrEmpty(_hiddenAuditChangeLog) && !string.IsNullOrEmpty(userNoteText))
            {
                combinedOpeningNote = $"{_hiddenAuditChangeLog} | Ghi chú: {userNoteText}";
            }
            else if (!string.IsNullOrEmpty(_hiddenAuditChangeLog))
            {
                combinedOpeningNote = _hiddenAuditChangeLog;
            }
            else if (!string.IsNullOrEmpty(userNoteText))
            {
                combinedOpeningNote = $"Ghi chú: {userNoteText}";
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
                    Pos3Opening = newPos3,
                    Bank1Opening = newBank1,
                    Bank2Opening = newBank2,
                    Bank3Opening = newBank3,
                    Note = combinedOpeningNote
                };

                var apiRes = await ApiService.Client.PostAsJsonAsync("api/Shift/confirm-change", changeReq);
                if (apiRes.IsSuccessStatusCode)
                {
                    _hasChangedInitialData = true;
                    _rawShiftNote = changeReq.Note;
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
                                    $"• Tiền mặt đầu ca: {txtCashOpening?.Text ?? "0"} đ\n";
                if (_pos1Active) confirmMsg += $"• {_pos1Name} đầu ca: {txtPos1Opening?.Text ?? "0"} đ\n";
                if (_pos2Active) confirmMsg += $"• {_pos2Name} đầu ca: {txtPos2Opening?.Text ?? "0"} đ\n";
                if (_pos3Active) confirmMsg += $"• {_pos3Name} đầu ca: {txtPos3Opening?.Text ?? "0"} đ\n";
                if (_bank1Active) confirmMsg += $"• {_bank1Name} đầu ca: {txtBank1Opening?.Text ?? "0"} đ\n";
                if (_bank2Active) confirmMsg += $"• {_bank2Name} đầu ca: {txtBank2Opening?.Text ?? "0"} đ\n";
                if (_bank3Active) confirmMsg += $"• {_bank3Name} đầu ca: {txtBank3Opening?.Text ?? "0"} đ\n";
                confirmMsg += "\nBạn đã kiểm đếm và xác nhận khớp số liệu bàn giao từ ca trước chứ?\n" +
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
                decimal pos1Opening = _pos1Active ? ParseMoney(txtPos1Opening?.Text ?? "0") : 0;
                decimal pos1Closing = _pos1Active ? ParseMoney(txtPos1Closing?.Text ?? "0") : 0;

                decimal pos2Opening = _pos2Active ? ParseMoney(txtPos2Opening?.Text ?? "0") : 0;
                decimal pos2Closing = _pos2Active ? ParseMoney(txtPos2Closing?.Text ?? "0") : 0;

                decimal pos3Opening = _pos3Active ? ParseMoney(txtPos3Opening?.Text ?? "0") : 0;
                decimal pos3Closing = _pos3Active ? ParseMoney(txtPos3Closing?.Text ?? "0") : 0;

                decimal bank1Opening = _bank1Active ? ParseMoney(txtBank1Opening?.Text ?? "0") : 0;
                decimal bank1Closing = _bank1Active ? ParseMoney(txtBank1Closing?.Text ?? "0") : 0;

                decimal bank2Opening = _bank2Active ? ParseMoney(txtBank2Opening?.Text ?? "0") : 0;
                decimal bank2Closing = _bank2Active ? ParseMoney(txtBank2Closing?.Text ?? "0") : 0;

                decimal bank3Opening = _bank3Active ? ParseMoney(txtBank3Opening?.Text ?? "0") : 0;
                decimal bank3Closing = _bank3Active ? ParseMoney(txtBank3Closing?.Text ?? "0") : 0;

                var validationErrors = new List<string>();

                if (_pos1Active && pos1Closing < pos1Opening)
                {
                    validationErrors.Add($"• {_pos1Name} (App bán hàng): Cuối ca ({FormatMoney(pos1Closing)} đ) < Đầu ca ({FormatMoney(pos1Opening)} đ)");
                }

                if (_pos2Active && pos2Closing < pos2Opening)
                {
                    validationErrors.Add($"• {_pos2Name} (App bán hàng): Cuối ca ({FormatMoney(pos2Closing)} đ) < Đầu ca ({FormatMoney(pos2Opening)} đ)");
                }

                if (_pos3Active && pos3Closing < pos3Opening)
                {
                    validationErrors.Add($"• {_pos3Name} (App bán hàng): Cuối ca ({FormatMoney(pos3Closing)} đ) < Đầu ca ({FormatMoney(pos3Opening)} đ)");
                }

                if (_bank1Active && bank1Closing < bank1Opening)
                {
                    validationErrors.Add($"• {_bank1Name} (Chuyển khoản): Cuối ca ({FormatMoney(bank1Closing)} đ) < Đầu ca ({FormatMoney(bank1Opening)} đ)");
                }

                if (_bank2Active && bank2Closing < bank2Opening)
                {
                    validationErrors.Add($"• {_bank2Name} (Chuyển khoản): Cuối ca ({FormatMoney(bank2Closing)} đ) < Đầu ca ({FormatMoney(bank2Opening)} đ)");
                }

                if (_bank3Active && bank3Closing < bank3Opening)
                {
                    validationErrors.Add($"• {_bank3Name} (Chuyển khoản): Cuối ca ({FormatMoney(bank3Closing)} đ) < Đầu ca ({FormatMoney(bank3Opening)} đ)");
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
                    if (_pos1Active && pos1Closing < pos1Opening) txtPos1Closing?.Focus();
                    else if (_pos2Active && pos2Closing < pos2Opening) txtPos2Closing?.Focus();
                    else if (_pos3Active && pos3Closing < pos3Opening) txtPos3Closing?.Focus();
                    else if (_bank1Active && bank1Closing < bank1Opening) txtBank1Closing?.Focus();
                    else if (_bank2Active && bank2Closing < bank2Opening) txtBank2Closing?.Focus();
                    else if (_bank3Active && bank3Closing < bank3Opening) txtBank3Closing?.Focus();

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
                            finalNote = $"{_hiddenAuditChangeLog} | Ghi chú: {userNote}";
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
                        Pos1Closing = _pos1Active ? ParseMoney(txtPos1Closing?.Text ?? "0") : 0,
                        Pos1Night = (_pos1Active && IsNightShift) ? ParseMoney(txtPos1Night?.Text ?? "0") : 0,
                        Pos2Closing = _pos2Active ? ParseMoney(txtPos2Closing?.Text ?? "0") : 0,
                        Pos2Night = (_pos2Active && IsNightShift) ? ParseMoney(txtPos2Night?.Text ?? "0") : 0,
                        Pos3Closing = _pos3Active ? ParseMoney(txtPos3Closing?.Text ?? "0") : 0,
                        Pos3Night = (_pos3Active && IsNightShift) ? ParseMoney(txtPos3Night?.Text ?? "0") : 0,
                        Bank1Closing = _bank1Active ? ParseMoney(txtBank1Closing?.Text ?? "0") : 0,
                        Bank1Night = (_bank1Active && IsNightShift) ? ParseMoney(txtBank1Night?.Text ?? "0") : 0,
                        Bank2Closing = _bank2Active ? ParseMoney(txtBank2Closing?.Text ?? "0") : 0,
                        Bank2Night = (_bank2Active && IsNightShift) ? ParseMoney(txtBank2Night?.Text ?? "0") : 0,
                        Bank3Closing = _bank3Active ? ParseMoney(txtBank3Closing?.Text ?? "0") : 0,
                        Bank3Night = (_bank3Active && IsNightShift) ? ParseMoney(txtBank3Night?.Text ?? "0") : 0,
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
                if (bdrChangedNotice != null)
                {
                    bdrChangedNotice.Visibility = Visibility.Visible;
                    if (lblChangedNoticeTitle != null) lblChangedNoticeTitle.Text = "ĐANG SỬA THÔNG TIN ĐẦU CA:";
                }
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

            TextBox?[] openingBoxes = { txtPos1Opening, txtPos2Opening, txtPos3Opening, txtBank1Opening, txtBank2Opening, txtBank3Opening, txtCashOpening };
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
            if (txtPos3Closing != null) txtPos3Closing.IsReadOnly = !isEditable;
            if (txtPos3Night != null)   txtPos3Night.IsReadOnly   = !isEditable;

            // 2. Ô cuối ca & đêm của CHUYỂN KHOẢN
            if (txtBank1Closing != null) txtBank1Closing.IsReadOnly = !isEditable;
            if (txtBank1Night != null)   txtBank1Night.IsReadOnly   = !isEditable;
            if (txtBank2Closing != null) txtBank2Closing.IsReadOnly = !isEditable;
            if (txtBank2Night != null)   txtBank2Night.IsReadOnly   = !isEditable;
            if (txtBank3Closing != null) txtBank3Closing.IsReadOnly = !isEditable;
            if (txtBank3Night != null)   txtBank3Night.IsReadOnly   = !isEditable;

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

            // Kiểm soát nút "Thay đổi thông tin" (chỉ vô hiệu hóa nếu ca này ĐÃ TỪNG SỬA SAU KHI CHỐT CA)
            bool hasEditedClosing = !string.IsNullOrEmpty(_rawShiftNote) &&
                (_rawShiftNote.IndexOf("Cuối ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 _rawShiftNote.IndexOf("đã chỉnh sửa", StringComparison.OrdinalIgnoreCase) >= 0);

            if (btnChangeInitialData != null)
            {
                if (hasEditedClosing)
                {
                    btnChangeInitialData.Visibility = Visibility.Visible;
                    btnChangeInitialData.IsEnabled = false;
                    btnChangeInitialData.Content = "Đã sửa (Chỉ đổi 1 lần)";
                    btnChangeInitialData.Background = Brushes.White;
                    btnChangeInitialData.Foreground = Brushes.Black;
                    btnChangeInitialData.BorderBrush = Brushes.Black;
                    btnChangeInitialData.ToolTip = "Ca làm việc này đã được điều chỉnh sau khi chốt ca 1 lần, không thể thay đổi thêm nữa.";
                }
                else
                {
                    btnChangeInitialData.Visibility = Visibility.Visible;
                    btnChangeInitialData.IsEnabled = true;
                    btnChangeInitialData.Content = "Thay đổi thông tin";
                    btnChangeInitialData.Background = Brushes.White;
                    btnChangeInitialData.Foreground = Brushes.Black;
                    btnChangeInitialData.BorderBrush = Brushes.Black;
                    btnChangeInitialData.ToolTip = "Chỉ người phụ trách ca mới được điều chỉnh thông tin (duy nhất 1 lần sau chốt ca).";
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
