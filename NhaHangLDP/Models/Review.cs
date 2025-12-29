using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class Review
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int MenuItemId { get; set; }

    public int? OrderId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; }

    public string ImageUrls { get; set; }

    public int LikesCount { get; set; }

    public bool IsVerifiedPurchase { get; set; }

    public string AdminReply { get; set; }

    public DateTime? AdminReplyDate { get; set; }

    public bool IsApproved { get; set; }

    public bool IsHidden { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Customer Customer { get; set; }

    public virtual MenuItem MenuItem { get; set; }

    public virtual CustomerOrder Order { get; set; }
}
