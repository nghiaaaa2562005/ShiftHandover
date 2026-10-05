using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class Shift
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public string ShiftType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal CashOpening { get; set; }

    public decimal? CashClosing { get; set; }

    public decimal? CashDifference { get; set; }

    public bool IsNewDayFirstShift { get; set; }

    public int? OpenedByUserId { get; set; }

    public int? ClosedByUserId { get; set; }

    public DateTime? OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Note { get; set; }
    public string? ActiveChannels { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User? ClosedByUser { get; set; }

    public virtual User? OpenedByUser { get; set; }

    public virtual ICollection<ShiftBankEntry> ShiftBankEntries { get; set; } = new List<ShiftBankEntry>();

    public virtual ICollection<ShiftEmployee> ShiftEmployees { get; set; } = new List<ShiftEmployee>();

    public virtual ICollection<ShiftExpense> ShiftExpenses { get; set; } = new List<ShiftExpense>();

    public virtual ICollection<ShiftPosEntry> ShiftPosEntries { get; set; } = new List<ShiftPosEntry>();
}
