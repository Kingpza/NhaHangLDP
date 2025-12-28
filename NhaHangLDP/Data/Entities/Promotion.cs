using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Promotion
{
    public int Id { get; set; }

    public string Code { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal MinOrderValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int? MaxUsageCount { get; set; }

    public int UsedCount { get; set; }

    public int? MaxUsagePerCustomer { get; set; }

    public string ApplicableTo { get; set; }

    public string ApplicableIds { get; set; }

    public bool IsNewCustomerOnly { get; set; }

    public TimeOnly? HappyHourStart { get; set; }

    public TimeOnly? HappyHourEnd { get; set; }

    public string ApplicableDays { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();
}
