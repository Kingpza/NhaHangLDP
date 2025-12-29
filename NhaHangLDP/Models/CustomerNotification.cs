using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class CustomerNotification
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }

    public string Type { get; set; }

    public string ActionUrl { get; set; }

    public string ImageUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ReadDate { get; set; }

    public virtual Customer Customer { get; set; }
}
