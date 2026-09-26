using System;
using System.Collections.Generic;
using System.Windows;

namespace ShiftHandOver.Client.Admin
{
    /// <summary>
    /// Interaction logic for AdminDashBoard.xaml
    /// </summary>
    public partial class AdminDashBoard : Window
    {
        public AdminDashBoard()
        {
            InitializeComponent();
            LoadMockData();
        }

        private void LoadMockData()
        {
            // Tab 1: Các ca bị lệch tiền gần đây
            DgRecentShiftDiff.ItemsSource = new List<ShiftSummaryItem>
            {
                new ShiftSummaryItem { ShiftId = "SH-1024", ShiftDate = "25/09/2026", ShiftType = "Ca Đêm", BranchName = "Cơ sở 1 - Cầu Giấy", ClosedByUser = "Trần Thị B", CashDifference = "-35.000 đ", Note = "Khách mua thanh toán nhầm tiền thừa" },
                new ShiftSummaryItem { ShiftId = "SH-1021", ShiftDate = "24/09/2026", ShiftType = "Ca Đêm", BranchName = "Cơ sở 2 - Đống Đa", ClosedByUser = "Lê Hoàng D", CashDifference = "-150.000 đ", Note = "Thiếu tiền két chưa rõ lý do, xin check cam" },
                new ShiftSummaryItem { ShiftId = "SH-1018", ShiftDate = "24/09/2026", ShiftType = "Ca Chiều", BranchName = "Cơ sở 1 - Cầu Giấy", ClosedByUser = "Nguyễn Văn A", CashDifference = "+50.000 đ", Note = "Khách boa thừa tiền thối" },
                new ShiftSummaryItem { ShiftId = "SH-1012", ShiftDate = "23/09/2026", ShiftType = "Ca Đêm", BranchName = "Cơ sở 1 - Cầu Giấy", ClosedByUser = "Lê Hoàng D", CashDifference = "-200.000 đ", Note = "Bị hụt tiền quầy khi giao ca sáng" }
            };

            // Tab 2: Danh sách ca để tra cứu biên bản
            DgShiftList.ItemsSource = new List<ShiftSummaryItem>
            {
                new ShiftSummaryItem { ShiftId = "SH-1024", ShiftDate = "25/09/2026", ShiftType = "Ca Sáng", BranchName = "Cơ sở 1", ClosedByUser = "Trần Thị B", CashDifference = "-35.000 đ", Status = "Đã chốt" },
                new ShiftSummaryItem { ShiftId = "SH-1023", ShiftDate = "24/09/2026", ShiftType = "Ca Đêm", BranchName = "Cơ sở 1", ClosedByUser = "Nguyễn Văn A", CashDifference = "0 đ", Status = "Đã chốt" },
                new ShiftSummaryItem { ShiftId = "SH-1022", ShiftDate = "24/09/2026", ShiftType = "Ca Chiều", BranchName = "Cơ sở 2", ClosedByUser = "Lê Văn C", CashDifference = "+20.000 đ", Status = "Đã chốt" },
                new ShiftSummaryItem { ShiftId = "SH-1021", ShiftDate = "24/09/2026", ShiftType = "Ca Sáng", BranchName = "Cơ sở 2", ClosedByUser = "Lê Hoàng D", CashDifference = "-150.000 đ", Status = "Đã chốt" },
                new ShiftSummaryItem { ShiftId = "SH-1020", ShiftDate = "23/09/2026", ShiftType = "Ca Đêm", BranchName = "Cơ sở 1", ClosedByUser = "Phạm Thị E", CashDifference = "0 đ", Status = "Đã chốt" }
            };

            // Tab 3: Danh sách nhân viên & giám sát lệch tiền
            DgEmployees.ItemsSource = new List<EmployeeAuditItem>
            {
                new EmployeeAuditItem { Id = 1, Username = "admin", FullName = "Quản Trị Viên", Role = "Admin", ShiftCount = 10, NegativeShiftCount = 0, TotalCashDiff = "0 đ", IsActive = "Hoạt động" },
                new EmployeeAuditItem { Id = 2, Username = "vana_nv", FullName = "Nguyễn Văn A", Role = "Employee", ShiftCount = 42, NegativeShiftCount = 2, TotalCashDiff = "+15.000 đ", IsActive = "Hoạt động" },
                new EmployeeAuditItem { Id = 3, Username = "thib_nv", FullName = "Trần Thị B", Role = "Employee", ShiftCount = 38, NegativeShiftCount = 3, TotalCashDiff = "-65.000 đ", IsActive = "Hoạt động" },
                new EmployeeAuditItem { Id = 4, Username = "hoangd_nv", FullName = "Lê Hoàng D", Role = "Employee", ShiftCount = 25, NegativeShiftCount = 7, TotalCashDiff = "-580.000 đ", IsActive = "Hoạt động" },
                new EmployeeAuditItem { Id = 5, Username = "vanc_nv", FullName = "Lê Văn C", Role = "Employee", ShiftCount = 31, NegativeShiftCount = 1, TotalCashDiff = "+20.000 đ", IsActive = "Hoạt động" }
            };

            // Tab 4: Soát xét chi phí két
            DgExpenses.ItemsSource = new List<ExpenseAuditItem>
            {
                new ExpenseAuditItem { Id = 101, ShiftId = "SH-1024", CreatedAt = "25/09/2026 10:15", CreatedByUser = "Trần Thị B", Amount = "300.000 đ", Description = "Trả tiền đá lạnh và túi xốp", Note = "Có hóa đơn giấy kẹp két" },
                new ExpenseAuditItem { Id = 102, ShiftId = "SH-1021", CreatedAt = "24/09/2026 23:40", CreatedByUser = "Lê Hoàng D", Amount = "500.000 đ", Description = "Ứng tiền mua cây lau nhà và nước lau sàn", Note = "Chưa nộp hóa đơn đỏ" },
                new ExpenseAuditItem { Id = 103, ShiftId = "SH-1015", CreatedAt = "23/09/2026 15:30", CreatedByUser = "Nguyễn Văn A", Amount = "1.200.000 đ", Description = "Trả tiền bia nước ngọt nhà phân phối", Note = "Kèm phiếu xuất kho NCC" }
            };
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var login = new Login();
            login.Show();
            this.Close();
        }
    }

    public class ShiftSummaryItem
    {
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
        public string TotalCashDiff { get; set; } = string.Empty;
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
