using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Reservation
{
    public int Id { get; set; }

    public string ReservationCode { get; set; }

    public int? CustomerId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerPhone { get; set; }

    public string CustomerEmail { get; set; }

    public DateOnly ReservationDate { get; set; }

    public TimeOnly ReservationTime { get; set; }

    public int NumberOfGuests { get; set; }

    public int? TableId { get; set; }

    public string TablePreference { get; set; }

    public string SpecialRequests { get; set; }

    public string Status { get; set; }

    public decimal? DepositAmount { get; set; }

    public bool DepositPaid { get; set; }

    public string CancelReason { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ConfirmedDate { get; set; }

    public DateTime? CancelledDate { get; set; }

    public virtual Customer Customer { get; set; }
}
