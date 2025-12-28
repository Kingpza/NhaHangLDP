using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class EmailConfig
{
    public int Id { get; set; }

    public string SmtpServer { get; set; }

    public int SmtpPort { get; set; }

    public string SmtpUsername { get; set; }

    public string SmtpPassword { get; set; }

    public bool EnableSsl { get; set; }

    public string FromEmail { get; set; }

    public string FromName { get; set; }

    public string DefaultCc { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
