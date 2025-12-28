using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Order
{
    public int Id { get; set; }

    public int TableId { get; set; }

    public int WaiterId { get; set; }

    public int ShiftId { get; set; }

    public DateTime OrderTime { get; set; }

    public string Status { get; set; }

    public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();

    public virtual ICollection<KitchenOrderTicket> KitchenOrderTickets { get; set; } = new List<KitchenOrderTicket>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Qrorder> Qrorders { get; set; } = new List<Qrorder>();

    public virtual CashierShift Shift { get; set; }

    public virtual RestaurantTable Table { get; set; }

    public virtual Employee Waiter { get; set; }
}
