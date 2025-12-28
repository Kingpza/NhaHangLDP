using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class EmailLog
{
    public int Id { get; set; }

    public string EmailType { get; set; }

    public string ToEmail { get; set; }

    public string ToName { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public string Status { get; set; }

    public string ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? SentDate { get; set; }

    public int? ReferenceId { get; set; }

    public string ReferenceType { get; set; }
}
