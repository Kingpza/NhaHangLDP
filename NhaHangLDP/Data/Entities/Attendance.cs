using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Attendance
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public DateTime CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public decimal? WorkHours { get; set; }

    public string Status { get; set; }

    public string Note { get; set; }

    public string Location { get; set; }

    public virtual Employee Employee { get; set; }
}
