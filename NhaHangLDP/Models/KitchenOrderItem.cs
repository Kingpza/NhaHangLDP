using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class KitchenOrderItem
{
    public int Id { get; set; }

    public int KitchenOrderTicketId { get; set; }

    public int? OrderDetailId { get; set; }

    public int MenuItemId { get; set; }

    public string ItemName { get; set; }

    public int Quantity { get; set; }

    public int CompletedQuantity { get; set; }

    public string Status { get; set; }

    public string CustomerNotes { get; set; }

    public string KitchenNotes { get; set; }

    public DateTime? StartedTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public string Station { get; set; }

    public virtual KitchenOrderTicket KitchenOrderTicket { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
