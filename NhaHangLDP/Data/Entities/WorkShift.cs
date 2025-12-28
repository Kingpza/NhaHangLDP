using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class WorkShift
{
    public int Id { get; set; }

    public string ShiftName { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public decimal? WorkHours { get; set; }

    public bool IsActive { get; set; }

    public string Description { get; set; }

    public virtual ICollection<EmployeeSchedule> EmployeeSchedules { get; set; } = new List<EmployeeSchedule>();
}
