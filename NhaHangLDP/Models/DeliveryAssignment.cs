using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class DeliveryAssignment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ShipperId { get; set; }

    public DateTime AssignedTime { get; set; }

    public DateTime? PickupTime { get; set; }

    public DateTime? DeliveryTime { get; set; }

    public string Status { get; set; }

    public decimal? CurrentLatitude { get; set; }

    public decimal? CurrentLongitude { get; set; }

    public DateTime? EstimatedArrival { get; set; }

    public decimal? ActualDistance { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal ShipperEarning { get; set; }

    public string Notes { get; set; }

    public string FailureReason { get; set; }

    public string ProofImageUrl { get; set; }

    public int? CustomerRating { get; set; }

    public string CustomerFeedback { get; set; }

    public virtual CustomerOrder Order { get; set; }

    public virtual Shipper Shipper { get; set; }
}
