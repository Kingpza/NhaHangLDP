using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public string LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Reason { get; set; }

    public string Status { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual Employee ApprovedByNavigation { get; set; }

    public virtual Employee Employee { get; set; }
}
