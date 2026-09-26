using System;
using System.Collections.Generic;

namespace ShiftHandOver.Server.Models;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Role { get; set; } = null!;

    public int ShiftCount { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Shift> ShiftClosedByUsers { get; set; } = new List<Shift>();

    public virtual ICollection<ShiftEmployee> ShiftEmployees { get; set; } = new List<ShiftEmployee>();

    public virtual ICollection<ShiftExpense> ShiftExpenses { get; set; } = new List<ShiftExpense>();

    public virtual ICollection<Shift> ShiftOpenedByUsers { get; set; } = new List<Shift>();
}
