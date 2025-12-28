using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho Advanced Dashboard
    /// </summary>
    public class AdvancedDashboardViewModel
    {
        public DashboardSummary Summary { get; set; }
        public List<RevenueDataPoint> RevenueChart { get; set; }
        public List<CategoryRevenue> CategoryBreakdown { get; set; }
        public List<TopPerformer> TopPerformers { get; set; }
        public List<AlertItem> Alerts { get; set; }
        public List<KPIMetric> KPIs { get; set; }
        public PredictionResult Predictions { get; set; }
        public string DateRange { get; set; }
        public string Period { get; set; }
        public DateTime LastUpdated { get; set; }

        public AdvancedDashboardViewModel()
        {
            Summary = new DashboardSummary();
            RevenueChart = new List<RevenueDataPoint>();
            CategoryBreakdown = new List<CategoryRevenue>();
            TopPerformers = new List<TopPerformer>();
            Alerts = new List<AlertItem>();
            KPIs = new List<KPIMetric>();
            Predictions = new PredictionResult();
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// Tổng hợp dashboard
    /// </summary>
    public class DashboardSummary
    {
        public decimal TotalRevenue { get; set; }
        public decimal RevenueGrowth { get; set; }
        public int TotalOrders { get; set; }
        public int OrdersGrowth { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal AvgOrderGrowth { get; set; }
        public int TotalCustomers { get; set; }
        public int NewCustomers { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public int TableTurnover { get; set; }
        public decimal RevPASH { get; set; } // Revenue per Available Seat Hour
        public decimal TableUtilization { get; set; } // Percentage of tables occupied
    }

    /// <summary>
    /// Data point cho biểu đồ doanh thu
    /// </summary>
    public class RevenueDataPoint
    {
        public string Label { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public int OrderCount { get; set; }
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Doanh thu theo danh mục
    /// </summary>
    public class CategoryRevenue
    {
        public string Category { get; set; }
        public decimal Revenue { get; set; }
        public decimal Percentage { get; set; }
        public int ItemCount { get; set; }
        public string Color { get; set; }
    }

    /// <summary>
    /// Top performers (món ăn, nhân viên, etc.)
    /// </summary>
    public class TopPerformer
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "dish", "employee", "table"
        public decimal Value { get; set; }
        public int Count { get; set; }
        public decimal Growth { get; set; }
        public string ImageUrl { get; set; }
    }

    /// <summary>
    /// Alert item cho dashboard
    /// </summary>
    public class AlertItem
    {
        public string Id { get; set; }
        public string Type { get; set; } // "warning", "danger", "info", "success"
        public string Title { get; set; }
        public string Message { get; set; }
        public string Action { get; set; }
        public string ActionUrl { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// KPI Metric
    /// </summary>
    public class KPIMetric
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal TargetValue { get; set; }
        public decimal PreviousValue { get; set; }
        public string Unit { get; set; }
        public string Format { get; set; } // "currency", "percentage", "number"
        public decimal Achievement { get; set; } // percentage of target achieved
        public string Trend { get; set; } // "up", "down", "stable"
        public string Color { get; set; }
    }

    /// <summary>
    /// Prediction result
    /// </summary>
    public class PredictionResult
    {
        public decimal PredictedRevenue { get; set; }
        public decimal PredictedProfit { get; set; }
        public int PredictedOrders { get; set; }
        public decimal Confidence { get; set; }
        public string Period { get; set; }
        public List<PredictionDataPoint> TrendPrediction { get; set; }

        public PredictionResult()
        {
            TrendPrediction = new List<PredictionDataPoint>();
        }
    }

    public class PredictionDataPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public bool IsActual { get; set; }
    }

    /// <summary>
    /// Menu Engineering Matrix
    /// </summary>
    public class MenuEngineeringItem
    {
        public int DishId { get; set; }
        public string DishName { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; }
        public int SoldQuantity { get; set; }
        public decimal Popularity { get; set; } // % of total sales
        public string Classification { get; set; } // "Star", "Plow Horse", "Puzzle", "Dog"
        public string Recommendation { get; set; }
        public string ImageUrl { get; set; }
    }

    /// <summary>
    /// ABC Analysis
    /// </summary>
    public class ABCAnalysisItem
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public decimal Revenue { get; set; }
        public decimal CumulativeRevenue { get; set; }
        public decimal CumulativePercentage { get; set; }
        public string Classification { get; set; } // "A", "B", "C"
    }

    /// <summary>
    /// Heatmap data cho peak hours
    /// </summary>
    public class HeatmapData
    {
        public string DayOfWeek { get; set; }
        public int Hour { get; set; }
        public int Value { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>
    /// Customer Analytics
    /// </summary>
    public class CustomerAnalytics
    {
        public int TotalCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public decimal RetentionRate { get; set; }
        public decimal AvgVisitsPerCustomer { get; set; }
        public decimal CustomerLifetimeValue { get; set; }
        public List<CustomerSegment> Segments { get; set; }

        public CustomerAnalytics()
        {
            Segments = new List<CustomerSegment>();
        }
    }

    public class CustomerSegment
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public decimal AvgSpend { get; set; }
        public string Color { get; set; }
    }

    /// <summary>
    /// Anomaly Detection Result
    /// </summary>
    public class AnomalyResult
    {
        public DateTime Date { get; set; }
        public string Metric { get; set; }
        public decimal ActualValue { get; set; }
        public decimal ExpectedValue { get; set; }
        public decimal Deviation { get; set; }
        public string Severity { get; set; } // "low", "medium", "high"
        public string PossibleCause { get; set; }
    }

    /// <summary>
    /// Comparative Analysis
    /// </summary>
    public class ComparativeAnalysis
    {
        public string Period1Name { get; set; }
        public string Period2Name { get; set; }
        public List<ComparisonMetric> Metrics { get; set; }

        public ComparativeAnalysis()
        {
            Metrics = new List<ComparisonMetric>();
        }
    }

    public class ComparisonMetric
    {
        public string Name { get; set; }
        public decimal Period1Value { get; set; }
        public decimal Period2Value { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercentage { get; set; }
        public string Trend { get; set; }
    }

    /// <summary>
    /// Export Request
    /// </summary>
    public class ExportRequest
    {
        public string ReportType { get; set; }
        public string Format { get; set; } // "excel", "pdf", "csv"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<string> SelectedMetrics { get; set; }
        public bool IncludeCharts { get; set; }

        public ExportRequest()
        {
            SelectedMetrics = new List<string>();
        }
    }

    /// <summary>
    /// Shift Performance for comparison
    /// </summary>
    public class ShiftPerformance
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; }
        public string CashierName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal Target { get; set; }
        public decimal Achievement { get; set; }
        public int Duration { get; set; } // minutes
        public decimal RevenuePerHour { get; set; }
    }

    /// <summary>
    /// Inventory Alert
    /// </summary>
    public class InventoryAlert
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        public string Unit { get; set; }
        public string AlertType { get; set; } // "low_stock", "out_of_stock", "expiring"
        public int DaysUntilStockout { get; set; }
        public List<string> AffectedDishes { get; set; }

        public InventoryAlert()
        {
            AffectedDishes = new List<string>();
        }
    }

    /// <summary>
    /// Trend Analysis
    /// </summary>
    public class TrendAnalysis
    {
        public string Metric { get; set; }
        public string TrendDirection { get; set; } // "increasing", "decreasing", "stable"
        public decimal TrendStrength { get; set; } // 0-100
        public decimal Forecast { get; set; }
        public string Insight { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; }

        public TrendAnalysis()
        {
            DataPoints = new List<TrendDataPoint>();
        }
    }

    public class TrendDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
        public decimal MovingAverage { get; set; }
    }
}
