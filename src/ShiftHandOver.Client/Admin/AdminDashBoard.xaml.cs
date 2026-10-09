using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ShiftHandOver.Client.Services;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Admin
{
    /// <summary>
    /// Interaction logic for AdminDashBoard.xaml
    /// </summary>
    public partial class AdminDashBoard : Window
    {
        private List<BranchDTO> _branches = new();
        private List<ShiftTypeDTO> _shiftTypes = new();
        private List<AdminShiftSummaryDTO> _allShifts = new();
        private List<AdminExpenseDTO> _allExpenses = new();
        private List<EmployeeAuditItem> _allEmployees = new();
        private List<PosConfigSettingDTO> _currentPosConfigs = new();
        private List<BranchBankSettingDTO> _currentBranchBanks = new();
        private List<BranchHandoverListItem> _allBranchHandoverList = new();
        private BranchHandoverConfigDTO _editingBranchConfig = new();
        private ShiftHandoverDetailDTO? _currentDetail;
        private string? _selectedNewBankLogoUrl;
        private string? _selectedNewPosLogoUrl;
        private UserDTO? _currentAdminUser;

        public AdminDashBoard(UserDTO? currentAdmin = null)
        {
            _currentAdminUser = currentAdmin;
            InitializeComponent();
            InitResetTab();
            InitAdminAccountTab();
            _ = LoadAllDashboardDataAsync();
        }

        private void InitAdminAccountTab()
        {
            if (_currentAdminUser != null)
            {
                if (TxtCurrentAdminHeader != null)
                {
                    string displayName = !string.IsNullOrWhiteSpace(_currentAdminUser.FullName) ? _currentAdminUser.FullName : _currentAdminUser.Username;
                    TxtCurrentAdminHeader.Text = $"Quản trị viên: {displayName}";
                }
                if (TxtAdminCurUsername != null)
                {
                    TxtAdminCurUsername.Text = _currentAdminUser.Username;
                }
                if (TxtAdminNewUsername != null)
                {
                    TxtAdminNewUsername.Text = _currentAdminUser.Username;
                }
                if (TxtAdminNewFullName != null)
                {
                    TxtAdminNewFullName.Text = _currentAdminUser.FullName;
                }
            }
        }

        private void InitResetTab()
        {
            if (DpResetFromDate != null)
            {
                DpResetFromDate.SelectedDate = DateTime.Today.AddDays(-30);
            }
            if (DpResetToDate != null)
            {
                DpResetToDate.SelectedDate = DateTime.Today;
            }
        }

        private async Task LoadAllDashboardDataAsync()
        {
            await Task.WhenAll(
                LoadBranchesAsync(),
                LoadBranchHandoverListAsync(),
                LoadShiftTypesAsync(),
                LoadShiftListAsync(),
                LoadRecentDifferencesAsync(),
                LoadExpensesAsync()
            );
            await LoadEmployeeDataAsync();
            UpdateEmployeeFilterCombobox();
            await LoadResetShiftsAsync();
        }

        #region TAB 1 & COMMON: NẠP DỮ LIỆU CƠ BẢN VÀ TÍNH TOÁN KPI

        private async Task LoadShiftTypesAsync()
        {
            try
            {
                var types = await ApiService.Client.GetFromJsonAsync<List<ShiftTypeDTO>>("api/Shift/types");
                if (types != null && types.Any())
                {
                    _shiftTypes = types;


                    // Cập nhật bộ lọc ca ở Tab 2
                    if (CbFilterShiftType != null)
                    {
                        var shiftFilterItems = new List<dynamic>
                        {
                            new { Code = "", Display = "Tất cả ca" }
                        };
                        foreach (var st in _shiftTypes)
                        {
                            shiftFilterItems.Add(new { Code = st.Code, Display = st.Name });
                        }
                        CbFilterShiftType.ItemsSource = shiftFilterItems;
                        CbFilterShiftType.DisplayMemberPath = "Display";
                        CbFilterShiftType.SelectedValuePath = "Code";
                        CbFilterShiftType.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load shift types: " + ex.Message);
            }
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                var branches = await ApiService.Client.GetFromJsonAsync<List<BranchDTO>>("api/Branch");
                if (branches != null && branches.Any())
                {
                    _branches = branches;

                    var branchItems = _branches.Select(b => new { b.Id, Display = $"{b.Name} (ID: {b.Id})" }).ToList();



                    // Tab 1 Branch Filter
                    if (CbBranchFilterTab1 != null)
                    {
                        var t1Branches = new List<dynamic>
                        {
                            new { Id = 0, Display = "Tất cả cơ sở" }
                        };
                        foreach (var b in _branches)
                        {
                            t1Branches.Add(new { Id = b.Id, Display = b.Name });
                        }
                        CbBranchFilterTab1.ItemsSource = t1Branches;
                        CbBranchFilterTab1.DisplayMemberPath = "Display";
                        CbBranchFilterTab1.SelectedValuePath = "Id";
                        CbBranchFilterTab1.SelectedIndex = 0;
                    }

                    // Tab 2 Branch Filter
                    if (CbFilterBranch != null)
                    {
                        var t2Branches = new List<dynamic>
                        {
                            new { Id = 0, Display = "Tất cả cơ sở" }
                        };
                        foreach (var b in _branches)
                        {
                            t2Branches.Add(new { Id = b.Id, Display = b.Name });
                        }
                        CbFilterBranch.ItemsSource = t2Branches;
                        CbFilterBranch.DisplayMemberPath = "Display";
                        CbFilterBranch.SelectedValuePath = "Id";
                        CbFilterBranch.SelectedIndex = 0;
                    }

                    // Tiền chi trong ca Branch Filter
                    if (CbExpenseFilterBranch != null)
                    {
                        var expBranches = new List<dynamic>
                        {
                            new { Id = 0, Display = "Tất cả cơ sở" }
                        };
                        foreach (var b in _branches)
                        {
                            expBranches.Add(new { Id = b.Id, Display = b.Name });
                        }
                        CbExpenseFilterBranch.ItemsSource = expBranches;
                        CbExpenseFilterBranch.DisplayMemberPath = "Display";
                        CbExpenseFilterBranch.SelectedValuePath = "Id";
                        CbExpenseFilterBranch.SelectedIndex = 0;
                    }

                    // Reset Filter Branch
                    if (CbResetBranch != null)
                    {
                        var resetBranches = new List<dynamic>
                        {
                            new { Id = 0, Display = "Tất cả cơ sở" }
                        };
                        foreach (var b in _branches)
                        {
                            resetBranches.Add(new { Id = b.Id, Display = b.Name });
                        }
                        CbResetBranch.ItemsSource = resetBranches;
                        CbResetBranch.DisplayMemberPath = "Display";
                        CbResetBranch.SelectedValuePath = "Id";
                        CbResetBranch.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load branches: " + ex.Message);
            }
        }

        private async Task LoadShiftListAsync()
        {
            try
            {
                var shifts = await ApiService.Client.GetFromJsonAsync<List<AdminShiftSummaryDTO>>("api/Shift/all-shifts");
                _allShifts = shifts ?? new List<AdminShiftSummaryDTO>();

                // Nạp danh sách tháng vào CbMonthFilterTab1
                if (CbMonthFilterTab1 != null)
                {
                    var months = _allShifts
                        .Where(s => !string.IsNullOrEmpty(s.ShiftDate))
                        .Select(s =>
                        {
                            if (DateTime.TryParseExact(s.ShiftDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d))
                            {
                                return d.ToString("MM/yyyy");
                            }
                            return "";
                        })
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct()
                        .OrderByDescending(m => m)
                        .ToList();

                    var monthItems = new List<dynamic>
                    {
                        new { Value = "", Display = "Tất cả các tháng" }
                    };
                    foreach (var m in months)
                    {
                        monthItems.Add(new { Value = m, Display = $"Tháng {m}" });
                    }
                    CbMonthFilterTab1.ItemsSource = monthItems;
                    CbMonthFilterTab1.DisplayMemberPath = "Display";
                    CbMonthFilterTab1.SelectedValuePath = "Value";
                    CbMonthFilterTab1.SelectedIndex = 0;
                }

                CalculateTab1KPIs();

                // Hiển thị ở Tab 2
                if (DgShiftList != null)
                {
                    var summaryList = MapToShiftSummaryItems(_allShifts);
                    DgShiftList.ItemsSource = summaryList;
                    if (TxtFilterCount != null)
                    {
                        TxtFilterCount.Text = $"Hiển thị: {summaryList.Count} Biên bản tìm thấy";
                    }

                    if (summaryList.Count > 0)
                    {
                        DgShiftList.SelectedIndex = 0;
                    }
                }

                if (_allEmployees != null && _allEmployees.Any())
                {
                    FilterLookupEmployees();
                }

                UpdateEmployeeFilterCombobox();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load shift list: " + ex.Message);
            }
        }

        private void CalculateTab1KPIs()
        {
            string selectedMonth = (CbMonthFilterTab1?.SelectedValue as string) ?? "";
            int selectedBranchId = (CbBranchFilterTab1?.SelectedValue as int?) ?? 0;

            var filtered = _allShifts.AsEnumerable();

            if (!string.IsNullOrEmpty(selectedMonth))
            {
                filtered = filtered.Where(s =>
                {
                    if (DateTime.TryParseExact(s.ShiftDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d))
                    {
                        return d.ToString("MM/yyyy") == selectedMonth;
                    }
                    return false;
                });
            }

            if (selectedBranchId > 0)
            {
                var branchName = _branches.FirstOrDefault(b => b.Id == selectedBranchId)?.Name ?? "";
                if (!string.IsNullOrEmpty(branchName))
                {
                    filtered = filtered.Where(s => s.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase));
                }
            }

            var list = filtered.ToList();

            // Loại bỏ các ca tự động chốt (không có nhân viên trực ca) khỏi tổng số ca làm trong khoảng thời gian theo yêu cầu
            var validWorkedList = list.Where(s => !(s.ClosedByUserId == null && (s.Note?.Contains("tự động chốt", StringComparison.OrdinalIgnoreCase) ?? false))).ToList();
            int total = validWorkedList.Count;

            // 1. Thẻ tổng ca
            if (TxtKpiTotalShifts != null)
            {
                TxtKpiTotalShifts.Text = $"{total} Ca";
            }
            if (TxtKpiTotalShiftsSub != null)
            {
                int closedCount = validWorkedList.Count(s => s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));
                double closedPct = total > 0 ? (closedCount * 100.0 / total) : 0;
                TxtKpiTotalShiftsSub.Text = $"Đã chốt {closedCount}/{total} ca ({closedPct:0.#}%)";
            }

            // Lọc danh sách các ca đã thực sự chốt ca (để không tính ca đang mở chưa có tiền cuối ca)
            var closedList = list.Where(s => s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase)).ToList();

            // 2. Thẻ tiền mặt thực thu một tháng (Tổng tất cả số tiền mặt chênh lệch kiếm được)
            decimal totalCashEarned = closedList.Sum(s => s.CashDiffClosing);
            if (TxtKpiCashActualEarned != null)
            {
                TxtKpiCashActualEarned.Text = totalCashEarned >= 0 ? $"+{totalCashEarned:N0} đ" : $"{totalCashEarned:N0} đ";
            }

            // 4. Thẻ ca âm
            var negatives = list.Where(s => (s.CashDifference ?? 0m) < 0m).ToList();
            decimal totalNeg = negatives.Sum(s => s.CashDifference ?? 0m);
            double negPct = total > 0 ? (negatives.Count * 100.0 / total) : 0;

            if (TxtKpiNegativeShifts != null) TxtKpiNegativeShifts.Text = $"{negatives.Count} Ca";
            if (TxtKpiNegativePercent != null) TxtKpiNegativePercent.Text = $" ({negPct:0.#}%)";
            if (TxtKpiNegativeTotal != null) TxtKpiNegativeTotal.Text = $"Tổng hụt: {totalNeg:N0} đ";

            // 5. Thẻ ca dương
            var positives = list.Where(s => (s.CashDifference ?? 0m) > 0m).ToList();
            decimal totalPos = positives.Sum(s => s.CashDifference ?? 0m);
            double posPct = total > 0 ? (positives.Count * 100.0 / total) : 0;

            if (TxtKpiPositiveShifts != null) TxtKpiPositiveShifts.Text = $"{positives.Count} Ca";
            if (TxtKpiPositivePercent != null) TxtKpiPositivePercent.Text = $" ({posPct:0.#}%)";
            if (TxtKpiPositiveTotal != null) TxtKpiPositiveTotal.Text = $"Tổng thừa: +{totalPos:N0} đ";
        }

        private void CbMonthFilterTab1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateTab1KPIs();
        }

        private void CbBranchFilterTab1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateTab1KPIs();
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_allShifts == null || !_allShifts.Any())
            {
                MessageBox.Show("Hiện không có dữ liệu ca làm việc nào để xuất báo cáo!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string monthDisplay = (CbMonthFilterTab1?.SelectedItem as string) ?? DateTime.Today.ToString("MM/yyyy");
                string safeMonth = monthDisplay.Replace("/", "-");
                string defaultFileName = $"BaoCao_TongHop_ChotCa_Thang_{safeMonth}.csv";

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "File Excel / CSV (*.csv)|*.csv|Tất cả tệp (*.*)|*.*",
                    FileName = defaultFileName,
                    Title = "Xuất báo cáo tổng hợp ca làm việc ra file CSV / Excel"
                };

                if (sfd.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("STT,Mã Ca,Ngày Làm,Ca Làm,Cơ Sở,Người Phụ Trách,Chênh Lệch Tiền Mặt,Tiền Âm/Dương,Doanh Thu Ngân Hàng,Trạng Thái,Ghi Chú");

                    int idx = 1;
                    foreach (var s in _allShifts)
                    {
                        string pic = !string.IsNullOrEmpty(s.EmployeeNames) ? s.EmployeeNames : s.ClosedByUser;
                        string diffStr = (s.CashDifference ?? 0m).ToString("0");
                        sb.AppendLine($"{idx},{EscapeCsv(s.ShiftCode)},{EscapeCsv(s.ShiftDate)},{EscapeCsv(s.ShiftType)},{EscapeCsv(s.BranchName)},{EscapeCsv(pic)},{s.CashDiffClosing:0},{diffStr},{s.BankRevenue:0},{EscapeCsv(s.StatusDisplay)},{EscapeCsv(s.Note)}");
                        idx++;
                    }

                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất thành công {_allShifts.Count} ca làm việc ra file:\n\n{sfd.FileName}", "Xuất Báo Cáo Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất file báo cáo: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRecentDifferencesAsync()
        {
            try
            {
                var diffs = await ApiService.Client.GetFromJsonAsync<List<AdminShiftSummaryDTO>>("api/Shift/recent-differences");
                if (diffs != null && DgRecentShiftDiff != null)
                {
                    var items = MapToShiftSummaryItems(diffs);
                    DgRecentShiftDiff.ItemsSource = items;
                    if (items.Count > 0)
                    {
                        DgRecentShiftDiff.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load recent differences: " + ex.Message);
            }
        }

        #endregion

        #region TAB 2: TRA CỨU & MASTER-DETAIL BIÊN BẢN CHỐT CA

        private List<ShiftSummaryItem> MapToShiftSummaryItems(List<AdminShiftSummaryDTO> list)
        {
            return list.Select(s =>
            {
                bool isMod = IsShiftModified(s);
                return new ShiftSummaryItem
                {
                    RawId = s.Id,
                    ShiftId = s.ShiftCode,
                    ShiftDate = s.ShiftDate,
                    ShiftType = s.ShiftType,
                    BranchName = s.BranchName,
                    ClosedByUser = s.ClosedByUser,
                    OpenedByUser = s.OpenedByUser,
                    EmployeeNames = s.EmployeeNames,
                    CashDifference = s.CashDifferenceDisplay,
                    Note = s.Note,
                    Status = s.StatusDisplay,
                    CashDiffDisplay = s.CashDiffClosing > 0 ? $"+{s.CashDiffClosing:N0} đ" : (s.CashDiffClosing < 0 ? $"{s.CashDiffClosing:N0} đ" : "0 đ"),
                    BankDiffDisplay = $"{s.BankRevenue:N0} đ"
                };
            }).ToList();
        }

        private static bool IsShiftModified(AdminShiftSummaryDTO s)
        {
            if (s == null) return false;

            // 1. Kiểm tra Status: Có hậu tố NC hoặc chứa NC (ConfirmStartNC, ClosedNC, CloseNC, NConfirmNC, ConfirmNC, v.v.)
            if (!string.IsNullOrEmpty(s.Status) &&
                (s.Status.EndsWith("NC", StringComparison.OrdinalIgnoreCase) ||
                 s.Status.IndexOf("NC", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            // 2. Kiểm tra StatusDisplay
            if (!string.IsNullOrEmpty(s.StatusDisplay) &&
                (s.StatusDisplay.IndexOf("Có sửa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 s.StatusDisplay.IndexOf("NC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 s.StatusDisplay.IndexOf("Đang sửa", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            // 3. Kiểm tra Note: Chứa log thay đổi tiền đầu ca ("đã thay đổi") hoặc sau chốt ca ("đã chỉnh sửa")
            if (!string.IsNullOrEmpty(s.Note))
            {
                if (s.Note.IndexOf("đã thay đổi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Note.IndexOf("đã chỉnh sửa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Note.IndexOf("Đầu ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Note.IndexOf("Cuối ca:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Note.IndexOf("NC", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateEmployeeFilterCombobox()
        {
            if (CbFilterEmployee == null) return;

            string currentSelected = (CbFilterEmployee.SelectedValue as string) ?? "";

            var employeeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_allEmployees != null)
            {
                foreach (var emp in _allEmployees)
                {
                    if (!string.IsNullOrWhiteSpace(emp.FullName))
                        employeeNames.Add(emp.FullName.Trim());
                }
            }

            if (_allShifts != null)
            {
                foreach (var s in _allShifts)
                {
                    if (!string.IsNullOrWhiteSpace(s.ClosedByUser) && s.ClosedByUser != "—")
                        employeeNames.Add(s.ClosedByUser.Trim());
                    if (!string.IsNullOrWhiteSpace(s.OpenedByUser) && s.OpenedByUser != "—")
                        employeeNames.Add(s.OpenedByUser.Trim());
                    if (!string.IsNullOrWhiteSpace(s.EmployeeNames))
                    {
                        foreach (var name in s.EmployeeNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string trimmed = name.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && trimmed != "—")
                                employeeNames.Add(trimmed);
                        }
                    }
                }
            }

            if (_allExpenses != null)
            {
                foreach (var exp in _allExpenses)
                {
                    if (!string.IsNullOrWhiteSpace(exp.CreatedByUser) && exp.CreatedByUser != "—")
                        employeeNames.Add(exp.CreatedByUser.Trim());
                    if (!string.IsNullOrWhiteSpace(exp.EmployeeNames))
                    {
                        foreach (var name in exp.EmployeeNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            string trimmed = name.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && trimmed != "—")
                                employeeNames.Add(trimmed);
                        }
                    }
                }
            }

            var list = new List<dynamic>
            {
                new { Name = "", Display = "Tất cả nhân viên" }
            };

            foreach (var name in employeeNames.OrderBy(n => n))
            {
                list.Add(new { Name = name, Display = name });
            }

            if (CbFilterEmployee != null)
            {
                CbFilterEmployee.ItemsSource = list;
                CbFilterEmployee.DisplayMemberPath = "Display";
                CbFilterEmployee.SelectedValuePath = "Name";

                int foundIndex = list.FindIndex(x => (string)x.Name == currentSelected);
                CbFilterEmployee.SelectedIndex = foundIndex >= 0 ? foundIndex : 0;
            }

            if (CbExpenseFilterEmployee != null)
            {
                string currentExpSelected = (CbExpenseFilterEmployee.SelectedValue as string) ?? "";
                CbExpenseFilterEmployee.ItemsSource = list;
                CbExpenseFilterEmployee.DisplayMemberPath = "Display";
                CbExpenseFilterEmployee.SelectedValuePath = "Name";

                int foundExpIndex = list.FindIndex(x => (string)x.Name == currentExpSelected);
                CbExpenseFilterEmployee.SelectedIndex = foundExpIndex >= 0 ? foundExpIndex : 0;
            }
        }

        private void BtnFilterShifts_Click(object sender, RoutedEventArgs e)
        {
            DateTime? filterDate = DpFilterDate?.SelectedDate;
            int branchId = (CbFilterBranch?.SelectedValue as int?) ?? 0;
            string shiftCode = (CbFilterShiftType?.SelectedValue as string) ?? "";
            string employee = (CbFilterEmployee?.SelectedValue as string) ?? "";
            int modifiedFilterIndex = CbFilterModified?.SelectedIndex ?? 0;

            var filtered = _allShifts.AsEnumerable();

            if (filterDate.HasValue)
            {
                string dateStr = filterDate.Value.ToString("dd/MM/yyyy");
                filtered = filtered.Where(s => s.ShiftDate == dateStr);
            }

            if (branchId > 0)
            {
                var branchName = _branches.FirstOrDefault(b => b.Id == branchId)?.Name ?? "";
                if (!string.IsNullOrEmpty(branchName))
                {
                    filtered = filtered.Where(s => s.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!string.IsNullOrEmpty(shiftCode))
            {
                var targetType = _shiftTypes.FirstOrDefault(st => st.Code == shiftCode)?.Name ?? shiftCode;
                filtered = filtered.Where(s => s.ShiftType.Equals(targetType, StringComparison.OrdinalIgnoreCase) || s.ShiftType.Equals(shiftCode, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(employee))
            {
                filtered = filtered.Where(s =>
                    (!string.IsNullOrEmpty(s.ClosedByUser) && s.ClosedByUser.IndexOf(employee, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.OpenedByUser) && s.OpenedByUser.IndexOf(employee, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.EmployeeNames) && s.EmployeeNames.IndexOf(employee, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            // Lọc theo ca có chỉnh sửa (kể cả xác thực tiền đầu ca & sau chốt ca)
            if (modifiedFilterIndex == 1) // Có chỉnh sửa
            {
                filtered = filtered.Where(s => IsShiftModified(s));
            }
            else if (modifiedFilterIndex == 2) // Không chỉnh sửa
            {
                filtered = filtered.Where(s => !IsShiftModified(s));
            }

            var result = MapToShiftSummaryItems(filtered.ToList());
            if (DgShiftList != null)
            {
                DgShiftList.ItemsSource = result;
                if (TxtFilterCount != null)
                {
                    TxtFilterCount.Text = $"Hiển thị: {result.Count} Biên bản tìm thấy";
                }

                if (result.Count > 0)
                {
                    DgShiftList.SelectedIndex = 0;
                }
            }
        }

        private void BtnResetShiftFilter_Click(object sender, RoutedEventArgs e)
        {
            if (DpFilterDate != null) DpFilterDate.SelectedDate = null;
            if (CbFilterBranch != null) CbFilterBranch.SelectedIndex = 0;
            if (CbFilterShiftType != null) CbFilterShiftType.SelectedIndex = 0;
            if (CbFilterEmployee != null) CbFilterEmployee.SelectedIndex = 0;
            if (CbFilterModified != null) CbFilterModified.SelectedIndex = 0;

            var result = MapToShiftSummaryItems(_allShifts);
            if (DgShiftList != null)
            {
                DgShiftList.ItemsSource = result;
                if (TxtFilterCount != null)
                {
                    TxtFilterCount.Text = $"Hiển thị: {result.Count} Biên bản tìm thấy";
                }
                if (result.Count > 0)
                {
                    DgShiftList.SelectedIndex = 0;
                }
            }
        }

        private async void DgShiftList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgShiftList?.SelectedItem is ShiftSummaryItem item && item.RawId > 0)
            {
                try
                {
                    var detail = await ApiService.Client.GetFromJsonAsync<ShiftHandoverDetailDTO>($"api/Shift/{item.RawId}");
                    if (detail != null)
                    {
                        _currentDetail = detail;
                        DisplayShiftDetail(detail);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi load chi tiết ca: " + ex.Message);
                }
            }
        }

        private void DisplayShiftDetail(ShiftHandoverDetailDTO detail)
        {
            // Header
            if (TxtDetailShiftCode != null)
            {
                bool isMod = (!string.IsNullOrEmpty(detail.Status) && detail.Status.Contains("NC", StringComparison.OrdinalIgnoreCase)) ||
                             (!string.IsNullOrEmpty(detail.Note) && (detail.Note.Contains("đã thay đổi", StringComparison.OrdinalIgnoreCase) || 
                                                                     detail.Note.Contains("đã chỉnh sửa", StringComparison.OrdinalIgnoreCase) ||
                                                                     detail.Note.Contains("Đầu ca:", StringComparison.OrdinalIgnoreCase) ||
                                                                     detail.Note.Contains("Cuối ca:", StringComparison.OrdinalIgnoreCase)));
                string modTag = isMod ? " [CÓ CHỈNH SỬA]" : "";
                TxtDetailShiftCode.Text = $"BIÊN BẢN CHỐT CA: {detail.BranchName} — {detail.ShiftTypeName} ({detail.ShiftDate:dd/MM/yyyy}){modTag}";
            }

            decimal diff = detail.CashDifference ?? 0m;
            if (TxtDetailDiffBadge != null)
            {
                if (diff < 0)
                {
                    TxtDetailDiffBadge.Text = $"ÂM TIỀN {diff:N0} đ";
                }
                else if (diff > 0)
                {
                    TxtDetailDiffBadge.Text = $"THỪA TIỀN +{diff:N0} đ";
                }
                else
                {
                    TxtDetailDiffBadge.Text = "CÂN TIỀN (0 đ)";
                }
            }

            // Thông tin chung
            if (TxtDetailBranch != null) TxtDetailBranch.Text = $"Cơ sở: {detail.BranchName}";
            if (TxtDetailShiftType != null) TxtDetailShiftType.Text = $"Ca làm: {detail.ShiftTypeName}";
            if (TxtDetailDate != null) TxtDetailDate.Text = $"Ngày làm việc: {detail.ShiftDate:dd/MM/yyyy}";
            if (TxtDetailOpenedBy != null) TxtDetailOpenedBy.Text = $"Người mở ca: {detail.OpenedByUser}";
            if (TxtDetailClosedBy != null) TxtDetailClosedBy.Text = $"Người đóng ca: {detail.ClosedByUser}";

            string empNames = (detail.EmployeeNames != null && detail.EmployeeNames.Any())
                ? string.Join(", ", detail.EmployeeNames)
                : detail.ClosedByUser;
            if (TxtDetailEmployees != null) TxtDetailEmployees.Text = $"Nhân viên cùng trực: {empNames}";

            bool isNight = detail.ShiftType != null && (detail.ShiftType.Equals("NIGHT", StringComparison.OrdinalIgnoreCase) || detail.ShiftTypeName.Contains("Đêm", StringComparison.OrdinalIgnoreCase) || detail.ShiftTypeName.Contains("Dem", StringComparison.OrdinalIgnoreCase));

            // 1. VÙNG: TIỀN MẶT TẠI KÉT
            decimal cashOpen = detail.CashOpening;
            bool hasClosed = detail.CashClosing.HasValue;
            decimal cashClose = detail.CashClosing ?? 0m;
            decimal cashDiff = hasClosed ? (cashClose - cashOpen) : 0m;

            if (TxtDetailCashOpening != null) TxtDetailCashOpening.Text = $"{cashOpen:N0} đ";
            if (TxtDetailCashClosing != null) TxtDetailCashClosing.Text = hasClosed ? $"{cashClose:N0} đ" : "Chưa chốt ca";
            if (TxtDetailCashDiff != null)
            {
                if (!hasClosed)
                {
                    TxtDetailCashDiff.Text = "—";
                }
                else if (cashDiff > 0)
                {
                    TxtDetailCashDiff.Text = $"+{cashDiff:N0} đ";
                }
                else if (cashDiff < 0)
                {
                    TxtDetailCashDiff.Text = $"{cashDiff:N0} đ";
                }
                else
                {
                    TxtDetailCashDiff.Text = "0 đ";
                }
            }

            // 2. VÙNG: DOANH SỐ BÁN HÀNG QUA MÁY POS
            decimal p1Open = detail.Pos1Opening;
            decimal p1Close = detail.Pos1Closing ?? 0m;
            decimal p1Night = detail.Pos1Night ?? 0m;
            decimal p1Diff = p1Close - p1Open;
            if (isNight && p1Night > 0) p1Diff += p1Night;

            decimal p2Open = detail.Pos2Opening;
            decimal p2Close = detail.Pos2Closing ?? 0m;
            decimal p2Night = detail.Pos2Night ?? 0m;
            decimal p2Diff = p2Close - p2Open;
            if (isNight && p2Night > 0) p2Diff += p2Night;

            decimal p1Rev = detail.Pos1IsActive ? ((p1Diff >= 0 ? p1Diff : 0m)) : 0m;
            decimal p2Rev = detail.Pos2IsActive ? ((p2Diff >= 0 ? p2Diff : 0m)) : 0m;

            decimal p3Open = detail.Pos3Opening;
            decimal p3Close = detail.Pos3Closing ?? 0m;
            decimal p3Night = detail.Pos3Night ?? 0m;
            decimal p3Diff = p3Close - p3Open;
            if (isNight && p3Night > 0) p3Diff += p3Night;
            decimal p3Rev = detail.Pos3IsActive ? ((p3Diff >= 0 ? p3Diff : 0m)) : 0m;

            decimal totalPosRev = p1Rev + p2Rev + p3Rev;

            if (PnlDetailPos1Group != null) PnlDetailPos1Group.Visibility = detail.Pos1IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (PnlDetailPos2Group != null) PnlDetailPos2Group.Visibility = detail.Pos2IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (PnlDetailPos3Group != null) PnlDetailPos3Group.Visibility = detail.Pos3IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (TxtDetailNoPosNotice != null) TxtDetailNoPosNotice.Visibility = (!detail.Pos1IsActive && !detail.Pos2IsActive && !detail.Pos3IsActive) ? Visibility.Visible : Visibility.Collapsed;

            if (TxtDetailPos1Label != null) TxtDetailPos1Label.Text = $"Máy POS 1 ({detail.Pos1Name})";
            if (TxtDetailPos1Diff != null) TxtDetailPos1Diff.Text = p1Diff >= 0 ? $"+{p1Diff:N0} đ" : $"{p1Diff:N0} đ";
            if (TxtDetailPos1Breakdown != null)
            {
                TxtDetailPos1Breakdown.Text = (isNight && p1Night > 0)
                    ? $"Đầu ca: {p1Open:N0} đ  |  Cuối ca: {p1Close:N0} đ  |  Chốt 02:30: {p1Night:N0} đ"
                    : $"Đầu ca: {p1Open:N0} đ  |  Cuối ca: {p1Close:N0} đ";
            }

            if (TxtDetailPos2Label != null) TxtDetailPos2Label.Text = $"Máy POS 2 ({detail.Pos2Name})";
            if (TxtDetailPos2Diff != null) TxtDetailPos2Diff.Text = p2Diff >= 0 ? $"+{p2Diff:N0} đ" : $"{p2Diff:N0} đ";
            if (TxtDetailPos2Breakdown != null)
            {
                TxtDetailPos2Breakdown.Text = (isNight && p2Night > 0)
                    ? $"Đầu ca: {p2Open:N0} đ  |  Cuối ca: {p2Close:N0} đ  |  Chốt 02:30: {p2Night:N0} đ"
                    : $"Đầu ca: {p2Open:N0} đ  |  Cuối ca: {p2Close:N0} đ";
            }

            if (TxtDetailPos3Label != null) TxtDetailPos3Label.Text = $"Máy POS 3 ({detail.Pos3Name})";
            if (TxtDetailPos3Diff != null) TxtDetailPos3Diff.Text = p3Diff >= 0 ? $"+{p3Diff:N0} đ" : $"{p3Diff:N0} đ";
            if (TxtDetailPos3Breakdown != null)
            {
                TxtDetailPos3Breakdown.Text = (isNight && p3Night > 0)
                    ? $"Đầu ca: {p3Open:N0} đ  |  Cuối ca: {p3Close:N0} đ  |  Chốt 02:30: {p3Night:N0} đ"
                    : $"Đầu ca: {p3Open:N0} đ  |  Cuối ca: {p3Close:N0} đ";
            }

            if (TxtDetailTotalPos != null) TxtDetailTotalPos.Text = totalPosRev >= 0 ? $"+{totalPosRev:N0} đ" : $"{totalPosRev:N0} đ";

            // 3. VÙNG: DOANH THU CHUYỂN KHOẢN & NGÂN HÀNG
            decimal b1Open = detail.Bank1Opening;
            decimal b1Close = detail.Bank1Closing ?? 0m;
            decimal b1Night = detail.Bank1Night ?? 0m;
            decimal b1Diff = b1Close - b1Open;
            if (isNight && b1Night > 0) b1Diff += b1Night;

            decimal b2Open = detail.Bank2Opening;
            decimal b2Close = detail.Bank2Closing ?? 0m;
            decimal b2Night = detail.Bank2Night ?? 0m;
            decimal b2Diff = b2Close - b2Open;
            if (isNight && b2Night > 0) b2Diff += b2Night;

            decimal b3Open = detail.Bank3Opening;
            decimal b3Close = detail.Bank3Closing ?? 0m;
            decimal b3Night = detail.Bank3Night ?? 0m;
            decimal b3Diff = b3Close - b3Open;
            if (isNight && b3Night > 0) b3Diff += b3Night;

            decimal b1Rev = detail.Bank1IsActive ? (b1Diff >= 0 ? b1Diff : 0m) : 0m;
            decimal b2Rev = detail.Bank2IsActive ? (b2Diff >= 0 ? b2Diff : 0m) : 0m;
            decimal b3Rev = detail.Bank3IsActive ? (b3Diff >= 0 ? b3Diff : 0m) : 0m;
            decimal totalBankRev = b1Rev + b2Rev + b3Rev;

            if (PnlDetailBank1Group != null) PnlDetailBank1Group.Visibility = detail.Bank1IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (PnlDetailBank2Group != null) PnlDetailBank2Group.Visibility = detail.Bank2IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (PnlDetailBank3Group != null) PnlDetailBank3Group.Visibility = detail.Bank3IsActive ? Visibility.Visible : Visibility.Collapsed;
            if (TxtDetailNoBankNotice != null) TxtDetailNoBankNotice.Visibility = (!detail.Bank1IsActive && !detail.Bank2IsActive && !detail.Bank3IsActive) ? Visibility.Visible : Visibility.Collapsed;

            if (TxtDetailBank1Label != null) TxtDetailBank1Label.Text = $"Ngân hàng 1 ({detail.Bank1Name})";
            if (TxtDetailBank1Diff != null) TxtDetailBank1Diff.Text = b1Diff >= 0 ? $"+{b1Diff:N0} đ" : $"{b1Diff:N0} đ";
            if (TxtDetailBank1Breakdown != null)
            {
                TxtDetailBank1Breakdown.Text = (isNight && b1Night > 0)
                    ? $"Đầu ca: {b1Open:N0} đ  |  Cuối ca: {b1Close:N0} đ  |  Chốt 02:30: {b1Night:N0} đ"
                    : $"Đầu ca: {b1Open:N0} đ  |  Cuối ca: {b1Close:N0} đ";
            }

            if (TxtDetailBank2Label != null) TxtDetailBank2Label.Text = $"Ngân hàng 2 ({detail.Bank2Name})";
            if (TxtDetailBank2Diff != null) TxtDetailBank2Diff.Text = b2Diff >= 0 ? $"+{b2Diff:N0} đ" : $"{b2Diff:N0} đ";
            if (TxtDetailBank2Breakdown != null)
            {
                TxtDetailBank2Breakdown.Text = (isNight && b2Night > 0)
                    ? $"Đầu ca: {b2Open:N0} đ  |  Cuối ca: {b2Close:N0} đ  |  Chốt 02:30: {b2Night:N0} đ"
                    : $"Đầu ca: {b2Open:N0} đ  |  Cuối ca: {b2Close:N0} đ";
            }

            if (TxtDetailBank3Label != null) TxtDetailBank3Label.Text = $"Ngân hàng 3 ({detail.Bank3Name})";
            if (TxtDetailBank3Diff != null) TxtDetailBank3Diff.Text = b3Diff >= 0 ? $"+{b3Diff:N0} đ" : $"{b3Diff:N0} đ";
            if (TxtDetailBank3Breakdown != null)
            {
                TxtDetailBank3Breakdown.Text = (isNight && b3Night > 0)
                    ? $"Đầu ca: {b3Open:N0} đ  |  Cuối ca: {b3Close:N0} đ  |  Chốt 02:30: {b3Night:N0} đ"
                    : $"Đầu ca: {b3Open:N0} đ  |  Cuối ca: {b3Close:N0} đ";
            }

            if (TxtDetailTotalBank != null) TxtDetailTotalBank.Text = totalBankRev >= 0 ? $"+{totalBankRev:N0} đ" : $"{totalBankRev:N0} đ";

            // 4. VÙNG: KHOẢN CHI XUẤT KÉT
            decimal totalExp = detail.Expenses?.Sum(x => x.Amount) ?? 0m;
            int expCount = detail.Expenses?.Count ?? 0;
            if (TxtDetailExpenseSummary != null)
            {
                TxtDetailExpenseSummary.Text = expCount > 0 ? $"Tổng đã chi: -{totalExp:N0} đ ({expCount} khoản)" : "Tổng đã chi: 0 đ";
            }

            if (detail.Expenses != null && detail.Expenses.Any())
            {
                var expenseViewItems = detail.Expenses.Select(e => new
                {
                    Description = e.Description,
                    AmountDisplay = $"-{e.Amount:N0} đ"
                }).ToList();

                if (IcDetailExpenses != null)
                {
                    IcDetailExpenses.ItemsSource = expenseViewItems;
                    IcDetailExpenses.Visibility = Visibility.Visible;
                }
                if (TxtNoExpensesNotice != null) TxtNoExpensesNotice.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (IcDetailExpenses != null)
                {
                    IcDetailExpenses.ItemsSource = null;
                    IcDetailExpenses.Visibility = Visibility.Collapsed;
                }
                if (TxtNoExpensesNotice != null) TxtNoExpensesNotice.Visibility = Visibility.Visible;
            }

            // 5. VÙNG: GIẢI TRÌNH CỦA NHÂN VIÊN
            if (TxtDetailNote != null)
            {
                TxtDetailNote.Text = string.IsNullOrWhiteSpace(detail.Note) ? "Không có ghi chú giải trình cho ca này." : detail.Note;
            }
        }


        private void DgLookupEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgLookupEmployees?.SelectedItem is EmployeeAuditItem emp)
            {
                DateTime? fromDate = DpLookupEmpFromDate?.SelectedDate;
                DateTime? toDate = DpLookupEmpToDate?.SelectedDate;

                if (TxtSelectedEmpHeader != null)
                {
                    string dateRangeInfo = (fromDate.HasValue || toDate.HasValue)
                        ? $" ({fromDate:dd/MM/yyyy} – {toDate:dd/MM/yyyy})"
                        : "";
                    TxtSelectedEmpHeader.Text = $"LỊCH SỬ CA LÀM: {emp.FullName.ToUpper()} ({emp.Username}){dateRangeInfo}";
                }

                if (TxtSelectedEmpNegativeDiff != null) TxtSelectedEmpNegativeDiff.Text = emp.TotalNegativeDiff;
                if (TxtSelectedEmpNegativeCount != null) TxtSelectedEmpNegativeCount.Text = $"{emp.NegativeShiftCount} ca bị hụt tiền";

                if (TxtSelectedEmpPositiveDiff != null) TxtSelectedEmpPositiveDiff.Text = emp.TotalPositiveDiff;
                if (TxtSelectedEmpPositiveCount != null) TxtSelectedEmpPositiveCount.Text = $"{emp.PositiveShiftCount} ca thừa tiền";

                var empShifts = _allShifts.Where(s =>
                    (s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase))) &&
                    (
                        (s.ClosedByUserId.HasValue && s.ClosedByUserId.Value == emp.Id) ||
                        (s.EmployeeUserIds != null && s.EmployeeUserIds.Contains(emp.Id)) ||
                        (!string.IsNullOrEmpty(s.ClosedByUser) && (s.ClosedByUser.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.ClosedByUser.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (!string.IsNullOrEmpty(s.OpenedByUser) && (s.OpenedByUser.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.OpenedByUser.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (!string.IsNullOrEmpty(s.EmployeeNames) && (s.EmployeeNames.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.EmployeeNames.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0))
                    )
                );

                if (fromDate.HasValue || toDate.HasValue)
                {
                    empShifts = empShifts.Where(s =>
                    {
                        if (DateTime.TryParseExact(s.ShiftDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d))
                        {
                            if (fromDate.HasValue && d.Date < fromDate.Value.Date) return false;
                            if (toDate.HasValue && d.Date > toDate.Value.Date) return false;
                            return true;
                        }
                        return false;
                    });
                }

                var shiftItems = MapToShiftSummaryItems(empShifts.ToList());
                if (DgEmployeeShiftsDetail != null)
                {
                    DgEmployeeShiftsDetail.ItemsSource = shiftItems;
                }

                if (TxtSelectedEmpShiftCount != null)
                {
                    TxtSelectedEmpShiftCount.Text = $"{shiftItems.Count} ca làm việc";
                }
            }
        }

        private void RecalculateEmployeeAuditItems(DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (_allEmployees == null) return;

            // Nếu không lọc theo khoảng ngày, khôi phục toàn bộ số liệu thống kê gốc từ máy chủ
            if (!fromDate.HasValue && !toDate.HasValue)
            {
                foreach (var emp in _allEmployees)
                {
                    emp.ShiftCount = emp.InitialShiftCount;
                    emp.TotalCashDiff = emp.InitialTotalCashDiff;
                    emp.TotalShiftsDisplay = emp.InitialTotalShiftsDisplay;
                    emp.NegativeShiftCount = emp.InitialNegativeShiftCount;
                    emp.NegativeDiffAmount = emp.InitialNegativeDiffAmount;
                    emp.TotalNegativeDiff = emp.InitialTotalNegativeDiff;
                    emp.PositiveShiftCount = emp.InitialPositiveShiftCount;
                    emp.PositiveDiffAmount = emp.InitialPositiveDiffAmount;
                    emp.TotalPositiveDiff = emp.InitialTotalPositiveDiff;
                    emp.BalancedShiftCount = emp.InitialBalancedShiftCount;
                }
                return;
            }

            if (_allShifts == null || !_allShifts.Any()) return;

            foreach (var emp in _allEmployees)
            {
                var empShifts = _allShifts.Where(s =>
                    (s.Status != null && (s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase))) &&
                    !(s.ClosedByUserId == null && (s.Note?.Contains("tự động chốt", StringComparison.OrdinalIgnoreCase) ?? false)) &&
                    (
                        (s.ClosedByUserId.HasValue && s.ClosedByUserId.Value == emp.Id) ||
                        (s.EmployeeUserIds != null && s.EmployeeUserIds.Contains(emp.Id)) ||
                        (!string.IsNullOrEmpty(s.ClosedByUser) && (s.ClosedByUser.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.ClosedByUser.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (!string.IsNullOrEmpty(s.OpenedByUser) && (s.OpenedByUser.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.OpenedByUser.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (!string.IsNullOrEmpty(s.EmployeeNames) && (s.EmployeeNames.IndexOf(emp.FullName, StringComparison.OrdinalIgnoreCase) >= 0 || s.EmployeeNames.IndexOf(emp.Username, StringComparison.OrdinalIgnoreCase) >= 0))
                    )
                );

                if (fromDate.HasValue || toDate.HasValue)
                {
                    empShifts = empShifts.Where(s =>
                    {
                        if (DateTime.TryParseExact(s.ShiftDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d))
                        {
                            if (fromDate.HasValue && d.Date < fromDate.Value.Date) return false;
                            if (toDate.HasValue && d.Date > toDate.Value.Date) return false;
                            return true;
                        }
                        return false;
                    });
                }

                var shiftList = empShifts.ToList();
                int totalShifts = shiftList.Count;
                decimal totalDiff = shiftList.Sum(s => s.CashDifference ?? 0m);

                var negShifts = shiftList.Where(s => (s.CashDifference ?? 0m) < 0m).ToList();
                decimal negDiff = negShifts.Sum(s => s.CashDifference ?? 0m);
                int negCount = negShifts.Count;

                var posShifts = shiftList.Where(s => (s.CashDifference ?? 0m) > 0m).ToList();
                decimal posDiff = posShifts.Sum(s => s.CashDifference ?? 0m);
                int posCount = posShifts.Count;

                int balancedCount = shiftList.Count(s => (s.CashDifference ?? 0m) == 0m);

                emp.ShiftCount = totalShifts;
                emp.TotalCashDiff = totalDiff;
                emp.TotalShiftsDisplay = $"{totalShifts} Ca";

                emp.NegativeShiftCount = negCount;
                emp.NegativeDiffAmount = negDiff;
                emp.TotalNegativeDiff = negDiff < 0 ? $"-{Math.Abs(negDiff):N0} đ" : "0 đ";

                emp.PositiveShiftCount = posCount;
                emp.PositiveDiffAmount = posDiff;
                emp.TotalPositiveDiff = posDiff > 0 ? $"+{posDiff:N0} đ" : "0 đ";

                emp.BalancedShiftCount = balancedCount;
            }
        }

        private void FilterLookupEmployees()
        {
            DateTime? fromDate = DpLookupEmpFromDate?.SelectedDate;
            DateTime? toDate = DpLookupEmpToDate?.SelectedDate;
            string kw = (TxtSearchLookupEmp?.Text ?? "").Trim().ToLower();

            RecalculateEmployeeAuditItems(fromDate, toDate);

            // Chỉ lấy tài khoản của nhân viên (Role == "Employee")
            var filtered = _allEmployees.Where(e => e.Role.Equals("Employee", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(kw))
            {
                filtered = filtered.Where(x => x.FullName.ToLower().Contains(kw) || x.Username.ToLower().Contains(kw));
            }

            var list = filtered.ToList();
            if (DgLookupEmployees != null)
            {
                DgLookupEmployees.ItemsSource = null;
                DgLookupEmployees.ItemsSource = list;
            }

            if (TxtLookupEmpCount != null)
            {
                TxtLookupEmpCount.Text = $"Tổng số nhân sự: {list.Count}";
            }

            if (list.Any())
            {
                if (DgLookupEmployees != null) DgLookupEmployees.SelectedIndex = 0;
            }
            else
            {
                ClearSelectedEmployeeDetails();
            }
        }

        private void ClearSelectedEmployeeDetails()
        {
            if (TxtSelectedEmpHeader != null) TxtSelectedEmpHeader.Text = "LỊCH SỬ CA LÀM CỦA NHÂN VIÊN";
            if (TxtSelectedEmpNegativeDiff != null) TxtSelectedEmpNegativeDiff.Text = "0 đ";
            if (TxtSelectedEmpNegativeCount != null) TxtSelectedEmpNegativeCount.Text = "0 ca bị hụt tiền";
            if (TxtSelectedEmpPositiveDiff != null) TxtSelectedEmpPositiveDiff.Text = "0 đ";
            if (TxtSelectedEmpPositiveCount != null) TxtSelectedEmpPositiveCount.Text = "0 ca thừa tiền";
            if (DgEmployeeShiftsDetail != null) DgEmployeeShiftsDetail.ItemsSource = null;
            if (TxtSelectedEmpShiftCount != null) TxtSelectedEmpShiftCount.Text = "0 ca làm việc";
        }

        private void BtnFilterLookupEmpDate_Click(object sender, RoutedEventArgs e)
        {
            FilterLookupEmployees();
        }

        private void BtnResetLookupEmpDate_Click(object sender, RoutedEventArgs e)
        {
            if (DpLookupEmpFromDate != null) DpLookupEmpFromDate.SelectedDate = null;
            if (DpLookupEmpToDate != null) DpLookupEmpToDate.SelectedDate = null;
            if (TxtSearchLookupEmp != null) TxtSearchLookupEmp.Text = "";
            FilterLookupEmployees();
        }

        private void TxtSearchLookupEmp_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterLookupEmployees();
        }

        private void DgEmployeeShiftsDetail_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DgEmployeeShiftsDetail?.SelectedItem is ShiftSummaryItem item)
            {
                // Chuyển sang Tab Tổng quan (Sub-tab 0)
                if (TcLookupTabs != null)
                {
                    TcLookupTabs.SelectedIndex = 0;
                }
                // Chọn ca đó trong DgShiftList
                if (DgShiftList != null && DgShiftList.ItemsSource is List<ShiftSummaryItem> list)
                {
                    int idx = list.FindIndex(x => x.RawId == item.RawId);
                    if (idx >= 0)
                    {
                        DgShiftList.SelectedIndex = idx;
                        DgShiftList.ScrollIntoView(list[idx]);
                    }
                }
            }
        }

        #endregion

        #region SUB-TAB (TRA CỨU): TIỀN CHI TRONG CA & CẢNH BÁO BẤT THƯỜNG

        private async Task LoadExpensesAsync()
        {
            try
            {
                var expenses = await ApiService.Client.GetFromJsonAsync<List<AdminExpenseDTO>>("api/Shift/expenses");
                _allExpenses = expenses ?? new List<AdminExpenseDTO>();

                if (DgExpenses != null)
                {
                    DgExpenses.ItemsSource = MapToExpenseItems(_allExpenses);
                }

                if (TxtExpenseFilterCount != null)
                {
                    TxtExpenseFilterCount.Text = $"Hiển thị: {_allExpenses.Count} phiếu chi";
                }

                CalculateTab3Cards();
                UpdateEmployeeFilterCombobox();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load expenses: " + ex.Message);
            }
        }

        private List<ExpenseAuditItem> MapToExpenseItems(List<AdminExpenseDTO> list)
        {
            return list.Select(e =>
            {
                string bName = e.BranchName;
                string empNames = e.EmployeeNames;

                int sId = e.ShiftId;
                if (sId == 0 && e.ShiftCode.StartsWith("SH-") && int.TryParse(e.ShiftCode.Substring(3), out int parsed))
                    sId = parsed;
                var sh = _allShifts.FirstOrDefault(s => s.Id == sId || s.ShiftCode == e.ShiftCode);

                if (string.IsNullOrEmpty(bName))
                {
                    bName = sh?.BranchName ?? "—";
                }

                if (string.IsNullOrEmpty(empNames) && sh != null)
                {
                    empNames = sh.EmployeeNames;
                    if (string.IsNullOrEmpty(empNames))
                        empNames = sh.ClosedByUser;
                }

                if (string.IsNullOrEmpty(empNames))
                {
                    empNames = e.CreatedByUser;
                }

                return new ExpenseAuditItem
                {
                    Id = e.Id,
                    ShiftId = e.ShiftCode,
                    BranchName = bName,
                    CreatedAt = e.CreatedAt,
                    CreatedByUser = e.CreatedByUser,
                    EmployeeNames = empNames,
                    Amount = e.AmountDisplay,
                    Description = e.Description,
                    Note = e.Note
                };
            }).ToList();
        }

        private void CalculateTab3Cards(List<AdminExpenseDTO>? currentExpenses = null)
        {
            var expList = currentExpenses ?? _allExpenses;

            // Card 1: Tổng tiền két đã chi
            decimal totalExpenseAmount = expList.Sum(e => e.Amount);
            if (TxtExpenseTotalAmount != null) TxtExpenseTotalAmount.Text = $"{totalExpenseAmount:N0} đ";
            if (TxtExpenseTotalCount != null) TxtExpenseTotalCount.Text = $"Tổng cộng: {expList.Count} phiếu xuất két";

            // Card 2: Ca lệch tiền vượt hạn mức
            int alertCount = _allShifts.Count(s => Math.Abs(s.CashDifference ?? 0m) > 0m);
            if (TxtExpenseAlertCount != null) TxtExpenseAlertCount.Text = $"{alertCount} Ca Cảnh Báo";
            if (TxtExpenseAlertSub != null) TxtExpenseAlertSub.Text = alertCount > 0 ? "Cần đối soát camera và biên bản" : "Không có ca nào bị lệch tiền";

            // Card 3: Khoản chi lớn nhất
            var maxExp = expList.OrderByDescending(e => e.Amount).FirstOrDefault();
            if (maxExp != null)
            {
                if (TxtExpenseMaxAmount != null) TxtExpenseMaxAmount.Text = $"{maxExp.Amount:N0} đ";
                if (TxtExpenseMaxDesc != null) TxtExpenseMaxDesc.Text = $"Lý do: {maxExp.Description} ({maxExp.CreatedByUser})";
            }
            else
            {
                if (TxtExpenseMaxAmount != null) TxtExpenseMaxAmount.Text = "0 đ";
                if (TxtExpenseMaxDesc != null) TxtExpenseMaxDesc.Text = "Không có khoản chi nào";
            }
        }

        private void ApplyExpenseFilter()
        {
            int branchId = (CbExpenseFilterBranch?.SelectedValue as int?) ?? 0;
            string employee = (CbExpenseFilterEmployee?.SelectedValue as string) ?? "";
            string keyword = (TxtSearchExpense?.Text ?? "").Trim().ToLower();

            var query = _allExpenses.AsEnumerable();

            // Lọc theo cơ sở
            if (branchId > 0)
            {
                var targetBranchName = _branches.FirstOrDefault(b => b.Id == branchId)?.Name ?? "";
                query = query.Where(e =>
                {
                    if (e.BranchId == branchId) return true;
                    if (!string.IsNullOrEmpty(e.BranchName) && !string.IsNullOrEmpty(targetBranchName))
                    {
                        return string.Equals(e.BranchName, targetBranchName, StringComparison.OrdinalIgnoreCase);
                    }
                    int sId = e.ShiftId;
                    if (sId == 0 && e.ShiftCode.StartsWith("SH-") && int.TryParse(e.ShiftCode.Substring(3), out int parsed))
                        sId = parsed;
                    var sh = _allShifts.FirstOrDefault(s => s.Id == sId || s.ShiftCode == e.ShiftCode);
                    return sh != null && !string.IsNullOrEmpty(sh.BranchName) && string.Equals(sh.BranchName, targetBranchName, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Lọc theo nhân viên
            if (!string.IsNullOrWhiteSpace(employee))
            {
                query = query.Where(e =>
                    string.Equals(e.CreatedByUser?.Trim(), employee.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(e.EmployeeNames) && e.EmployeeNames.IndexOf(employee.Trim(), StringComparison.OrdinalIgnoreCase) >= 0));
            }

            // Lọc theo từ khóa (lý do, ghi chú, mã ca, người bán)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(e =>
                    (e.Description?.ToLower().Contains(keyword) ?? false) ||
                    (e.Note?.ToLower().Contains(keyword) ?? false) ||
                    (e.ShiftCode?.ToLower().Contains(keyword) ?? false) ||
                    (e.CreatedByUser?.ToLower().Contains(keyword) ?? false) ||
                    (e.EmployeeNames?.ToLower().Contains(keyword) ?? false));
            }

            var filtered = query.ToList();

            if (DgExpenses != null)
            {
                DgExpenses.ItemsSource = MapToExpenseItems(filtered);
            }

            if (TxtExpenseFilterCount != null)
            {
                TxtExpenseFilterCount.Text = $"Hiển thị: {filtered.Count} phiếu chi";
            }

            CalculateTab3Cards(filtered);
        }

        private void BtnFilterExpenses_Click(object sender, RoutedEventArgs e)
        {
            ApplyExpenseFilter();
        }

        private void BtnResetExpenseFilter_Click(object sender, RoutedEventArgs e)
        {
            if (CbExpenseFilterBranch != null) CbExpenseFilterBranch.SelectedIndex = 0;
            if (CbExpenseFilterEmployee != null) CbExpenseFilterEmployee.SelectedIndex = 0;
            if (TxtSearchExpense != null) TxtSearchExpense.Text = "";
            ApplyExpenseFilter();
        }

        private void ExpenseFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyExpenseFilter();
        }

        private void TxtSearchExpense_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyExpenseFilter();
            }
        }

        #endregion

        #region TAB 4: CÀI ĐẶT HỆ THỐNG - TẠO VÀ CẤU HÌNH BIÊN BẢN CHỐT CA CHO TỪNG CƠ SỞ

        private async Task LoadBranchHandoverListAsync()
        {
            try
            {
                var configs = await ApiService.Client.GetFromJsonAsync<List<BranchHandoverConfigDTO>>("api/Branch/handover-configs");
                if (configs != null)
                {
                    _allBranchHandoverList = configs.Select(c => new BranchHandoverListItem
                    {
                        BranchId = c.BranchId,
                        BranchName = c.BranchName,
                        DefaultCashOpening = c.DefaultCashOpening,
                        BankSummary = c.Banks.Any() 
                            ? string.Join(", ", c.Banks.Where(b => b.IsActive).Select(b => $"{b.BankName} (Cổng {b.SlotIndex})")) 
                            : "(Chưa cấu hình)",
                        PosSummary = c.PosConfigs.Any() 
                            ? string.Join(", ", c.PosConfigs.Where(p => p.IsActive).Select(p => p.PosName)) 
                            : "(Chưa cấu hình)",
                        IsActive = c.IsActive,
                        RawConfig = c
                    }).ToList();

                    if (DgBranchHandoverList != null)
                    {
                        DgBranchHandoverList.ItemsSource = null;
                        DgBranchHandoverList.ItemsSource = _allBranchHandoverList;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load danh sách biên bản cơ sở: " + ex.Message);
            }
        }

        private void BtnOpenCreateBranch_Click(object sender, RoutedEventArgs e)
        {
            _editingBranchConfig = new BranchHandoverConfigDTO
            {
                BranchId = 0,
                BranchName = "",
                DefaultCashOpening = 2000000,
                IsActive = true,
                Banks = new List<BranchBankSettingDTO>
                {
                    new BranchBankSettingDTO { SlotIndex = 1, BankName = "MbBank", IsActive = true },
                    new BranchBankSettingDTO { SlotIndex = 2, BankName = "Techcombank", IsActive = true }
                },
                PosConfigs = new List<PosConfigSettingDTO>
                {
                    new PosConfigSettingDTO { DisplayOrder = 1, PosName = "Sapo POS", IsActive = true },
                    new PosConfigSettingDTO { DisplayOrder = 2, PosName = "KiotViet", IsActive = true }
                }
            };

            TxtFormHeaderTitle.Text = "TẠO MỚI BIÊN BẢN BÀN GIAO CƠ SỞ";
            PopulateBranchForm(_editingBranchConfig);
            PnlBranchListView.Visibility = Visibility.Collapsed;
            PnlBranchFormView.Visibility = Visibility.Visible;
        }

        private void BtnEditBranchHandover_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BranchHandoverListItem item)
            {
                var raw = item.RawConfig;
                _editingBranchConfig = new BranchHandoverConfigDTO
                {
                    BranchId = raw.BranchId,
                    BranchName = raw.BranchName,
                    DefaultCashOpening = raw.DefaultCashOpening,
                    IsActive = raw.IsActive,
                    Banks = raw.Banks.Select(b => new BranchBankSettingDTO
                    {
                        Id = b.Id,
                        BranchId = b.BranchId,
                        SlotIndex = b.SlotIndex,
                        BankName = b.BankName,
                        IsActive = b.IsActive,
                        ImageUrl = b.ImageUrl
                    }).ToList(),
                    PosConfigs = raw.PosConfigs.Select(p => new PosConfigSettingDTO
                    {
                        Id = p.Id,
                        BranchId = p.BranchId,
                        DisplayOrder = p.DisplayOrder,
                        PosName = p.PosName,
                        IsActive = p.IsActive,
                        ImageUrl = p.ImageUrl
                    }).ToList()
                };

                TxtFormHeaderTitle.Text = $"CẤU HÌNH BIÊN BẢN BÀN GIAO — {item.BranchName.ToUpper()}";
                PopulateBranchForm(_editingBranchConfig);
                PnlBranchListView.Visibility = Visibility.Collapsed;
                PnlBranchFormView.Visibility = Visibility.Visible;
            }
        }

        private void PopulateBranchForm(BranchHandoverConfigDTO config)
        {
            TxtFormBranchName.Text = config.BranchName;
            TxtFormCashOpening.Text = $"{config.DefaultCashOpening:N0}";
            ChkFormBranchActive.IsChecked = config.IsActive;

            DgFormBanks.ItemsSource = null;
            DgFormBanks.ItemsSource = config.Banks;

            DgFormPos.ItemsSource = null;
            DgFormPos.ItemsSource = config.PosConfigs;

            TxtNewFormBankName?.Clear();
            if (TxtNewFormBankSlot != null)
            {
                TxtNewFormBankSlot.Text = ((config.Banks.Any() ? config.Banks.Max(x => x.SlotIndex) : 0) + 1).ToString();
            }

            TxtNewFormPosName?.Clear();
            if (TxtNewFormPosOrder != null)
            {
                TxtNewFormPosOrder.Text = ((config.PosConfigs.Any() ? config.PosConfigs.Max(x => x.DisplayOrder) : 0) + 1).ToString();
            }

            _selectedNewBankLogoUrl = null;
            if (ImgNewFormBankPreview != null) ImgNewFormBankPreview.Source = null;
            _selectedNewPosLogoUrl = null;
            if (ImgNewFormPosPreview != null) ImgNewFormPosPreview.Source = null;
        }

        private void BtnBackToList_Click(object sender, RoutedEventArgs e)
        {
            PnlBranchFormView.Visibility = Visibility.Collapsed;
            PnlBranchListView.Visibility = Visibility.Visible;
        }

        private async void BtnToggleBranchActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BranchHandoverListItem item)
            {
                var newStatus = !item.IsActive;
                string actionName = newStatus ? "mở khóa hoạt động" : "khóa tạm thời";
                var confirm = MessageBox.Show($"Bạn có chắc chắn muốn {actionName} cho cơ sở '{item.BranchName}'?", 
                                              "Xác nhận thay đổi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        var res = await ApiService.Client.PutAsJsonAsync($"api/Branch/{item.BranchId}", new BranchDTO
                        {
                            Id = item.BranchId,
                            Name = item.BranchName,
                            DefaultCashOpening = item.DefaultCashOpening,
                            IsActive = newStatus
                        });

                        if (res.IsSuccessStatusCode)
                        {
                            await LoadBranchHandoverListAsync();
                            await LoadBranchesAsync();
                        }
                        else
                        {
                            var err = await res.Content.ReadAsStringAsync();
                            MessageBox.Show("Không thể thay đổi trạng thái: " + err, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi kết nối máy chủ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnPickNewBankLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ảnh Logo (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp (*.*)|*.*",
                Title = "Chọn ảnh logo ngân hàng"
            };
            if (dlg.ShowDialog() == true)
            {
                var url = await ApiService.UploadImageAsync(dlg.FileName);
                if (!string.IsNullOrEmpty(url))
                {
                    _selectedNewBankLogoUrl = url;
                    if (ImgNewFormBankPreview != null) ImgNewFormBankPreview.Source = ApiService.GetImageSource(url);
                }
            }
        }

        private async void BtnChangeBankLogo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BranchBankSettingDTO bank)
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Ảnh Logo (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp (*.*)|*.*",
                    Title = $"Chọn ảnh logo cho ngân hàng {bank.BankName}"
                };
                if (dlg.ShowDialog() == true)
                {
                    var url = await ApiService.UploadImageAsync(dlg.FileName);
                    if (!string.IsNullOrEmpty(url))
                    {
                        bank.ImageUrl = url;
                        DgFormBanks.ItemsSource = null;
                        DgFormBanks.ItemsSource = _editingBranchConfig.Banks;
                    }
                }
            }
        }

        private void BtnAddFormBank_Click(object sender, RoutedEventArgs e)
        {
            if (_editingBranchConfig.Banks.Count >= 3)
            {
                MessageBox.Show("Mỗi cơ sở chỉ được cấu hình tối đa 3 ngân hàng / ví điện tử (Cổng 1, Cổng 2 và Cổng 3)!", "Giới hạn cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string bankName = TxtNewFormBankName?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(bankName))
            {
                MessageBox.Show("Vui lòng nhập tên Ngân hàng / Ví Điện tử!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNewFormBankName?.Focus();
                return;
            }

            byte slot = byte.TryParse(TxtNewFormBankSlot?.Text, out var s) ? s : (byte)((_editingBranchConfig.Banks.Any() ? _editingBranchConfig.Banks.Max(x => x.SlotIndex) : 0) + 1);

            _editingBranchConfig.Banks.Add(new BranchBankSettingDTO
            {
                BranchId = _editingBranchConfig.BranchId,
                SlotIndex = slot,
                BankName = bankName,
                IsActive = ChkNewFormBankActive?.IsChecked == true,
                ImageUrl = _selectedNewBankLogoUrl
            });

            DgFormBanks.ItemsSource = null;
            DgFormBanks.ItemsSource = _editingBranchConfig.Banks;

            TxtNewFormBankName?.Clear();
            _selectedNewBankLogoUrl = null;
            if (ImgNewFormBankPreview != null) ImgNewFormBankPreview.Source = null;

            if (TxtNewFormBankSlot != null)
            {
                TxtNewFormBankSlot.Text = ((_editingBranchConfig.Banks.Any() ? _editingBranchConfig.Banks.Max(x => x.SlotIndex) : 0) + 1).ToString();
            }
        }

        private void BtnDeleteFormBank_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BranchBankSettingDTO bank)
            {
                _editingBranchConfig.Banks.Remove(bank);
                DgFormBanks.ItemsSource = null;
                DgFormBanks.ItemsSource = _editingBranchConfig.Banks;
            }
        }

        private async void BtnPickNewPosLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ảnh Logo (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp (*.*)|*.*",
                Title = "Chọn ảnh logo App POS"
            };
            if (dlg.ShowDialog() == true)
            {
                var url = await ApiService.UploadImageAsync(dlg.FileName);
                if (!string.IsNullOrEmpty(url))
                {
                    _selectedNewPosLogoUrl = url;
                    if (ImgNewFormPosPreview != null) ImgNewFormPosPreview.Source = ApiService.GetImageSource(url);
                }
            }
        }

        private async void BtnChangePosLogo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PosConfigSettingDTO pos)
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Ảnh Logo (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp (*.*)|*.*",
                    Title = $"Chọn ảnh logo cho App POS {pos.PosName}"
                };
                if (dlg.ShowDialog() == true)
                {
                    var url = await ApiService.UploadImageAsync(dlg.FileName);
                    if (!string.IsNullOrEmpty(url))
                    {
                        pos.ImageUrl = url;
                        DgFormPos.ItemsSource = null;
                        DgFormPos.ItemsSource = _editingBranchConfig.PosConfigs;
                    }
                }
            }
        }

        private void BtnAddFormPos_Click(object sender, RoutedEventArgs e)
        {
            if (_editingBranchConfig.PosConfigs.Count >= 3)
            {
                MessageBox.Show("Mỗi cơ sở chỉ được cấu hình tối đa 3 máy POS!", "Giới hạn cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string posName = TxtNewFormPosName?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(posName))
            {
                MessageBox.Show("Vui lòng nhập tên App POS / Máy POS!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNewFormPosName?.Focus();
                return;
            }

            byte order = byte.TryParse(TxtNewFormPosOrder?.Text, out var o) ? o : (byte)((_editingBranchConfig.PosConfigs.Any() ? _editingBranchConfig.PosConfigs.Max(x => x.DisplayOrder) : 0) + 1);

            _editingBranchConfig.PosConfigs.Add(new PosConfigSettingDTO
            {
                BranchId = _editingBranchConfig.BranchId,
                PosName = posName,
                DisplayOrder = order,
                IsActive = ChkNewFormPosActive?.IsChecked == true,
                ImageUrl = _selectedNewPosLogoUrl
            });

            DgFormPos.ItemsSource = null;
            DgFormPos.ItemsSource = _editingBranchConfig.PosConfigs;

            TxtNewFormPosName?.Clear();
            _selectedNewPosLogoUrl = null;
            if (ImgNewFormPosPreview != null) ImgNewFormPosPreview.Source = null;

            if (TxtNewFormPosOrder != null)
            {
                TxtNewFormPosOrder.Text = ((_editingBranchConfig.PosConfigs.Any() ? _editingBranchConfig.PosConfigs.Max(x => x.DisplayOrder) : 0) + 1).ToString();
            }
        }

        private void BtnDeleteFormPos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PosConfigSettingDTO pos)
            {
                _editingBranchConfig.PosConfigs.Remove(pos);
                DgFormPos.ItemsSource = null;
                DgFormPos.ItemsSource = _editingBranchConfig.PosConfigs;
            }
        }

        private async void BtnSaveBranchHandover_Click(object sender, RoutedEventArgs e)
        {
            if (_editingBranchConfig.Banks.Count > 3)
            {
                MessageBox.Show("Mỗi cơ sở chỉ được cấu hình tối đa 3 ngân hàng / ví điện tử! Vui lòng xóa bớt trước khi lưu.", "Giới hạn cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingBranchConfig.PosConfigs.Count > 3)
            {
                MessageBox.Show("Mỗi cơ sở chỉ được cấu hình tối đa 3 máy POS! Vui lòng xóa bớt máy POS trước khi lưu.", "Giới hạn cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string branchName = TxtFormBranchName?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(branchName))
            {
                MessageBox.Show("Vui lòng nhập tên cơ sở bán hàng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormBranchName?.Focus();
                return;
            }

            string rawCash = (TxtFormCashOpening?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
            if (!decimal.TryParse(rawCash, out decimal cash) || cash < 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền mặt két đầu ca hợp lệ!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormCashOpening?.Focus();
                return;
            }

            _editingBranchConfig.BranchName = branchName;
            _editingBranchConfig.DefaultCashOpening = cash;
            _editingBranchConfig.IsActive = ChkFormBranchActive?.IsChecked == true;

            try
            {
                var res = await ApiService.Client.PostAsJsonAsync("api/Branch/handover-config", _editingBranchConfig);
                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Lưu cấu hình biên bản cho cơ sở '{branchName}' thành công!", 
                                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadBranchHandoverListAsync();
                    await LoadBranchesAsync();

                    PnlBranchFormView.Visibility = Visibility.Collapsed;
                    PnlBranchListView.Visibility = Visibility.Visible;
                }
                else
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Không thể lưu biên bản cơ sở: {err}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối máy chủ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnSearchEmployee_Click(object sender, RoutedEventArgs e)
        {
            string keyword = (TxtSearchEmployee?.Text ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(keyword))
            {
                DgEmployees.ItemsSource = _allEmployees;
            }
            else
            {
                DgEmployees.ItemsSource = _allEmployees
                    .Where(x => x.FullName.ToLower().Contains(keyword) || x.Username.ToLower().Contains(keyword))
                    .ToList();
            }
        }

        private async void BtnRefreshEmployees_Click(object sender, RoutedEventArgs e)
        {
            await LoadEmployeeDataAsync();
        }

        private void BtnExportEmployees_Click(object sender, RoutedEventArgs e)
        {
            var exportList = (DgEmployees?.ItemsSource as System.Collections.Generic.IEnumerable<EmployeeAuditItem>)?.ToList() ?? _allEmployees;
            if (exportList == null || !exportList.Any())
            {
                MessageBox.Show("Hiện không có dữ liệu nhân viên nào trong danh sách để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string defaultFileName = $"DanhSach_NhanVien_PlusMart_{DateTime.Now:dd-MM-yyyy}.csv";
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "File Excel / CSV (*.csv)|*.csv|Tất cả tệp (*.*)|*.*",
                    FileName = defaultFileName,
                    Title = "Xuất dữ liệu danh sách nhân sự và theo dõi chênh lệch"
                };

                if (sfd.ShowDialog() != true) return;

                var sb = new System.Text.StringBuilder();

                // Dòng tiêu đề cột (Header)
                sb.AppendLine("Mã NV,Tên tài khoản,Họ và tên,Vai trò,Số ca đã làm,Số ca Âm,Tổng tiền Âm,Số ca Dương,Tổng tiền Dương,Số ca Khớp,Trạng thái");

                foreach (var emp in exportList)
                {
                    string id = emp.Id.ToString();
                    string username = EscapeCsv(emp.Username);
                    string fullName = EscapeCsv(emp.FullName);
                    string role = EscapeCsv(emp.Role);
                    string shiftCount = emp.ShiftCount.ToString();
                    string negCount = emp.NegativeShiftCount.ToString();
                    string negAmount = emp.NegativeDiffAmount.ToString("0");
                    string posCount = emp.PositiveShiftCount.ToString();
                    string posAmount = emp.PositiveDiffAmount.ToString("0");
                    string balCount = emp.BalancedShiftCount.ToString();
                    string status = EscapeCsv(emp.IsActive);

                    sb.AppendLine($"{id},{username},{fullName},{role},{shiftCount},{negCount},{negAmount},{posCount},{posAmount},{balCount},{status}");
                }

                // Dòng tổng kết ở cuối file
                int totalShifts = exportList.Sum(x => x.ShiftCount);
                int totalNegShifts = exportList.Sum(x => x.NegativeShiftCount);
                decimal totalNegMoney = exportList.Sum(x => x.NegativeDiffAmount);
                int totalPosShifts = exportList.Sum(x => x.PositiveShiftCount);
                decimal totalPosMoney = exportList.Sum(x => x.PositiveDiffAmount);
                int totalBalShifts = exportList.Sum(x => x.BalancedShiftCount);

                sb.AppendLine();
                sb.AppendLine($"Tổng cộng,{exportList.Count} nhân sự,,,{totalShifts},{totalNegShifts},{totalNegMoney:0},{totalPosShifts},{totalPosMoney:0},{totalBalShifts},");

                // Ghi file UTF-8 có BOM để Excel hiển thị tiếng Việt chuẩn xác 100%
                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));

                MessageBox.Show($"Đã xuất dữ liệu thành công {exportList.Count} nhân viên ra file:\n\n{sfd.FileName}",
                                "Xuất Dữ Liệu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất file danh sách nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EmployeeEditDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.ResultUser != null)
            {
                try
                {
                    var res = await ApiService.Client.PostAsJsonAsync("api/User", dialog.ResultUser);
                    if (res.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Thêm nhân viên '{dialog.ResultUser.FullName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadEmployeeDataAsync();
                    }
                    else
                    {
                        var msg = await res.Content.ReadAsStringAsync();
                        MessageBox.Show($"Không thể thêm nhân viên: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi thêm nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnEditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EmployeeAuditItem emp)
            {
                try
                {
                    var u = await ApiService.Client.GetFromJsonAsync<UserDTO>($"api/User/{emp.Id}");
                    if (u != null)
                    {
                        var dialog = new EmployeeEditDialog(u);
                        dialog.Owner = this;
                        if (dialog.ShowDialog() == true && dialog.ResultUser != null)
                        {
                            var res = await ApiService.Client.PutAsJsonAsync($"api/User/{emp.Id}", dialog.ResultUser);
                            if (res.IsSuccessStatusCode)
                            {
                                MessageBox.Show($"Cập nhật nhân viên '{dialog.ResultUser.FullName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                await LoadEmployeeDataAsync();
                            }
                            else
                            {
                                var msg = await res.Content.ReadAsStringAsync();
                                MessageBox.Show($"Không thể cập nhật nhân viên: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi tải thông tin nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EmployeeAuditItem emp)
            {
                var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa nhân viên '{emp.FullName}' ({emp.Username}) khỏi hệ thống?\n\n(Lưu ý: Nếu nhân viên đã có ca làm việc lịch sử, tài khoản sẽ được chuyển sang trạng thái ngưng hoạt động để bảo vệ toàn vẹn dữ liệu ca)",
                    "Xác nhận xóa nhân viên", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        var res = await ApiService.Client.DeleteAsync($"api/User/{emp.Id}");
                        if (res.IsSuccessStatusCode)
                        {
                            MessageBox.Show($"Đã xóa / ngừng kích hoạt nhân viên '{emp.FullName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadEmployeeDataAsync();
                        }
                        else
                        {
                            var msg = await res.Content.ReadAsStringAsync();
                            MessageBox.Show($"Không thể xóa nhân viên: {msg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi xóa nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnToggleLockEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EmployeeAuditItem emp)
            {
                var confirm = MessageBox.Show($"Bạn có chắc chắn muốn thay đổi trạng thái Khóa / Mở khóa cho nhân viên '{emp.FullName}' ({emp.Username})?",
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        var u = await ApiService.Client.GetFromJsonAsync<UserDTO>($"api/User/{emp.Id}");
                        if (u != null)
                        {
                            u.IsActive = !u.IsActive;
                            var res = await ApiService.Client.PutAsJsonAsync($"api/User/{emp.Id}", u);
                            if (res.IsSuccessStatusCode)
                            {
                                MessageBox.Show($"Đã cập nhật trạng thái nhân viên: {(u.IsActive ? "Hoạt động" : "Đã khóa")}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                                await LoadEmployeeDataAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi cập nhật nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async Task LoadEmployeeDataAsync()
        {
            try
            {
                var res = await ApiService.Client.GetAsync("api/Shift/employee-statistics");
                if (res.IsSuccessStatusCode)
                {
                    var stats = await res.Content.ReadFromJsonAsync<List<EmployeeShiftStatisticsDTO>>();
                    if (stats != null)
                    {
                        _allEmployees = stats.Select(s =>
                        {
                            decimal totalDiff = s.TotalPositiveDiff + s.TotalNegativeDiff;
                            string totalShiftsDisplay = $"{s.TotalShifts} Ca";
                            string negDisplay = s.TotalNegativeDiff < 0 ? $"-{Math.Abs(s.TotalNegativeDiff):N0} đ" : "0 đ";
                            string posDisplay = s.TotalPositiveDiff > 0 ? $"+{s.TotalPositiveDiff:N0} đ" : "0 đ";

                            return new EmployeeAuditItem
                            {
                                Id = s.UserId,
                                Username = s.Username,
                                FullName = s.FullName,
                                Role = s.Role,
                                ShiftCount = s.TotalShifts,
                                TotalCashDiff = totalDiff,
                                TotalShiftsDisplay = totalShiftsDisplay,
                                NegativeShiftCount = s.NegativeShiftsCount,
                                NegativeDiffAmount = s.TotalNegativeDiff,
                                TotalNegativeDiff = negDisplay,
                                PositiveShiftCount = s.PositiveShiftsCount,
                                PositiveDiffAmount = s.TotalPositiveDiff,
                                TotalPositiveDiff = posDisplay,
                                BalancedShiftCount = s.BalancedShiftsCount,
                                IsActive = s.IsActive ? "Hoạt động" : "Đã khóa",

                                InitialShiftCount = s.TotalShifts,
                                InitialTotalCashDiff = totalDiff,
                                InitialTotalShiftsDisplay = totalShiftsDisplay,
                                InitialNegativeShiftCount = s.NegativeShiftsCount,
                                InitialNegativeDiffAmount = s.TotalNegativeDiff,
                                InitialTotalNegativeDiff = negDisplay,
                                InitialPositiveShiftCount = s.PositiveShiftsCount,
                                InitialPositiveDiffAmount = s.TotalPositiveDiff,
                                InitialTotalPositiveDiff = posDisplay,
                                InitialBalancedShiftCount = s.BalancedShiftsCount
                            };
                        }).ToList();

                        if (DgEmployees != null)
                            DgEmployees.ItemsSource = _allEmployees;

                        if (TxtEmployeeCountHeader != null)
                            TxtEmployeeCountHeader.Text = $"Tổng: {_allEmployees.Count} Nhân sự";

                        FilterLookupEmployees();

                        UpdateEmployeeFilterCombobox();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load employee statistics: " + ex.Message);
            }
        }

        #endregion

        #region SUB-TAB 3: RESET HỆ THỐNG (LỌC & XÓA DỮ LIỆU GIAO DỊCH CA)
        private List<ResetShiftItemDTO> _resetShifts = new();

        private async Task LoadResetShiftsAsync()
        {
            try
            {
                string fromDate = DpResetFromDate?.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
                string toDate = DpResetToDate?.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
                int branchId = 0;
                if (CbResetBranch?.SelectedValue is int bId)
                {
                    branchId = bId;
                }

                string url = $"api/Shift/reset-filter?fromDate={fromDate}&toDate={toDate}&branchId={branchId}";
                var items = await ApiService.Client.GetFromJsonAsync<List<ResetShiftItemDTO>>(url);
                _resetShifts = items ?? new List<ResetShiftItemDTO>();

                if (DgResetShifts != null)
                {
                    DgResetShifts.ItemsSource = _resetShifts;
                }

                if (TxtResetTotalShifts != null)
                {
                    TxtResetTotalShifts.Text = $"Tổng: {_resetShifts.Count} ca";
                }
                if (TxtResetTotalNeg != null)
                {
                    decimal totalNeg = _resetShifts.Sum(x => x.NegativeAmount);
                    TxtResetTotalNeg.Text = $"Tổng Âm: {totalNeg:N0} đ";
                }
                if (TxtResetTotalPos != null)
                {
                    decimal totalPos = _resetShifts.Sum(x => x.PositiveAmount);
                    TxtResetTotalPos.Text = $"Tổng Dương: {totalPos:N0} đ";
                }
                if (TxtResetGridCount != null)
                {
                    TxtResetGridCount.Text = $"Hiển thị: {_resetShifts.Count} ca";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load reset shifts: " + ex.Message);
            }
        }

        private async void BtnFilterResetShifts_Click(object sender, RoutedEventArgs e)
        {
            await LoadResetShiftsAsync();
        }

        private async void BtnRefreshResetShifts_Click(object sender, RoutedEventArgs e)
        {
            if (DpResetFromDate != null) DpResetFromDate.SelectedDate = DateTime.Today.AddDays(-30);
            if (DpResetToDate != null) DpResetToDate.SelectedDate = DateTime.Today;
            if (CbResetBranch != null) CbResetBranch.SelectedIndex = 0;
            await LoadResetShiftsAsync();
        }

        private async void BtnConfirmResetData_Click(object sender, RoutedEventArgs e)
        {
            if (_resetShifts == null || !_resetShifts.Any())
            {
                MessageBox.Show("Không có ca làm việc nào trong danh sách bộ lọc hiện tại để xóa!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string fromStr = DpResetFromDate?.SelectedDate?.ToString("dd/MM/yyyy") ?? "—";
            string toStr = DpResetToDate?.SelectedDate?.ToString("dd/MM/yyyy") ?? "—";
            string dateRangeText = $"Từ ngày {fromStr} đến ngày {toStr}";

            // Mở dialog xác thực quyền Admin
            var dialog = new AdminSecurityConfirmDialog(_resetShifts.Count, dateRangeText);
            dialog.Owner = this;
            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult == true && dialog.Confirmed)
            {
                // Nếu tùy chọn tự động sao lưu được chọn -> Xuất file an toàn trước khi xóa
                if (dialog.AutoBackup)
                {
                    bool saved = ExportResetShiftsToCsv(showSuccessMessage: false);
                    if (!saved)
                    {
                        MessageBox.Show("Thao tác xóa đã được tạm dừng vì bạn chưa hoàn tất việc lưu file sao lưu an toàn.\n\nDữ liệu vẫn được giữ nguyên vẹn trên hệ thống.", 
                                        "Đã Dừng Thao Tác Xóa", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                try
                {
                    var req = new ResetShiftsRequestDTO
                    {
                        AdminUsername = dialog.AdminUsername,
                        AdminPassword = dialog.AdminPassword,
                        ShiftIds = _resetShifts.Select(x => x.Id).ToList()
                    };

                    var response = await ApiService.Client.PostAsJsonAsync("api/Shift/reset-shifts", req);
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadFromJsonAsync<ResetShiftsResponseDTO>();
                        MessageBox.Show(res?.Message ?? $"Đã xóa thành công {_resetShifts.Count} ca làm việc!", "Reset Dữ Liệu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Nạp lại toàn bộ dữ liệu trên Dashboard và bảng Reset
                        await LoadResetShiftsAsync();
                        await LoadAllDashboardDataAsync();
                    }
                    else
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        MessageBox.Show("Không thể thực hiện xóa dữ liệu:\n" + err, "Lỗi Máy Chủ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi kết nối khi gửi yêu cầu xóa: " + ex.Message, "Lỗi Kết Nối", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportResetBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_resetShifts == null || !_resetShifts.Any())
            {
                MessageBox.Show("Hiện không có dữ liệu ca làm việc nào trong danh sách lọc để xuất sao lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ExportResetShiftsToCsv(showSuccessMessage: true);
        }

        private bool ExportResetShiftsToCsv(bool showSuccessMessage)
        {
            if (_resetShifts == null || !_resetShifts.Any())
            {
                return false;
            }

            try
            {
                string fromStr = DpResetFromDate?.SelectedDate?.ToString("dd-MM-yyyy") ?? "BatDau";
                string toStr = DpResetToDate?.SelectedDate?.ToString("dd-MM-yyyy") ?? "KetThuc";
                string defaultFileName = $"SaoLuu_CaLamViec_PlusMart_{fromStr}_Den_{toStr}.csv";

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "File Excel / CSV (*.csv)|*.csv|Tất cả tệp (*.*)|*.*",
                    FileName = defaultFileName,
                    Title = "Lưu file sao lưu dữ liệu ca làm việc"
                };

                if (sfd.ShowDialog() != true)
                {
                    return false;
                }

                var sb = new System.Text.StringBuilder();

                // Dòng tiêu đề cột (Header)
                sb.AppendLine("STT,Mã Ca / Đơn,Ngày Làm Việc,Ca Làm,Cơ Sở,Người Phụ Trách,Tiền Âm (Thiếu),Tiền Dương (Thừa),Trạng Thái");

                int idx = 1;
                foreach (var s in _resetShifts)
                {
                    string code = EscapeCsv(s.ShiftCode);
                    string date = EscapeCsv(s.ShiftDateDisplay);
                    string shiftType = EscapeCsv(s.ShiftTypeDisplay);
                    string branch = EscapeCsv(s.BranchName);
                    string pic = EscapeCsv(s.PersonInCharge);
                    string neg = s.NegativeAmount.ToString("0");
                    string pos = s.PositiveAmount.ToString("0");
                    string status = EscapeCsv(s.StatusDisplay);

                    sb.AppendLine($"{idx},{code},{date},{shiftType},{branch},{pic},{neg},{pos},{status}");
                    idx++;
                }

                // Dòng tổng kết ở cuối file
                decimal sumNeg = _resetShifts.Sum(x => x.NegativeAmount);
                decimal sumPos = _resetShifts.Sum(x => x.PositiveAmount);
                sb.AppendLine();
                sb.AppendLine($"Tổng cộng,{_resetShifts.Count} ca,,,,,{sumNeg:0},{sumPos:0},");

                // Ghi file UTF-8 có BOM để Excel hiển thị tiếng Việt không bao giờ lỗi font
                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));

                if (showSuccessMessage)
                {
                    MessageBox.Show($"Đã xuất sao lưu thành công {_resetShifts.Count} ca làm việc ra file:\n\n{sfd.FileName}", 
                                    "Xuất Sao Lưu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất file sao lưu dữ liệu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
        #endregion

        #region SUB-TAB 4: QUẢN LÝ TÀI KHOẢN VÀ ĐỔI MẬT KHẨU ADMIN
        private void BtnQuickChangeAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (TcMainTabs != null && TabSystemSettings != null)
            {
                TcMainTabs.SelectedItem = TabSystemSettings;
            }
            if (TcSettingsTabs != null)
            {
                // Chọn Sub-tab 4: Tài khoản Admin (index 3)
                TcSettingsTabs.SelectedIndex = 3;
            }
        }

        private void BtnResetAdminAccountForm_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAdminUser != null)
            {
                if (TxtAdminCurUsername != null) TxtAdminCurUsername.Text = _currentAdminUser.Username;
                if (TxtAdminNewUsername != null) TxtAdminNewUsername.Text = _currentAdminUser.Username;
                if (TxtAdminNewFullName != null) TxtAdminNewFullName.Text = _currentAdminUser.FullName;
            }
            else
            {
                if (TxtAdminCurUsername != null) TxtAdminCurUsername.Text = "";
                if (TxtAdminNewUsername != null) TxtAdminNewUsername.Text = "";
                if (TxtAdminNewFullName != null) TxtAdminNewFullName.Text = "";
            }

            if (PbAdminCurPassword != null) PbAdminCurPassword.Password = "";
            if (PbAdminNewPassword != null) PbAdminNewPassword.Password = "";
            if (PbAdminConfirmPassword != null) PbAdminConfirmPassword.Password = "";
        }

        private async void BtnSaveAdminAccount_Click(object sender, RoutedEventArgs e)
        {
            string curUser = TxtAdminCurUsername?.Text?.Trim() ?? "";
            string curPass = PbAdminCurPassword?.Password?.Trim() ?? "";
            string newUser = TxtAdminNewUsername?.Text?.Trim() ?? "";
            string newFullName = TxtAdminNewFullName?.Text?.Trim() ?? "";
            string newPass = PbAdminNewPassword?.Password?.Trim() ?? "";
            string confirmPass = PbAdminConfirmPassword?.Password?.Trim() ?? "";

            if (string.IsNullOrEmpty(curUser) || string.IsNullOrEmpty(curPass))
            {
                MessageBox.Show("Vui lòng nhập tên tài khoản và mật khẩu Quản trị viên hiện tại để xác thực!", "Thiếu thông tin xác thực", MessageBoxButton.OK, MessageBoxImage.Warning);
                PbAdminCurPassword?.Focus();
                return;
            }

            if (string.IsNullOrEmpty(newUser))
            {
                MessageBox.Show("Vui lòng nhập tên tài khoản mới cho Quản trị viên!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtAdminNewUsername?.Focus();
                return;
            }

            if (string.IsNullOrEmpty(newPass))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu mới!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                PbAdminNewPassword?.Focus();
                return;
            }

            if (newPass != confirmPass)
            {
                MessageBox.Show("Mật khẩu mới và mật khẩu xác nhận không trùng khớp nhau!", "Xác nhận mật khẩu sai", MessageBoxButton.OK, MessageBoxImage.Warning);
                PbAdminConfirmPassword?.Focus();
                return;
            }

            if (newPass.Length < 4)
            {
                MessageBox.Show("Mật khẩu mới phải có ít nhất 4 ký tự!", "Mật khẩu quá ngắn", MessageBoxButton.OK, MessageBoxImage.Warning);
                PbAdminNewPassword?.Focus();
                return;
            }

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn thay đổi thông tin tài khoản Quản trị viên?\n\n- Tên tài khoản mới: {newUser}\n- Họ tên hiển thị: {newFullName}\n\n* Lưu ý: Bạn cần ghi nhớ mật khẩu mới này cho lần đăng nhập tiếp theo.", 
                                          "Xác nhận thay đổi tài khoản Admin", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var dto = new ChangeAdminAccountDTO
                {
                    CurrentUsername = curUser,
                    CurrentPassword = curPass,
                    NewUsername = newUser,
                    NewFullName = newFullName,
                    NewPassword = newPass
                };

                var res = await ApiService.Client.PostAsJsonAsync("api/User/change-admin", dto);
                if (res.IsSuccessStatusCode)
                {
                    var msg = await res.Content.ReadAsStringAsync();
                    MessageBox.Show(string.IsNullOrWhiteSpace(msg) ? "Thay đổi tài khoản và mật khẩu Quản trị viên thành công!" : msg, 
                                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Cập nhật phiên đăng nhập
                    if (_currentAdminUser != null)
                    {
                        _currentAdminUser.Username = newUser;
                        _currentAdminUser.FullName = newFullName;
                    }
                    else
                    {
                        _currentAdminUser = new UserDTO
                        {
                            Username = newUser,
                            FullName = newFullName,
                            Role = "Admin"
                        };
                    }

                    if (TxtCurrentAdminHeader != null)
                    {
                        string displayName = !string.IsNullOrWhiteSpace(newFullName) ? newFullName : newUser;
                        TxtCurrentAdminHeader.Text = $"Quản trị viên: {displayName}";
                    }

                    if (TxtAdminCurUsername != null) TxtAdminCurUsername.Text = newUser;
                    if (PbAdminCurPassword != null) PbAdminCurPassword.Password = "";
                    if (PbAdminNewPassword != null) PbAdminNewPassword.Password = "";
                    if (PbAdminConfirmPassword != null) PbAdminConfirmPassword.Password = "";
                }
                else
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show("Không thể thay đổi thông tin Admin:\n" + err, "Lỗi máy chủ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối khi gửi yêu cầu đổi tài khoản Admin: " + ex.Message, "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var login = new Login();
            login.Show();
            this.Close();
        }
    }

    public class ShiftSummaryItem
    {
        public int RawId { get; set; }
        public string ShiftId { get; set; } = string.Empty;
        public string ShiftDate { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string ClosedByUser { get; set; } = string.Empty;
        public string OpenedByUser { get; set; } = string.Empty;
        public string EmployeeNames { get; set; } = string.Empty;
        public string CashDifference { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CashDiffDisplay { get; set; } = "0 đ";
        public string BankDiffDisplay { get; set; } = "0 đ";
    }

    public class EmployeeAuditItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ShiftCount { get; set; }
        public decimal TotalCashDiff { get; set; }
        public string TotalShiftsDisplay { get; set; } = "0 Ca";
        public int NegativeShiftCount { get; set; }
        public decimal NegativeDiffAmount { get; set; }
        public string TotalNegativeDiff { get; set; } = "0 đ";
        public int PositiveShiftCount { get; set; }
        public decimal PositiveDiffAmount { get; set; }
        public string TotalPositiveDiff { get; set; } = "0 đ";
        public int BalancedShiftCount { get; set; }
        public string IsActive { get; set; } = string.Empty;

        // Lưu giữ số liệu gốc từ máy chủ cho toàn bộ thời gian
        public int InitialShiftCount { get; set; }
        public decimal InitialTotalCashDiff { get; set; }
        public string InitialTotalShiftsDisplay { get; set; } = "0 Ca";
        public int InitialNegativeShiftCount { get; set; }
        public decimal InitialNegativeDiffAmount { get; set; }
        public string InitialTotalNegativeDiff { get; set; } = "0 đ";
        public int InitialPositiveShiftCount { get; set; }
        public decimal InitialPositiveDiffAmount { get; set; }
        public string InitialTotalPositiveDiff { get; set; } = "0 đ";
        public int InitialBalancedShiftCount { get; set; }
    }

    public class ExpenseAuditItem
    {
        public int Id { get; set; }
        public string ShiftId { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedByUser { get; set; } = string.Empty;
        public string EmployeeNames { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class BranchHandoverListItem
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal DefaultCashOpening { get; set; }
        public string DefaultCashOpeningDisplay => $"{DefaultCashOpening:N0} đ";
        public string BankSummary { get; set; } = string.Empty;
        public string PosSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string StatusText => IsActive ? "Hoạt động" : "Không hoạt động";
        public string StatusColor => IsActive ? "#16A34A" : "#000000";
        public string StatusDisplay => StatusText;
        public string ToggleLockContent => IsActive ? "Khóa" : "Mở khóa";
        public BranchHandoverConfigDTO RawConfig { get; set; } = new();
    }
}

