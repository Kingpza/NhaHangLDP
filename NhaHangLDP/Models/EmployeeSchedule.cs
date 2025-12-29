using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class EmployeeSchedule
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int WorkShiftId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string Status { get; set; }

    public string Note { get; set; }

    public virtual Employee Employee { get; set; }

    public virtual WorkShift WorkShift { get; set; }
}
