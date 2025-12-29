using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class Notification
{
    public int Id { get; set; }

    public string Type { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }

    public string Level { get; set; }

    public int? RecipientEmployeeId { get; set; }

    public string RecipientRole { get; set; }

    public string ActionUrl { get; set; }

    public string Data { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ReadDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public virtual Employee RecipientEmployee { get; set; }
}
