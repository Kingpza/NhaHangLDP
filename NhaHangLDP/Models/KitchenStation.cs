using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class KitchenStation
{
    public int Id { get; set; }

    public string Code { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string HandledCategories { get; set; }

    public string DisplayColor { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}
