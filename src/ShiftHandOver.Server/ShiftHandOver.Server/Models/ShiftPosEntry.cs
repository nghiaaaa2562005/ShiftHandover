using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class ShiftPosEntry
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public int PosConfigId { get; set; }

    public decimal PosOpening { get; set; }

    public decimal? PosClosing { get; set; }

    public decimal? PosClosingDay1 { get; set; }

    public decimal? PosClosingDay2 { get; set; }

    public virtual PosConfig PosConfig { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;
}
