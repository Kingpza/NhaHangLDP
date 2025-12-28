using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Qrorder
{
    public int Id { get; set; }

    public string QrorderCode { get; set; }

    public string SessionToken { get; set; }

    public int TableId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public string Status { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? SubmittedTime { get; set; }

    public DateTime? ConfirmedTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public int? LinkedOrderId { get; set; }

    public int? ConfirmedByEmployeeId { get; set; }

    public string AppliedPromotionCode { get; set; }

    public virtual Employee ConfirmedByEmployee { get; set; }

    public virtual Order LinkedOrder { get; set; }

    public virtual ICollection<QrorderDetail> QrorderDetails { get; set; } = new List<QrorderDetail>();

    public virtual RestaurantTable Table { get; set; }
}
