using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class MenuComboItem
{
    public int Id { get; set; }

    public int MenuComboId { get; set; }

    public int MenuItemId { get; set; }

    public int Quantity { get; set; }

    public virtual MenuCombo MenuCombo { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
