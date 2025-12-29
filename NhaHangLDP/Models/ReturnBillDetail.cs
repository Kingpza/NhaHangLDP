using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class ReturnBillDetail
{
    public int ReturnBillDetailID { get; set; }

    public int ReturnBillID { get; set; }

    public int MenuItemID { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsDamaged { get; set; }

    public virtual MenuItem MenuItem { get; set; }

    public virtual ReturnBill ReturnBill { get; set; }
}
