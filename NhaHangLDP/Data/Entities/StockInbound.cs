using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class StockInbound
{
    public int Id { get; set; }

    public int? SupplierId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime InboundDate { get; set; }

    public decimal TotalCost { get; set; }

    public string Notes { get; set; }

    public string InboundCode { get; set; }

    public string CreatedBy { get; set; }

    public string Status { get; set; }

    public virtual Employee Employee { get; set; }

    public virtual ICollection<StockInboundDetail> StockInboundDetails { get; set; } = new List<StockInboundDetail>();

    public virtual Supplier Supplier { get; set; }
}
