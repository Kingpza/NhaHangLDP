using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class PerformanceReview
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public int ReviewerId { get; set; }

    public DateOnly ReviewDate { get; set; }

    public string ReviewPeriod { get; set; }

    public int ServiceQuality { get; set; }

    public int Punctuality { get; set; }

    public int Teamwork { get; set; }

    public int Communication { get; set; }

    public int WorkEfficiency { get; set; }

    public decimal? OverallScore { get; set; }

    public string Strengths { get; set; }

    public string AreasToImprove { get; set; }

    public string Comments { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual Employee Employee { get; set; }

    public virtual Employee Reviewer { get; set; }
}
