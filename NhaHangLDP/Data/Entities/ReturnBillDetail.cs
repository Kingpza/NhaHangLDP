using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class ReturnBillDetail
{
    public int ReturnBillDetailId { get; set; }

    public int ReturnBillId { get; set; }

    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsDamaged { get; set; }

    public virtual MenuItem MenuItem { get; set; }

    public virtual ReturnBill ReturnBill { get; set; }
}
