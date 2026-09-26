using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class BranchBank
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public byte SlotIndex { get; set; }

    public string BankName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<ShiftBankEntry> ShiftBankEntries { get; set; } = new List<ShiftBankEntry>();
}
