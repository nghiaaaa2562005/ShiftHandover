using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class Branch
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal DefaultCashOpening { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<BranchBank> BranchBanks { get; set; } = new List<BranchBank>();

    public virtual ICollection<PosConfig> PosConfigs { get; set; } = new List<PosConfig>();

    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
