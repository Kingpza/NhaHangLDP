using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class PromotionUsage
{
    public int Id { get; set; }

    public int PromotionId { get; set; }

    public int? BillId { get; set; }

    public string CustomerPhone { get; set; }

    public decimal DiscountApplied { get; set; }

    public DateTime UsedDate { get; set; }

    public virtual Bill Bill { get; set; }

    public virtual Promotion Promotion { get; set; }
}
