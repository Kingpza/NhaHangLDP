using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Wishlist
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int MenuItemId { get; set; }

    public DateTime AddedDate { get; set; }

    public virtual Customer Customer { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
