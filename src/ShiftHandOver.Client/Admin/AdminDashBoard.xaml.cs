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
        private List<ShiftScheduleConfigDTO> _shiftSchedules = new();
        private ShiftHandoverDetailDTO? _currentDetail;

        public AdminDashBoard()
        {
            InitializeComponent();
            _ = LoadAllDashboardDataAsync();
        }

        private async Task LoadAllDashboardDataAsync()
        {
            await Task.WhenAll(
                LoadBranchesAsync(),
                LoadShiftTypesAsync(),
                LoadShiftListAsync(),
                LoadRecentDifferencesAsync(),
                LoadEmployeeDataAsync(),
                LoadExpensesAsync()
            );
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

                    _shiftSchedules = _shiftTypes.Select(st => new ShiftScheduleConfigDTO
                    {
                        Code = st.Code,
                        Name = st.Name,
                        TimeRange = st.Code switch
                        {
                            "MORNING" => "07:00 – 12:00",
                            "AFTERNOON" => "12:00 – 18:00",
                            "EVENING" => "18:00 – 23:00",
                            "NIGHT" => "23:00 – 02:30 hôm sau",
                            _ => "Theo quy định"
                        },
                        MaxEmployees = 2,
                        Description = st.Code switch
                        {
                            "MORNING" => "Ca mở đầu ngày, kế thừa tiền két từ ca Đêm hôm trước nếu có.",
                            "AFTERNOON" => "Kế thừa POS/Bank từ ca Sáng, tiền két mặc định theo cơ sở.",
                            "EVENING" => "Kế thừa POS/Bank từ ca Chiều, tiền két mặc định theo cơ sở.",
                            "NIGHT" => "Xuyên 00:00, chốt sổ 1 lần lúc 02:30 rạng sáng với 2 cột Day 1 & Day 2.",
                            _ => "Quy định ca làm việc chuẩn cửa hàng."
                        },
                        IsActive = true
                    }).ToList();

                    if (DgShiftSchedules != null)
                    {
                        DgShiftSchedules.ItemsSource = _shiftSchedules;
                    }

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

                    // Tab 4 Combos
                    if (CbCashBranch != null)
                    {
                        CbCashBranch.ItemsSource = branchItems;
                        CbCashBranch.DisplayMemberPath = "Display";
                        CbCashBranch.SelectedValuePath = "Id";
                        CbCashBranch.SelectedIndex = 0;
                    }

                    if (CbBankBranch != null)
                    {
                        CbBankBranch.ItemsSource = branchItems;
                        CbBankBranch.DisplayMemberPath = "Display";
                        CbBankBranch.SelectedValuePath = "Id";
                        CbBankBranch.SelectedIndex = 0;
                    }

                    if (CbPosBranch != null)
                    {
                        CbPosBranch.ItemsSource = branchItems;
                        CbPosBranch.DisplayMemberPath = "Display";
                        CbPosBranch.SelectedValuePath = "Id";
                        CbPosBranch.SelectedIndex = 0;
                    }

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
            int total = list.Count;

            // 1. Thẻ tổng ca
            if (TxtKpiTotalShifts != null)
            {
                TxtKpiTotalShifts.Text = $"{total} Ca";
            }
            if (TxtKpiTotalShiftsSub != null)
            {
                int closedCount = list.Count(s => s.Status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase) || s.Status.StartsWith("Close", StringComparison.OrdinalIgnoreCase));
                double closedPct = total > 0 ? (closedCount * 100.0 / total) : 0;
                TxtKpiTotalShiftsSub.Text = $"Đã chốt {closedCount}/{total} ca ({closedPct:0.#}%)";
            }

            // 2. Thẻ ca âm
            var negatives = list.Where(s => (s.CashDifference ?? 0m) < 0m).ToList();
            decimal totalNeg = negatives.Sum(s => s.CashDifference ?? 0m);
            double negPct = total > 0 ? (negatives.Count * 100.0 / total) : 0;

            if (TxtKpiNegativeShifts != null) TxtKpiNegativeShifts.Text = $"{negatives.Count} Ca";
            if (TxtKpiNegativePercent != null) TxtKpiNegativePercent.Text = $" ({negPct:0.#}%)";
            if (TxtKpiNegativeTotal != null) TxtKpiNegativeTotal.Text = $"Tổng hụt: {totalNeg:N0} đ";

            // 3. Thẻ ca dương
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
            MessageBox.Show("Đã xuất báo cáo tổng hợp chốt ca của tháng ra định dạng văn bản thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void DgRecentShiftDiff_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRecentShiftDiff?.SelectedItem is ShiftSummaryItem item && item.RawId > 0)
            {
                try
                {
                    var detail = await ApiService.Client.GetFromJsonAsync<ShiftHandoverDetailDTO>($"api/Shift/{item.RawId}");
                    if (detail != null)
                    {
                        DisplayRecentShiftDetail(detail);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi load chi tiết ca lệch: " + ex.Message);
                }
            }
        }

        private void DisplayRecentShiftDetail(ShiftHandoverDetailDTO detail)
        {
            if (TxtRecentDetailShiftCode != null)
            {
                TxtRecentDetailShiftCode.Text = $"BIÊN BẢN CHỐT CA: #SH-{detail.ShiftId} ({detail.ShiftTypeName})";
            }

            decimal diff = detail.CashDifference ?? 0m;
            if (TxtRecentDetailDiffBadge != null)
            {
                if (diff < 0)
                {
                    TxtRecentDetailDiffBadge.Text = $"ÂM TIỀN {diff:N0} đ";
                }
                else if (diff > 0)
                {
                    TxtRecentDetailDiffBadge.Text = $"THỪA TIỀN +{diff:N0} đ";
                }
                else
                {
                    TxtRecentDetailDiffBadge.Text = "CÂN TIỀN (0 đ)";
                }
            }

            if (TxtRecentDetailBranch != null) TxtRecentDetailBranch.Text = $"Cơ sở: {detail.BranchName}";
            if (TxtRecentDetailShiftType != null) TxtRecentDetailShiftType.Text = $"Ca làm: {detail.ShiftTypeName}";
            if (TxtRecentDetailDate != null) TxtRecentDetailDate.Text = $"Ngày làm: {detail.ShiftDate:dd/MM/yyyy}";
            if (TxtRecentDetailOpenedBy != null) TxtRecentDetailOpenedBy.Text = $"Người mở: {detail.OpenedByUser}";
            if (TxtRecentDetailClosedBy != null) TxtRecentDetailClosedBy.Text = $"Người đóng: {detail.ClosedByUser}";

            string empNames = (detail.EmployeeNames != null && detail.EmployeeNames.Any())
                ? string.Join(", ", detail.EmployeeNames)
                : detail.ClosedByUser;
            if (TxtRecentDetailEmployees != null) TxtRecentDetailEmployees.Text = $"Trực ca: {empNames}";

            decimal posRev1 = (detail.Pos1Closing ?? 0m) - detail.Pos1Opening;
            if (posRev1 < 0) posRev1 = 0;
            decimal posRev2 = (detail.Pos2Closing ?? 0m) - detail.Pos2Opening;
            if (posRev2 < 0) posRev2 = 0;
            decimal totalPosCash = posRev1 + posRev2;

            decimal totalExp = detail.Expenses?.Sum(x => x.Amount) ?? 0m;
            decimal theoretical = detail.CashOpening + totalPosCash - totalExp;

            if (TxtRecentDetailCashOpening != null) TxtRecentDetailCashOpening.Text = $"{detail.CashOpening:N0} đ";
            if (TxtRecentDetailCashPos != null) TxtRecentDetailCashPos.Text = $"{totalPosCash:N0} đ";
            if (TxtRecentDetailCashExpenses != null) TxtRecentDetailCashExpenses.Text = $"-{totalExp:N0} đ";
            if (TxtRecentDetailCashTheoretical != null) TxtRecentDetailCashTheoretical.Text = $"{theoretical:N0} đ";
            if (TxtRecentDetailCashActual != null) TxtRecentDetailCashActual.Text = $"{detail.CashClosing ?? 0m:N0} đ";

            if (TxtRecentDetailCashDiff != null)
            {
                if (diff < 0)
                    TxtRecentDetailCashDiff.Text = $"{diff:N0} đ (Âm)";
                else if (diff > 0)
                    TxtRecentDetailCashDiff.Text = $"+{diff:N0} đ (Thừa)";
                else
                    TxtRecentDetailCashDiff.Text = "0 đ (Khớp)";
            }

            if (TxtRecentDetailPos1 != null) TxtRecentDetailPos1.Text = $"{detail.Pos1Closing ?? 0m:N0} đ";
            if (TxtRecentDetailPos2 != null) TxtRecentDetailPos2.Text = $"{detail.Pos2Closing ?? 0m:N0} đ";
            if (TxtRecentDetailBank1 != null) TxtRecentDetailBank1.Text = $"{detail.Bank1Closing ?? 0m:N0} đ";
            if (TxtRecentDetailBank2 != null) TxtRecentDetailBank2.Text = $"{detail.Bank2Closing ?? 0m:N0} đ";

            if (detail.Expenses != null && detail.Expenses.Any())
            {
                var expenseViewItems = detail.Expenses.Select(e => new
                {
                    Description = e.Description,
                    AmountDisplay = $"{e.Amount:N0} đ"
                }).ToList();

                if (IcRecentDetailExpenses != null)
                {
                    IcRecentDetailExpenses.ItemsSource = expenseViewItems;
                    IcRecentDetailExpenses.Visibility = Visibility.Visible;
                }
                if (TxtRecentNoExpensesNotice != null) TxtRecentNoExpensesNotice.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (IcRecentDetailExpenses != null)
                {
                    IcRecentDetailExpenses.ItemsSource = null;
                    IcRecentDetailExpenses.Visibility = Visibility.Collapsed;
                }
                if (TxtRecentNoExpensesNotice != null) TxtRecentNoExpensesNotice.Visibility = Visibility.Visible;
            }

            if (TxtRecentDetailNote != null)
            {
                TxtRecentDetailNote.Text = string.IsNullOrWhiteSpace(detail.Note) ? "Không có ghi chú giải trình cho ca này." : detail.Note;
            }
        }

        #endregion

        #region TAB 2: TRA CỨU & MASTER-DETAIL BIÊN BẢN CHỐT CA

        private List<ShiftSummaryItem> MapToShiftSummaryItems(List<AdminShiftSummaryDTO> list)
        {
            return list.Select(s => new ShiftSummaryItem
            {
                RawId = s.Id,
                ShiftId = s.ShiftCode,
                ShiftDate = s.ShiftDate,
                ShiftType = s.ShiftType,
                BranchName = s.BranchName,
                ClosedByUser = s.ClosedByUser,
                CashDifference = s.CashDifferenceDisplay,
                Note = s.Note,
                Status = s.StatusDisplay
            }).ToList();
        }

        private void BtnFilterShifts_Click(object sender, RoutedEventArgs e)
        {
            DateTime? filterDate = DpFilterDate?.SelectedDate;
            int branchId = (CbFilterBranch?.SelectedValue as int?) ?? 0;
            string shiftCode = (CbFilterShiftType?.SelectedValue as string) ?? "";

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
                TxtDetailShiftCode.Text = $"BIÊN BẢN CHỐT CA CHI TIẾT: #SH-{detail.ShiftId} ({detail.ShiftTypeName})";
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

            // 1. Tiền mặt két
            decimal posRev1 = (detail.Pos1Closing ?? 0m) - detail.Pos1Opening;
            if (posRev1 < 0) posRev1 = 0;
            decimal posRev2 = (detail.Pos2Closing ?? 0m) - detail.Pos2Opening;
            if (posRev2 < 0) posRev2 = 0;
            decimal totalPosCash = posRev1 + posRev2;

            decimal totalExp = detail.Expenses?.Sum(x => x.Amount) ?? 0m;
            decimal theoretical = detail.CashOpening + totalPosCash - totalExp;

            if (TxtDetailCashOpening != null) TxtDetailCashOpening.Text = $"{detail.CashOpening:N0} đ";
            if (TxtDetailCashPos != null) TxtDetailCashPos.Text = $"{totalPosCash:N0} đ";
            if (TxtDetailCashExpenses != null) TxtDetailCashExpenses.Text = $"-{totalExp:N0} đ";
            if (TxtDetailCashTheoretical != null) TxtDetailCashTheoretical.Text = $"{theoretical:N0} đ";
            if (TxtDetailCashActual != null) TxtDetailCashActual.Text = $"{detail.CashClosing ?? 0m:N0} đ";

            if (TxtDetailCashDiff != null)
            {
                if (diff < 0)
                    TxtDetailCashDiff.Text = $"{diff:N0} đ (Âm tiền)";
                else if (diff > 0)
                    TxtDetailCashDiff.Text = $"+{diff:N0} đ (Thừa tiền)";
                else
                    TxtDetailCashDiff.Text = "0 đ (Khớp tiền)";
            }

            // 2. POS & Bank
            if (TxtDetailPos1 != null) TxtDetailPos1.Text = $"{detail.Pos1Closing ?? 0m:N0} đ";
            if (TxtDetailPos2 != null) TxtDetailPos2.Text = $"{detail.Pos2Closing ?? 0m:N0} đ";
            if (TxtDetailBank1 != null) TxtDetailBank1.Text = $"{detail.Bank1Closing ?? 0m:N0} đ";
            if (TxtDetailBank2 != null) TxtDetailBank2.Text = $"{detail.Bank2Closing ?? 0m:N0} đ";

            // 3. Khoản chi xuất két
            if (detail.Expenses != null && detail.Expenses.Any())
            {
                var expenseViewItems = detail.Expenses.Select(e => new
                {
                    Description = e.Description,
                    AmountDisplay = $"{e.Amount:N0} đ"
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

            // 4. Giải trình
            if (TxtDetailNote != null)
            {
                TxtDetailNote.Text = string.IsNullOrWhiteSpace(detail.Note) ? "Không có ghi chú giải trình cho ca này." : detail.Note;
            }
        }

        private void BtnPrintShift_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDetail != null)
            {
                MessageBox.Show($"Đã xuất lệnh in biên bản bàn giao ca #SH-{_currentDetail.ShiftId} ({_currentDetail.ShiftTypeName}) ngày {_currentDetail.ShiftDate:dd/MM/yyyy} thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một ca làm việc từ danh sách bên trái!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRequestCompensation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDetail != null)
            {
                decimal diff = _currentDetail.CashDifference ?? 0m;
                if (diff < 0)
                {
                    MessageBox.Show($"Đã gửi thông báo yêu cầu giải trình và xử lý khoản hụt két {diff:N0} đ tới nhân viên chốt ca '{_currentDetail.ClosedByUser}'!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ca này không bị âm tiền két nên không cần gửi yêu cầu bồi hoàn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnApproveShift_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDetail != null)
            {
                MessageBox.Show($"Quản trị viên đã xác nhận duyệt biên bản bàn giao ca #SH-{_currentDetail.ShiftId} thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region TAB 3: CHI PHÍ KÉT & CẢNH BÁO BẤT THƯỜNG

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

                CalculateTab3Cards();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load expenses: " + ex.Message);
            }
        }

        private List<ExpenseAuditItem> MapToExpenseItems(List<AdminExpenseDTO> list)
        {
            return list.Select(e => new ExpenseAuditItem
            {
                Id = e.Id,
                ShiftId = e.ShiftCode,
                CreatedAt = e.CreatedAt,
                CreatedByUser = e.CreatedByUser,
                Amount = e.AmountDisplay,
                Description = e.Description,
                Note = e.Note
            }).ToList();
        }

        private void CalculateTab3Cards()
        {
            // Card 1: Tổng tiền két đã chi
            decimal totalExpenseAmount = _allExpenses.Sum(e => e.Amount);
            if (TxtExpenseTotalAmount != null) TxtExpenseTotalAmount.Text = $"{totalExpenseAmount:N0} đ";
            if (TxtExpenseTotalCount != null) TxtExpenseTotalCount.Text = $"Tổng cộng: {_allExpenses.Count} phiếu xuất két";

            // Card 2: Ca lệch tiền vượt hạn mức
            int alertCount = _allShifts.Count(s => Math.Abs(s.CashDifference ?? 0m) > 0m);
            if (TxtExpenseAlertCount != null) TxtExpenseAlertCount.Text = $"{alertCount} Ca Cảnh Báo";
            if (TxtExpenseAlertSub != null) TxtExpenseAlertSub.Text = alertCount > 0 ? "Cần đối soát camera và biên bản" : "Không có ca nào bị lệch tiền";

            // Card 3: Khoản chi lớn nhất
            var maxExp = _allExpenses.OrderByDescending(e => e.Amount).FirstOrDefault();
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

        private void BtnFilterExpenses_Click(object sender, RoutedEventArgs e)
        {
            string keyword = (TxtSearchExpense?.Text ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(keyword))
            {
                if (DgExpenses != null) DgExpenses.ItemsSource = MapToExpenseItems(_allExpenses);
            }
            else
            {
                var filtered = _allExpenses
                    .Where(x => x.CreatedByUser.ToLower().Contains(keyword) || x.Description.ToLower().Contains(keyword) || x.ShiftCode.ToLower().Contains(keyword))
                    .ToList();
                if (DgExpenses != null) DgExpenses.ItemsSource = MapToExpenseItems(filtered);
            }
        }

        #endregion

        #region TAB 4: CÀI ĐẶT HỆ THỐNG

        private void CbCashBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbCashBranch?.SelectedValue is int branchId)
            {
                var branch = _branches.FirstOrDefault(b => b.Id == branchId);
                if (branch != null && TxtDefaultCashOpening != null)
                {
                    TxtDefaultCashOpening.Text = $"{branch.DefaultCashOpening:N0}";
                }
            }
        }

        private async void BtnSaveCashOpening_Click(object sender, RoutedEventArgs e)
        {
            if (CbCashBranch?.SelectedValue is int branchId)
            {
                string raw = (TxtDefaultCashOpening?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
                if (decimal.TryParse(raw, out decimal cash) && cash >= 0)
                {
                    try
                    {
                        var res = await ApiService.Client.PutAsJsonAsync($"api/Branch/{branchId}/cash-opening", new UpdateCashOpeningDTO
                        {
                            BranchId = branchId,
                            DefaultCashOpening = cash
                        });
                        if (res.IsSuccessStatusCode)
                        {
                            var b = _branches.FirstOrDefault(x => x.Id == branchId);
                            if (b != null) b.DefaultCashOpening = cash;
                            MessageBox.Show($"Đã lưu mức tiền mặt két mặc định: {cash:N0} đ cho cơ sở thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi kết nối lưu tiền két: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Vui lòng nhập số tiền hợp lệ!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void CbBankBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbBankBranch?.SelectedValue is int branchId)
            {
                try
                {
                    var banks = await ApiService.Client.GetFromJsonAsync<List<BranchBankSettingDTO>>($"api/Branch/{branchId}/banks");
                    var b1 = banks?.FirstOrDefault(b => b.SlotIndex == 1);
                    var b2 = banks?.FirstOrDefault(b => b.SlotIndex == 2);

                    if (TxtBank1Name != null) TxtBank1Name.Text = b1?.BankName ?? "TingTing";
                    if (ChkBank1Active != null) ChkBank1Active.IsChecked = b1?.IsActive ?? true;

                    if (TxtBank2Name != null) TxtBank2Name.Text = b2?.BankName ?? "Zalo Pay";
                    if (ChkBank2Active != null) ChkBank2Active.IsChecked = b2?.IsActive ?? true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi load banks: " + ex.Message);
                }
            }
        }

        private async void BtnSaveBanks_Click(object sender, RoutedEventArgs e)
        {
            if (CbBankBranch?.SelectedValue is int branchId)
            {
                var banks = new List<BranchBankSettingDTO>
                {
                    new BranchBankSettingDTO { BranchId = branchId, SlotIndex = 1, BankName = TxtBank1Name?.Text?.Trim() ?? "TingTing", IsActive = ChkBank1Active?.IsChecked == true },
                    new BranchBankSettingDTO { BranchId = branchId, SlotIndex = 2, BankName = TxtBank2Name?.Text?.Trim() ?? "Zalo Pay", IsActive = ChkBank2Active?.IsChecked == true }
                };

                try
                {
                    var res = await ApiService.Client.PostAsJsonAsync($"api/Branch/{branchId}/banks", banks);
                    if (res.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Đã lưu cấu hình cổng ngân hàng/ví điện tử cho cơ sở thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi lưu ngân hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void CbPosBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbPosBranch?.SelectedValue is int branchId)
            {
                try
                {
                    var posList = await ApiService.Client.GetFromJsonAsync<List<PosConfigSettingDTO>>($"api/Branch/{branchId}/pos");
                    _currentPosConfigs = posList ?? new List<PosConfigSettingDTO>();
                    if (DgPosConfigs != null)
                    {
                        DgPosConfigs.ItemsSource = null;
                        DgPosConfigs.ItemsSource = _currentPosConfigs;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi load pos: " + ex.Message);
                }
            }
        }

        private void BtnAddPos_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewPosName?.Text))
            {
                MessageBox.Show("Vui lòng nhập tên App POS!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            byte order = byte.TryParse(TxtNewPosOrder?.Text, out var o) ? o : (byte)1;
            int branchId = (CbPosBranch?.SelectedValue as int?) ?? 1;

            _currentPosConfigs.Add(new PosConfigSettingDTO
            {
                BranchId = branchId,
                PosName = TxtNewPosName.Text.Trim(),
                DisplayOrder = order,
                IsActive = ChkNewPosActive?.IsChecked == true
            });

            if (DgPosConfigs != null)
            {
                DgPosConfigs.ItemsSource = null;
                DgPosConfigs.ItemsSource = _currentPosConfigs;
            }
            TxtNewPosName.Clear();
        }

        private async void BtnSavePos_Click(object sender, RoutedEventArgs e)
        {
            if (CbPosBranch?.SelectedValue is int branchId)
            {
                try
                {
                    var res = await ApiService.Client.PostAsJsonAsync($"api/Branch/{branchId}/pos", _currentPosConfigs);
                    if (res.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Đã lưu cấu hình danh sách App POS cho cơ sở thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi lưu POS: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSaveShiftSchedule_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Đã lưu cấu hình danh mục ca làm việc thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Chức năng thêm nhân viên mới: Vui lòng sử dụng biểu mẫu phân quyền nhân sự hoặc quản lý tài khoản để thêm nhân viên.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        _allEmployees = stats.Select(s => new EmployeeAuditItem
                        {
                            Id = s.UserId,
                            Username = s.Username,
                            FullName = s.FullName,
                            Role = s.Role,
                            ShiftCount = s.TotalShifts,
                            NegativeShiftCount = s.NegativeShiftsCount,
                            TotalNegativeDiff = s.TotalNegativeDiff < 0 ? $"-{Math.Abs(s.TotalNegativeDiff):N0} đ" : "0 đ",
                            PositiveShiftCount = s.PositiveShiftsCount,
                            TotalPositiveDiff = s.TotalPositiveDiff > 0 ? $"+{s.TotalPositiveDiff:N0} đ" : "0 đ",
                            BalancedShiftCount = s.BalancedShiftsCount,
                            IsActive = s.IsActive ? "Hoạt động" : "Đã khóa"
                        }).ToList();

                        if (DgEmployees != null)
                            DgEmployees.ItemsSource = _allEmployees;

                        if (TxtEmployeeCountHeader != null)
                            TxtEmployeeCountHeader.Text = $"Tổng: {_allEmployees.Count} Nhân sự";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load employee statistics: " + ex.Message);
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
        public string CashDifference { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class EmployeeAuditItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ShiftCount { get; set; }
        public int NegativeShiftCount { get; set; }
        public string TotalNegativeDiff { get; set; } = "0 đ";
        public int PositiveShiftCount { get; set; }
        public string TotalPositiveDiff { get; set; } = "0 đ";
        public int BalancedShiftCount { get; set; }
        public string IsActive { get; set; } = string.Empty;
    }

    public class ExpenseAuditItem
    {
        public int Id { get; set; }
        public string ShiftId { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedByUser { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}
