using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    // Dashboard Models
    public class ReportsDashboardViewModel
    {
        public int TodayOrders { get; set; }
        public decimal TodayRevenue { get; set; }
        public string TodayRevenueFormatted { get; set; }
        public int TableUtilization { get; set; }
        public int OccupiedTables { get; set; }
        public int TotalTables { get; set; }
        public decimal AvgOrderValue { get; set; }
        public string AvgOrderValueFormatted { get; set; }
        public decimal ProfitMargin { get; set; }
        public List<TopSellingDish> TopSellingDishes { get; set; } = new List<TopSellingDish>();
        public List<RevenueChart> RevenueChartData { get; set; } = new List<RevenueChart>();
        public List<CategoryChart> CategoryChartData { get; set; } = new List<CategoryChart>();
        public DateTime LastUpdated { get; set; }
    }

    public class TopSellingDish
    {
        public string DishName { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public string Category { get; set; }
    }

    public class RevenueChart
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }

    public class CategoryChart
    {
        public string Category { get; set; }
        public decimal Revenue { get; set; }
        public int Count { get; set; }
    }

    // Booking Analytics Models
    public class BookingAnalyticsViewModel
    {
        public int TotalBookings { get; set; }
        public int SuccessfulBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal BookingRevenue { get; set; }
        public decimal WalkInRevenue { get; set; }
        public decimal AverageBookingValue { get; set; }
        public decimal AveragePartySize { get; set; }
        public decimal TableUtilizationRate { get; set; }
        public int OccupiedTables { get; set; }
        public int TotalTables { get; set; }
        public string Period { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime LastUpdated { get; set; }
        
        public List<BookingTrendData> BookingTrends { get; set; } = new List<BookingTrendData>();
        public List<BookingSourceData> BookingSources { get; set; } = new List<BookingSourceData>();
        public List<HourlyBookingData> HourlyDistribution { get; set; } = new List<HourlyBookingData>();
        public List<AreaBookingData> AreaStatistics { get; set; } = new List<AreaBookingData>();
        public List<BookingDetailItem> TodayBookings { get; set; } = new List<BookingDetailItem>();
        public List<BookingDetailItem> UpcomingBookings { get; set; } = new List<BookingDetailItem>();
        public List<TopCustomerData> TopCustomers { get; set; } = new List<TopCustomerData>();
    }

    public class BookingTrendData
    {
        public string Label { get; set; }
        public int TotalBookings { get; set; }
        public int SuccessfulBookings { get; set; }
        public DateTime Date { get; set; }
    }

    public class BookingSourceData
    {
        public string Source { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class HourlyBookingData
    {
        public string TimeSlot { get; set; }
        public int BookingCount { get; set; }
        public decimal Revenue { get; set; }
        public bool IsPeakHour { get; set; }
        public decimal AveragePartySize { get; set; }
    }

    public class AreaBookingData
    {
        public string AreaName { get; set; }
        public int TotalBookings { get; set; }
        public decimal Revenue { get; set; }
        public decimal UtilizationRate { get; set; }
        public int TotalTables { get; set; }
        public int OccupiedTables { get; set; }
    }

    public class BookingDetailItem
    {
        public int BookingId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime BookingDateTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string TableNumber { get; set; }
        public string TableArea { get; set; }
        public string Status { get; set; }
        public string StatusDisplay { get; set; }
        public string StatusClass { get; set; }
        public string Notes { get; set; }
        public decimal EstimatedValue { get; set; }
    }

    public class TopCustomerData
    {
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int TotalBookings { get; set; }
        public int SuccessfulBookings { get; set; }
        public DateTime LastBookingDate { get; set; }
        public decimal AveragePartySize { get; set; }
        public decimal TotalRevenue { get; set; }
        public string CustomerType { get; set; }
    }

    // Sales Performance Models
    public class SalesPerformanceViewModel
    {
        public List<CashierPerformanceData> CashierPerformance { get; set; } = new List<CashierPerformanceData>();
        public List<HourlyPerformanceData> HourlyPerformance { get; set; } = new List<HourlyPerformanceData>();
        public SalesTargetData Targets { get; set; } = new SalesTargetData();
        public string Period { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CashierPerformanceData
    {
        public string CashierName { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public decimal RevenuePerHour { get; set; }
    }

    public class HourlyPerformanceData
    {
        public string Hour { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class SalesTargetData
    {
        public decimal Target { get; set; }
        public decimal Actual { get; set; }
        public decimal Achievement { get; set; }
        public decimal Remaining { get; set; }
    }

    // Revenue Analytics Models
    public class RevenueAnalyticsViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public List<RevenueTrendData> TrendData { get; set; } = new List<RevenueTrendData>();
        public List<RevenueByCategoryData> CategoryData { get; set; } = new List<RevenueByCategoryData>();
        public RevenueComparisonData ComparisonData { get; set; } = new RevenueComparisonData();
        public string Period { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class RevenueTrendData
    {
        public string Period { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public DateTime Date { get; set; }
    }

    public class RevenueByCategoryData
    {
        public string Category { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public int ItemCount { get; set; }
    }

    public class RevenueComparisonData
    {
        public decimal CurrentPeriod { get; set; }
        public decimal PreviousPeriod { get; set; }
        public decimal GrowthAmount { get; set; }
        public decimal GrowthPercentage { get; set; }
    }

    // Dish Profit Analysis Models
    public class DishProfitAnalysisViewModel
    {
        public List<DishProfitData> DishProfits { get; set; } = new List<DishProfitData>();
        public List<CategoryProfitData> CategoryProfits { get; set; } = new List<CategoryProfitData>();
        public List<ProfitTrendData> ProfitTrends { get; set; } = new List<ProfitTrendData>();
        public string Period { get; set; }
        public DateTime LastUpdated { get; set; }
        public ProfitSummary Summary { get; set; }
        public string DateRange { get; set; }
    }

    public class ProfitSummary
    {
        public decimal TotalProfit { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageProfitMargin { get; set; }
        public int TotalDishes { get; set; }
        public DishProfitData BestDish { get; set; }
        public RevenueComparisonData ProfitComparison { get; set; }
        public RevenueComparisonData RevenueComparison { get; set; }
    }

    public class DishProfitData
    {
        public string DishName { get; set; }
        public string Category { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal UnitProfit { get; set; }
        public double ProfitMargin { get; set; }
        public int SoldQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public class CategoryProfitData
    {
        public string CategoryName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public double ProfitMargin { get; set; }
        public int DishCount { get; set; }
        public int TotalSold { get; set; }
    }

    public class ProfitTrendData
    {
        public string Period { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; }
        public string Label { get; set; }
        public DateTime Date { get; set; }
    }

    public class BookingStatsViewModel
    {
        public int TotalBookings { get; set; }
        public int SuccessfulBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int NoShowBookings { get; set; }
        public List<PeakHourData> PeakHours { get; set; } = new List<PeakHourData>();
        public List<TableUtilizationData> TableUtilization { get; set; } = new List<TableUtilizationData>();
        public string Period { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PeakHourData
    {
        public string Hour { get; set; }
        public int Bookings { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TableUtilizationData
    {
        public string TableSize { get; set; }
        public decimal Utilization { get; set; }
        public int TotalTables { get; set; }
        public int OccupiedTables { get; set; }
    }
}