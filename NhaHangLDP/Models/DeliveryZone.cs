using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class DeliveryZone
{
    public int Id { get; set; }

    public string ZoneName { get; set; }

    public string District { get; set; }

    public string Ward { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal MinOrderForFreeDelivery { get; set; }

    public int EstimatedTime { get; set; }

    public decimal? MaxDistance { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
