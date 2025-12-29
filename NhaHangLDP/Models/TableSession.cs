using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class TableSession
{
    public int Id { get; set; }

    public int TableId { get; set; }

    public string SessionToken { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string Status { get; set; }

    public int? GuestCount { get; set; }

    public virtual RestaurantTable Table { get; set; }
}
