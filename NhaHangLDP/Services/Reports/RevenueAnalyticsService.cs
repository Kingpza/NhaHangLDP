using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class RevenueAnalyticsService
    {
        private readonly NhaHangLDPEntities db;

        public RevenueAnalyticsService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public RevenueAnalyticsData GetRevenueAnalytics(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, out startDate, out endDate);

            var totalRevenue = db.Bill
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var totalCost = totalRevenue * 0.6m;

            return new RevenueAnalyticsData
            {
                Period = period,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                NetProfit = totalRevenue - totalCost,
                ProfitMargin = totalRevenue > 0 ? ((totalRevenue - totalCost) / totalRevenue) * 100 : 0,
                LastUpdated = DateTime.Now
            };
        }

        public object GetRevenueChartData(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, out startDate, out endDate);

            var paidBills = db.Bill
                .Include(b => b.Order)
                .Include(b => b.Order.OrderDetail)
                .Include(b => b.Order.OrderDetail.Select(od => od.MenuItem))
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .ToList();

            var totalRevenue = paidBills.Sum(b => b.FinalAmount);
            var totalCost = totalRevenue * 0.6m;
            var netProfit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue * 100) : 0;

            var previousStart = GetPreviousPeriodStart(startDate, period);
            var previousEnd = GetPreviousPeriodEnd(previousStart, period);
            var previousBills = db.Bill
                .Where(b => b.BillDate >= previousStart && b.BillDate < previousEnd && b.Status == "Paid")
                .ToList();
            var previousRevenue = previousBills.Sum(b => b.FinalAmount);

            var growthAmount = totalRevenue - previousRevenue;
            var growthPercentage = previousRevenue > 0 ? (growthAmount / previousRevenue * 100) : 0;

            var trendData = GenerateTrendData(paidBills, period, startDate, endDate);
            var categoryData = GenerateCategoryData(paidBills);

            return new
            {
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                NetProfit = netProfit,
                ProfitMargin = profitMargin,
                TrendData = trendData,
                CategoryData = categoryData,
                ComparisonData = new
                {
                    CurrentPeriod = totalRevenue,
                    PreviousPeriod = previousRevenue,
                    GrowthAmount = growthAmount,
                    GrowthPercentage = growthPercentage
                },
                Period = period,
                LastUpdated = DateTime.Now,
                DataCount = paidBills.Count,
                DateRange = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}"
            };
        }

        public object CompareRevenueReports(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, out startDate, out endDate);

            var dishRevenue = db.OrderDetail
                .Include(od => od.Order.Bill)
                .Where(od => od.Order.OrderTime >= startDate
                          && od.Order.OrderTime < endDate
                          && od.Order.Bill.Any(b => b.Status == "Paid"))
                .Sum(od => (decimal?)(od.Quantity * od.PriceAtTime)) ?? 0;

            var totalBillRevenue = db.Bill
                .Where(b => b.BillDate >= startDate
                          && b.BillDate < endDate
                          && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var preTaxRevenue = db.Bill
                .Where(b => b.BillDate >= startDate
                          && b.BillDate < endDate
                          && b.Status == "Paid")
                .Sum(b => (decimal?)b.TotalAmount) ?? 0;

            var serviceFeeAndTax = totalBillRevenue - preTaxRevenue;

            var billCount = db.Bill
                .Where(b => b.BillDate >= startDate
                          && b.BillDate < endDate
                          && b.Status == "Paid")
                .Count();

            return new
            {
                Period = period,
                DateRange = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                DishRevenue = new { Amount = dishRevenue, Description = "Chỉ tính giá trị món ăn đã bán" },
                PreTaxRevenue = new { Amount = preTaxRevenue, Description = "Doanh thu trước thuế và phí dịch vụ" },
                TotalBillRevenue = new { Amount = totalBillRevenue, Description = "Tổng doanh thu cuối cùng" },
                ServiceFeeAndTax = new { Amount = serviceFeeAndTax, Description = "Phí dịch vụ + VAT" },
                BillCount = billCount,
                AverageBillValue = billCount > 0 ? totalBillRevenue / billCount : 0,
                Explanation = new
                {
                    DishProfitAnalysis = "Chỉ tính giá món ăn để phân tích lợi nhuận từ nguyên liệu",
                    RevenueAnalysis = "Tính tổng doanh thu bao gồm tất cả khoản thu",
                    Difference = Math.Abs(totalBillRevenue - dishRevenue),
                    ReasonForDifference = "VAT, phí dịch vụ, giảm giá, và các khoản phụ thu khác"
                }
            };
        }

        private object[] GenerateTrendData(List<Bill> bills, string period, DateTime startDate, DateTime endDate)
        {
            var trendData = new List<object>();

            switch (period?.ToLower())
            {
                case "today":
                    for (int hour = 6; hour <= 23; hour++)
                    {
                        var hourStart = startDate.AddHours(hour);
                        var hourEnd = hourStart.AddHours(1);
                        var hourBills = bills.Where(b => b.BillDate >= hourStart && b.BillDate < hourEnd).ToList();

                        trendData.Add(new
                        {
                            Label = $"{hour}:00",
                            Revenue = hourBills.Sum(b => b.FinalAmount),
                            OrderCount = hourBills.Count
                        });
                    }
                    break;
                case "week":
                    for (int day = 0; day < 7; day++)
                    {
                        var dayStart = startDate.AddDays(day);
                        var dayEnd = dayStart.AddDays(1);
                        var dayBills = bills.Where(b => b.BillDate >= dayStart && b.BillDate < dayEnd).ToList();

                        trendData.Add(new
                        {
                            Label = dayStart.ToString("dd/MM"),
                            Revenue = dayBills.Sum(b => b.FinalAmount),
                            OrderCount = dayBills.Count
                        });
                    }
                    break;
                case "month":
                    var currentWeekStart = startDate;
                    int weekNumber = 1;
                    while (currentWeekStart < endDate)
                    {
                        var weekEnd = currentWeekStart.AddDays(7);
                        if (weekEnd > endDate) weekEnd = endDate;
                        var weekBills = bills.Where(b => b.BillDate >= currentWeekStart && b.BillDate < weekEnd).ToList();

                        trendData.Add(new
                        {
                            Label = $"Tuần {weekNumber}",
                            Revenue = weekBills.Sum(b => b.FinalAmount),
                            OrderCount = weekBills.Count
                        });
                        currentWeekStart = weekEnd;
                        weekNumber++;
                    }
                    break;
                case "year":
                    for (int month = 1; month <= 12; month++)
                    {
                        var monthStart = new DateTime(startDate.Year, month, 1);
                        var monthEnd = monthStart.AddMonths(1);
                        var monthBills = bills.Where(b => b.BillDate >= monthStart && b.BillDate < monthEnd).ToList();

                        trendData.Add(new
                        {
                            Label = $"T{month}",
                            Revenue = monthBills.Sum(b => b.FinalAmount),
                            OrderCount = monthBills.Count
                        });
                    }
                    break;
                default:
                    trendData.Add(new
                    {
                        Label = "Hiện tại",
                        Revenue = bills.Sum(b => b.FinalAmount),
                        OrderCount = bills.Count
                    });
                    break;
            }
            return trendData.ToArray();
        }

        private object[] GenerateCategoryData(List<Bill> bills)
        {
            try
            {
                if (!bills.Any())
                {
                    return new object[]
                    {
                        new { Category = "Món chính", Revenue = 0m },
                        new { Category = "Đồ uống", Revenue = 0m },
                        new { Category = "Tráng miệng", Revenue = 0m }
                    };
                }

                var categoryData = bills
                    .Where(b => b.Order != null && b.Order.OrderDetail != null)
                    .SelectMany(b => b.Order.OrderDetail)
                    .Where(od => od.MenuItem != null)
                    .GroupBy(od => od.MenuItem.Category ?? "Khác")
                    .Select(g => new
                    {
                        Category = g.Key,
                        Revenue = g.Sum(od => od.Quantity * od.PriceAtTime)
                    })
                    .OrderByDescending(x => x.Revenue)
                    .Take(5)
                    .ToArray();

                return categoryData;
            }
            catch
            {
                return new object[] { new { Category = "Lỗi dữ liệu", Revenue = 0m } };
            }
        }

        private void GetDateRange(string period, out DateTime startDate, out DateTime endDate)
        {
            switch (period?.ToLower())
            {
                case "today":
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1);
                    break;
                case "week":
                    startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    endDate = startDate.AddDays(7);
                    break;
                case "quarter":
                    var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                    startDate = new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3);
                    break;
                case "year":
                    startDate = new DateTime(DateTime.Today.Year, 1, 1);
                    endDate = startDate.AddYears(1);
                    break;
                default:
                    startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    endDate = startDate.AddMonths(1);
                    break;
            }
        }

        private DateTime GetPreviousPeriodStart(DateTime currentStart, string period)
        {
            switch (period?.ToLower())
            {
                case "week": return currentStart.AddDays(-7);
                case "month": return currentStart.AddMonths(-1);
                case "year": return currentStart.AddYears(-1);
                default: return currentStart.AddDays(-1);
            }
        }

        private DateTime GetPreviousPeriodEnd(DateTime previousStart, string period)
        {
            switch (period?.ToLower())
            {
                case "week": return previousStart.AddDays(7);
                case "month": return previousStart.AddMonths(1);
                case "year": return previousStart.AddYears(1);
                default: return previousStart.AddDays(1);
            }
        }
    }

    public class RevenueAnalyticsData
    {
        public string Period { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
