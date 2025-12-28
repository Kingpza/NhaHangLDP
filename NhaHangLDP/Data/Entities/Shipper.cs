using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Shipper
{
    public int Id { get; set; }

    public string FullName { get; set; }

    public string Phone { get; set; }

    public string Email { get; set; }

    public string PasswordHash { get; set; }

    public string VehicleType { get; set; }

    public string LicensePlate { get; set; }

    public string AvatarUrl { get; set; }

    public string Status { get; set; }

    public decimal Rating { get; set; }

    public int TotalDeliveries { get; set; }

    public decimal TotalEarnings { get; set; }

    public decimal? CurrentLatitude { get; set; }

    public decimal? CurrentLongitude { get; set; }

    public DateTime? LastLocationUpdate { get; set; }

    public DateTime CreatedDate { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<DeliveryAssignment> DeliveryAssignments { get; set; } = new List<DeliveryAssignment>();
}
