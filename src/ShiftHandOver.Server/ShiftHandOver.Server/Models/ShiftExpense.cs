using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class ShiftExpense
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;
}
