using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class CashierShift
{
    public int Id { get; set; }

    public int CashierId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public decimal InitialCash { get; set; }

    public decimal? FinalCash { get; set; }

    public decimal? TotalRevenue { get; set; }

    public string Status { get; set; }

    public virtual Employee Cashier { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ShiftSupportStaff> ShiftSupportStaffs { get; set; } = new List<ShiftSupportStaff>();
}
