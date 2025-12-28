using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Booking
{
    public int Id { get; set; }

    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public DateTime BookingDateTime { get; set; }

    public int NumberOfGuests { get; set; }

    public int? TableId { get; set; }

    public string Status { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual RestaurantTable Table { get; set; }
}
