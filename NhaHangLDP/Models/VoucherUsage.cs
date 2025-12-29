using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class VoucherUsage
{
    public int Id { get; set; }

    public int VoucherId { get; set; }

    public int? CustomerId { get; set; }

    public int OrderId { get; set; }

    public decimal DiscountAmount { get; set; }

    public DateTime UsedDate { get; set; }

    public virtual Customer Customer { get; set; }

    public virtual CustomerOrder Order { get; set; }

    public virtual Voucher Voucher { get; set; }
}
