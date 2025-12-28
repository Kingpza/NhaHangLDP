using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class EmployeeContract
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public string ContractType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal BaseSalary { get; set; }

    public decimal? Allowance { get; set; }

    public string Status { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual Employee Employee { get; set; }
}
