using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class CustomerAddress
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string ReceiverName { get; set; }

    public string ReceiverPhone { get; set; }

    public string AddressLine { get; set; }

    public string Ward { get; set; }

    public string District { get; set; }

    public string City { get; set; }

    public string AddressType { get; set; }

    public bool IsDefault { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public virtual Customer Customer { get; set; }
}
