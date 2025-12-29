using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class ReturnBill
{
    public int ReturnBillID { get; set; }

    public int OriginalBillID { get; set; }

    public int EmployeeID { get; set; }

    public DateTime ReturnDate { get; set; }

    public decimal TotalRefundAmount { get; set; }

    public string Reason { get; set; }

    public virtual Employee Employee { get; set; }

    public virtual Bill OriginalBill { get; set; }

    public virtual ICollection<ReturnBillDetail> ReturnBillDetails { get; set; } = new List<ReturnBillDetail>();
}
