using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class OrderDetail
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    public decimal PriceAtTime { get; set; }

    public string Notes { get; set; }

    public virtual MenuItem MenuItem { get; set; }

    public virtual Order Order { get; set; }
}
