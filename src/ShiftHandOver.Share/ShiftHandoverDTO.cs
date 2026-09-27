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

        // Sapo POS
        public decimal Pos1Opening { get; set; }
        public decimal? Pos1Closing { get; set; }
        public decimal? Pos1Night { get; set; }

        // KiotViet
        public decimal Pos2Opening { get; set; }
        public decimal? Pos2Closing { get; set; }
        public decimal? Pos2Night { get; set; }

        // TingTing
        public decimal Bank1Opening { get; set; }
        public decimal? Bank1Closing { get; set; }
        public decimal? Bank1Night { get; set; }

        // Zalo Pay
        public decimal Bank2Opening { get; set; }
        public decimal? Bank2Closing { get; set; }
        public decimal? Bank2Night { get; set; }

        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new List<ShiftExpenseItemDTO>();
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

        public decimal Bank1Closing { get; set; }
        public decimal Bank1Night { get; set; }

        public decimal Bank2Closing { get; set; }
        public decimal Bank2Night { get; set; }

        public string? Note { get; set; }
        public List<ShiftExpenseItemDTO> Expenses { get; set; } = new List<ShiftExpenseItemDTO>();
    }

    public class ChangeInitialDataRequestDTO
    {
        public int ShiftId { get; set; }
        public int UserId { get; set; }
        public decimal CashOpening { get; set; }
        public decimal Pos1Opening { get; set; }
        public decimal Pos2Opening { get; set; }
        public decimal Bank1Opening { get; set; }
        public decimal Bank2Opening { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}

