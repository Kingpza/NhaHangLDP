using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class CustomerOrderDetail
{
    public int Id { get; set; }

    public int CustomerOrderId { get; set; }

    public int MenuItemId { get; set; }

    public string ItemName { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }

    public string SpecialInstructions { get; set; }

    public virtual CustomerOrder CustomerOrder { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
