using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class CartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }

    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string SpecialInstructions { get; set; }

    public DateTime AddedDate { get; set; }

    public virtual Cart Cart { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
