using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Supplier
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string ContactPerson { get; set; }

    public string PhoneNumber { get; set; }

    public string Address { get; set; }

    public virtual ICollection<StockInbound> StockInbounds { get; set; } = new List<StockInbound>();
}
