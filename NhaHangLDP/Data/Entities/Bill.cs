using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Bill
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int CashierId { get; set; }

    public DateTime BillDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string PaymentMethod { get; set; }

    public string Status { get; set; }

    public virtual Employee Cashier { get; set; }

    public virtual Order Order { get; set; }

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual ICollection<ReturnBill> ReturnBills { get; set; } = new List<ReturnBill>();
}
