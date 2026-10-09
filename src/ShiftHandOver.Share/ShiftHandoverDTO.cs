using System;
using System.Collections.Generic;

namespace ShiftHandOver.Share
{
    public class ShiftExpenseItemDTO
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class InitShiftRequestDTO
    {
        public int BranchId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = "MORNING";
        public int UserId { get; set; }
    }

    public class ShiftHandoverDetailDTO
    {
        public int ShiftId { get; set; }
        public string ShiftCode { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = "MORNING";
        public string ShiftTypeName { get; set; } = "Ca Sáng";
        public string Status { get; set; } = "NConfirm";
        public bool IsReadOnly { get; set; }

        public decimal CashOpening { get; set; }
        public decimal? CashClosing { get; set; }
        public decimal? CashDifference { get; set; }
        public string? Note { get; set; }

        // Tên và trạng thái cấu hình App POS & Ngân hàng
        public string Pos1Name { get; set; } = "Sapo POS";
        public string? Pos1ImageUrl { get; set; }
        public bool Pos1IsActive { get; set; } = true;
        public decimal Pos1Opening { get; set; }
        public decimal? Pos1Closing { get; set; }
        public decimal? Pos1Night { get; set; }

        public string Pos2Name { get; set; } = "KiotViet";
        public string? Pos2ImageUrl { get; set; }
        public bool Pos2IsActive { get; set; } = true;
        public decimal Pos2Opening { get; set; }
        public decimal? Pos2Closing { get; set; }
        public decimal? Pos2Night { get; set; }

        public string Pos3Name { get; set; } = "";
        public string? Pos3ImageUrl { get; set; }
        public bool Pos3IsActive { get; set; } = false;
        public decimal Pos3Opening { get; set; }
        public decimal? Pos3Closing { get; set; }
        public decimal? Pos3Night { get; set; }

        public string Bank1Name { get; set; } = "TingTing";
        public string? Bank1ImageUrl { get; set; }
        public bool Bank1IsActive { get; set; } = true;
        public decimal Bank1Opening { get; set; }
        public decimal? Bank1Closing { get; set; }
        public decimal? Bank1Night { get; set; }

        public string Bank2Name { get; set; } = "Zalo Pay";
        public string? Bank2ImageUrl { get; set; }
        public bool Bank2IsActive { get; set; } = true;
        public decimal Bank2Opening { get; set; }
        public decimal? Bank2Closing { get; set; }
        public decimal? Bank2Night { get; set; }

        public string Bank3Name { get; set; } = "Ngân hàng 3";
        public string? Bank3ImageUrl { get; set; }
        public bool Bank3IsActive { get; set; } = false;
        public decimal Bank3Opening { get; set; }
        public decimal? Bank3Closing { get; set; }
        public decimal? Bank3Night { get; set; }

        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new List<ShiftExpenseItemDTO>();
        public List<string> EmployeeNames { get; set; } = new List<string>();
        public string OpenedByUser { get; set; } = string.Empty;
        public string ClosedByUser { get; set; } = string.Empty;
    }

    public class ShiftEmployeeSignDTO
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ConfirmStartRequestDTO
    {
        public int ShiftId { get; set; }
        public int UserId { get; set; }
    }

    public class CloseShiftRequestDTO
    {
        public int ShiftId { get; set; }
        public int ClosedByUserId { get; set; }
        public decimal CashClosing { get; set; }
        public decimal CashDifference { get; set; }

        public decimal Pos1Closing { get; set; }
        public decimal Pos1Night { get; set; }

        public decimal Pos2Closing { get; set; }
        public decimal Pos2Night { get; set; }

        public decimal Pos3Closing { get; set; }
        public decimal Pos3Night { get; set; }

        public decimal Bank1Closing { get; set; }
        public decimal Bank1Night { get; set; }

        public decimal Bank2Closing { get; set; }
        public decimal Bank2Night { get; set; }

        public decimal Bank3Closing { get; set; }
        public decimal Bank3Night { get; set; }

        public string? Note { get; set; }
        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new List<ShiftExpenseItemDTO>();

        // Danh sách chữ ký xác nhận của nhân viên tham gia ca (tài khoản & mật khẩu)
        public List<ShiftEmployeeSignDTO> Signatures { get; set; } = new List<ShiftEmployeeSignDTO>();
    }

    public class CloseShiftResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ConfirmedEmployeeNames { get; set; } = new List<string>();
    }

    public class EmployeeShiftStatisticsDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int TotalShifts { get; set; }           // Tổng số ca đã làm
        public int NegativeShiftsCount { get; set; }   // Số ca bị âm tiền
        public decimal TotalNegativeDiff { get; set; } // Tổng tiền âm (không triệt tiêu)
        public int PositiveShiftsCount { get; set; }   // Số ca bị dương tiền
        public decimal TotalPositiveDiff { get; set; } // Tổng tiền dương (không triệt tiêu)
        public int BalancedShiftsCount { get; set; }   // Số ca khớp chuẩn (0 đ)
        public bool IsActive { get; set; }
    }

    public class ChangeInitialDataRequestDTO
    {
        public int ShiftId { get; set; }
        public int UserId { get; set; }
        public decimal CashOpening { get; set; }
        public decimal Pos1Opening { get; set; }
        public decimal Pos2Opening { get; set; }
        public decimal Pos3Opening { get; set; }
        public decimal Bank1Opening { get; set; }
        public decimal Bank2Opening { get; set; }
        public decimal Bank3Opening { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class VerifyShiftOwnerRequestDTO
    {
        public int ShiftId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class VerifyShiftOwnerResponseDTO
    {
        public bool IsAuthorized { get; set; }
        public string Message { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateClosedShiftRequestDTO
    {
        public int ShiftId { get; set; }
        public int EditorUserId { get; set; }
        public string EditorFullName { get; set; } = string.Empty;

        public decimal CashOpening { get; set; }
        public decimal CashClosing { get; set; }
        public decimal CashDifference { get; set; }

        public decimal Pos1Opening { get; set; }
        public decimal Pos1Closing { get; set; }
        public decimal Pos1Night { get; set; }

        public decimal Pos2Opening { get; set; }
        public decimal Pos2Closing { get; set; }
        public decimal Pos2Night { get; set; }

        public decimal Pos3Opening { get; set; }
        public decimal Pos3Closing { get; set; }
        public decimal Pos3Night { get; set; }

        public decimal Bank1Opening { get; set; }
        public decimal Bank1Closing { get; set; }
        public decimal Bank1Night { get; set; }

        public decimal Bank2Opening { get; set; }
        public decimal Bank2Closing { get; set; }
        public decimal Bank2Night { get; set; }

        public decimal Bank3Opening { get; set; }
        public decimal Bank3Closing { get; set; }
        public decimal Bank3Night { get; set; }

        public string ChangeLog { get; set; } = string.Empty;
        public string UserNote { get; set; } = string.Empty;

        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new List<ShiftExpenseItemDTO>();
    }

    public class AdminShiftSummaryDTO
    {
        public int Id { get; set; }
        public string ShiftCode { get; set; } = string.Empty;
        public string ShiftDate { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string ClosedByUser { get; set; } = string.Empty;
        public string OpenedByUser { get; set; } = string.Empty;
        public string EmployeeNames { get; set; } = string.Empty;
        public decimal? CashDifference { get; set; }
        public string CashDifferenceDisplay { get; set; } = "0 đ";
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public int? ClosedByUserId { get; set; }
        public List<int> EmployeeUserIds { get; set; } = new List<int>();
        public decimal CashDiffClosing { get; set; }
        public decimal BankRevenue { get; set; }
    }

    public class AdminExpenseDTO
    {
        public int Id { get; set; }
        public string ShiftCode { get; set; } = string.Empty;
        public int ShiftId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedByUser { get; set; } = string.Empty;
        public string EmployeeNames { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountDisplay { get; set; } = "0 đ";
        public string Description { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class ShiftChannelSelection
    {
        public bool Pos1Active { get; set; } = true;
        public bool Pos2Active { get; set; } = true;
        public bool Pos3Active { get; set; } = true;

        public bool Bank1Active { get; set; } = true;
        public bool Bank2Active { get; set; } = true;
        public bool Bank3Active { get; set; } = true;
    }

    public class ResetShiftItemDTO
    {
        public int Id { get; set; }
        public string ShiftCode { get; set; } = string.Empty;
        public string ShiftDateDisplay { get; set; } = string.Empty;
        public string ShiftTypeDisplay { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string PersonInCharge { get; set; } = string.Empty;
        public decimal NegativeAmount { get; set; }
        public string NegativeAmountDisplay { get; set; } = "0 đ";
        public decimal PositiveAmount { get; set; }
        public string PositiveAmountDisplay { get; set; } = "0 đ";
        public string StatusDisplay { get; set; } = string.Empty;
    }

    public class ResetShiftsRequestDTO
    {
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int? BranchId { get; set; }
        public List<int>? ShiftIds { get; set; }
    }

    public class ResetShiftsResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DeletedCount { get; set; }
    }
}

