using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class QROrderDetail
{
    public int Id { get; set; }

    public int QROrderId { get; set; }

    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string ItemNotes { get; set; }

    public string ItemStatus { get; set; }

    public DateTime AddedTime { get; set; }

    public virtual MenuItem MenuItem { get; set; }

    public virtual QROrder QROrder { get; set; }
}
