using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class PosConfig
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    public string PosName { get; set; } = null!;

    public byte DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<ShiftPosEntry> ShiftPosEntries { get; set; } = new List<ShiftPosEntry>();
}
