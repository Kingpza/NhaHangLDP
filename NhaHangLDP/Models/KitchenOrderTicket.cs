using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class KitchenOrderTicket
{
    public int Id { get; set; }

    public string TicketCode { get; set; }

    public int OrderId { get; set; }

    public int TableId { get; set; }

    public string Status { get; set; }

    public int Priority { get; set; }

    public string SpecialNotes { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? StartedTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public int EstimatedMinutes { get; set; }

    public int? AssignedChefId { get; set; }

    public string KitchenStation { get; set; }

    public bool IsPrinted { get; set; }

    public int PrintCount { get; set; }

    public virtual Employee AssignedChef { get; set; }

    public virtual ICollection<KitchenOrderItem> KitchenOrderItems { get; set; } = new List<KitchenOrderItem>();

    public virtual Order Order { get; set; }

    public virtual RestaurantTable Table { get; set; }
}
