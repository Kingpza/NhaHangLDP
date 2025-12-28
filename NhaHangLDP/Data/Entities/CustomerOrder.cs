using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class CustomerOrder
{
    public int Id { get; set; }

    public string OrderCode { get; set; }

    public int? CustomerId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public string CustomerEmail { get; set; }

    public string OrderType { get; set; }

    public string DeliveryAddress { get; set; }

    public string Ward { get; set; }

    public string District { get; set; }

    public string City { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal Discount { get; set; }

    public string VoucherCode { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentMethod { get; set; }

    public string PaymentStatus { get; set; }

    public DateTime? PaidDate { get; set; }

    public string TransactionId { get; set; }

    public string Status { get; set; }

    public string Note { get; set; }

    public string CancelReason { get; set; }

    public DateTime OrderDate { get; set; }

    public DateTime? ConfirmedDate { get; set; }

    public DateTime? PreparingDate { get; set; }

    public DateTime? ReadyDate { get; set; }

    public DateTime? DeliveringDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? CancelledDate { get; set; }

    public DateTime? EstimatedDeliveryTime { get; set; }

    public int? Rating { get; set; }

    public string ReviewComment { get; set; }

    public int EarnedPoints { get; set; }

    public int UsedPoints { get; set; }

    public virtual Customer Customer { get; set; }

    public virtual ICollection<CustomerOrderDetail> CustomerOrderDetails { get; set; } = new List<CustomerOrderDetail>();

    public virtual ICollection<DeliveryAssignment> DeliveryAssignments { get; set; } = new List<DeliveryAssignment>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
}
