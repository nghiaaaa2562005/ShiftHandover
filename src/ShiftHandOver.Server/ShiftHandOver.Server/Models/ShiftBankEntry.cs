using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class ShiftBankEntry
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public int BranchBankId { get; set; }

    public decimal BankOpening { get; set; }

    public decimal? BankClosing { get; set; }

    public decimal? BankClosingDay1 { get; set; }

    public decimal? BankClosingDay2 { get; set; }

    public virtual BranchBank BranchBank { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;
}
