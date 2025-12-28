using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class ShiftSupportStaff
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int CashierShiftId { get; set; }

    public virtual CashierShift CashierShift { get; set; }

    public virtual Employee Employee { get; set; }
}
