using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class SalesAnalyticsService
    {
        private readonly MyDbContext db;

        public SalesAnalyticsService(MyDbContext context)
        {
            db = context;
        }

        public SalesAnalyticsData GetSalesAnalytics(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, out startDate, out endDate);

            var totalOrders = db.Orders
                .Where(o => o.OrderTime >= startDate && o.OrderTime < endDate)
                .Count();

            var totalRevenue = db.Bills
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0m;

            var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0m;
            var hoursWorked = Math.Max(1, (DateTime.Now - startDate).Hours);
            var revenuePerHour = totalRevenue / hoursWorked;

            var target = period == "today" ? 2000000m : period == "week" ? 10000000m : 30000000m;
            var achievement = target > 0 ? Math.Round(totalRevenue / target * 100, 1) : 0;
            var remaining = Math.Max(0, target - totalRevenue);

            return new SalesAnalyticsData
            {
                Period = period,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                AverageOrderValue = averageOrderValue,
                RevenuePerHour = revenuePerHour,
                Target = target,
                Achievement = achievement,
                Remaining = remaining,
                LastUpdated = DateTime.Now
            };
        }

        public object GetSalesChartData(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, out startDate, out endDate);

            var bills = db.Bills
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .ToList();

            var totalRevenue = bills.Sum(b => (decimal?)b.FinalAmount) ?? 0m;
            var totalOrders = db.Orders.Count(o => o.OrderTime >= startDate && o.OrderTime < endDate);
            var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0m;
            var activeCashiers = db.CashierShifts.Count(s => s.Status == "Active");

            var previousStart = GetPreviousPeriodStart(startDate, period);
            var previousEnd = GetPreviousPeriodEnd(previousStart, period);
            var previousRevenue = db.Bills
                .Where(b => b.BillDate >= previousStart && b.BillDate < previousEnd && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0m;

            var revenueGrowth = CalculateGrowthRate(totalRevenue, previousRevenue);

            var comparison = new object[]
            {
                new { period = GetCurrentPeriodText(period), value = totalRevenue, change = revenueGrowth },
                new { period = GetPreviousPeriodText(period), value = previousRevenue, change = 0 }
            };

            var chartData = GenerateChartData(bills, period, startDate, endDate);

            var shifts = db.CashierShifts
                .Include(s => s.Cashier)
                .Where(s => s.StartTime >= startDate && s.StartTime < endDate)
                .ToList();

            var cashierData = shifts
                .Select(shift => new
                {
                    name = shift.Cashier?.FullName ?? "Thu Ngân",
                    orders = db.Orders.Count(o => o.ShiftId == shift.Id),
                    hours = shift.EndTime.HasValue ? (int)(shift.EndTime.Value - shift.StartTime).TotalHours : (int)(DateTime.Now - shift.StartTime).TotalHours,
                    shifts = 1,
                    revenue = shift.TotalRevenue ?? 0,
                    target = 2000000m
                })
                .GroupBy(c => c.name)
                .Select(g => new
                {
                    name = g.Key,
                    orders = g.Sum(s => s.orders),
                    hours = g.Sum(s => s.hours),
                    shifts = g.Count(),
                    revenue = g.Sum(s => s.revenue),
                    target = g.Sum(s => s.target)
                })
                .OrderByDescending(g => g.revenue)
                .Take(5)
                .ToList();

            return new
            {
                totalRevenue = totalRevenue,
                totalOrders = totalOrders,
                avgOrderValue = avgOrderValue,
                activeCashiers = activeCashiers,
                revenueGrowth = revenueGrowth,
                ordersGrowth = 0m,
                avgValueGrowth = 0m,
                cashiersGrowth = 0m,
                cashiers = cashierData,
                chartData = chartData,
                comparison = comparison,
                dateRange = $"{startDate:dd/MM/yyyy} - {endDate.AddTicks(-1):dd/MM/yyyy}"
            };
        }

        private object GenerateChartData(List<Bill> bills, string period, DateTime startDate, DateTime endDate)
        {
            var labels = new List<string>();
            var revenues = new List<decimal>();
            var orders = new List<int>();

            switch (period?.ToLower())
            {
                case "today":
                    for (int hour = 6; hour <= 23; hour++)
                    {
                        var hourStart = startDate.AddHours(hour);
                        var hourEnd = hourStart.AddHours(1);
                        var hourBills = bills.Where(b => b.BillDate >= hourStart && b.BillDate < hourEnd).ToList();

                        labels.Add($"{hour}:00");
                        revenues.Add(hourBills.Sum(b => b.FinalAmount));
                        orders.Add(hourBills.Count);
                    }
                    break;
                case "week":
                    for (int day = 0; day < 7; day++)
                    {
                        var dayStart = startDate.AddDays(day);
                        var dayEnd = dayStart.AddDays(1);
                        var dayBills = bills.Where(b => b.BillDate >= dayStart && b.BillDate < dayEnd).ToList();

                        labels.Add(dayStart.ToString("dd/MM"));
                        revenues.Add(dayBills.Sum(b => b.FinalAmount));
                        orders.Add(dayBills.Count);
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

                        labels.Add($"Tuần {weekNumber}");
                        revenues.Add(weekBills.Sum(b => b.FinalAmount));
                        orders.Add(weekBills.Count);
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

                        labels.Add($"T{month}");
                        revenues.Add(monthBills.Sum(b => b.FinalAmount));
                        orders.Add(monthBills.Count);
                    }
                    break;
                default:
                    labels.Add("Hiện tại");
                    revenues.Add(bills.Sum(b => b.FinalAmount));
                    orders.Add(bills.Count);
                    break;
            }

            return new { labels = labels, revenues = revenues, orders = orders };
        }

        private void GetDateRange(string period, out DateTime startDate, out DateTime endDate)
        {
            switch (period?.ToLower())
            {
                case "week":
                    startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    endDate = startDate.AddDays(7);
                    break;
                case "month":
                    startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    endDate = startDate.AddMonths(1);
                    break;
                case "year":
                    startDate = new DateTime(DateTime.Today.Year, 1, 1);
                    endDate = startDate.AddYears(1);
                    break;
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1);
                    break;
            }
        }

        private decimal CalculateGrowthRate(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100m : 0m;
            return Math.Round(((current - previous) / previous) * 100, 2);
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

        private string GetCurrentPeriodText(string period)
        {
            switch (period?.ToLower())
            {
                case "today": return "Hôm nay";
                case "week": return "Tuần này";
                case "month": return "Tháng này";
                case "year": return "Năm này";
                default: return "Hiện tại";
            }
        }

        private string GetPreviousPeriodText(string period)
        {
            switch (period?.ToLower())
            {
                case "today": return "Hôm qua";
                case "week": return "Tuần trước";
                case "month": return "Tháng trước";
                case "year": return "Năm trước";
                default: return "Kỳ trước";
            }
        }
    }

    public class SalesAnalyticsData
    {
        public string Period { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal RevenuePerHour { get; set; }
        public decimal Target { get; set; }
        public decimal Achievement { get; set; }
        public decimal Remaining { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
