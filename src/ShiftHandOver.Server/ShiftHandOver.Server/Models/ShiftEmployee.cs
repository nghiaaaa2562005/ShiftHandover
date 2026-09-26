using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class ShiftEmployee
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public int UserId { get; set; }

    public virtual Shift Shift { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
