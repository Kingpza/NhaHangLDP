using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class RestaurantTable
{
    public int Id { get; set; }

    public int TableAreaId { get; set; }

    public string TableNumber { get; set; }

    public int Capacity { get; set; }

    public string Status { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<KitchenOrderTicket> KitchenOrderTickets { get; set; } = new List<KitchenOrderTicket>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<QROrder> QROrders { get; set; } = new List<QROrder>();

    public virtual TableArea TableArea { get; set; }

    public virtual ICollection<TableSession> TableSessions { get; set; } = new List<TableSession>();
}
