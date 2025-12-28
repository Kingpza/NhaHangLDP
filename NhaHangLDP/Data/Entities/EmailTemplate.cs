using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class EmailTemplate
{
    public int Id { get; set; }

    public string TemplateCode { get; set; }

    public string Name { get; set; }

    public string Subject { get; set; }

    public string HtmlBody { get; set; }

    public string TextBody { get; set; }

    public string AvailablePlaceholders { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
