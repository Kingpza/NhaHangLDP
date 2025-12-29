using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class Payroll
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public decimal BaseSalary { get; set; }

    public decimal Allowance { get; set; }

    public decimal OvertimeBonus { get; set; }

    public decimal PerformanceBonus { get; set; }

    public decimal Deduction { get; set; }

    public decimal TotalWorkDays { get; set; }

    public decimal TotalWorkHours { get; set; }

    public int LateCount { get; set; }

    public int AbsentCount { get; set; }

    public decimal NetSalary { get; set; }

    public string Status { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual Employee Employee { get; set; }
}
