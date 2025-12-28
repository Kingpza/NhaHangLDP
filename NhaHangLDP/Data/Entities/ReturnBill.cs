using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class ReturnBill
{
    public int ReturnBillId { get; set; }

    public int OriginalBillId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime ReturnDate { get; set; }

    public decimal TotalRefundAmount { get; set; }

    public string Reason { get; set; }

    public virtual Employee Employee { get; set; }

    public virtual Bill OriginalBill { get; set; }

    public virtual ICollection<ReturnBillDetail> ReturnBillDetails { get; set; } = new List<ReturnBillDetail>();
}
