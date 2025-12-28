using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class PriceHistory
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }

    public decimal OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public string ChangeReason { get; set; }

    public DateTime ChangeDate { get; set; }

    public int? ChangedByEmployeeId { get; set; }

    public virtual Employee ChangedByEmployee { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
