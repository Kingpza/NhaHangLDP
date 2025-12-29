using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class BookingAnalyticsService
    {
        private readonly MyDbContext db;

        public BookingAnalyticsService(MyDbContext context)
        {
            db = context;
        }

        public BookingAnalyticsData GetBookingAnalytics(string period, DateTime? fromDate = null, DateTime? toDate = null)
        {
            DateTime startDate, endDate;
            GetDateRange(period, fromDate, toDate, out startDate, out endDate);

            var bookingsInPeriod = db.Bookings
                .Where(b => b.BookingDateTime >= startDate && b.BookingDateTime <= endDate)
                .ToList();

            var totalBookings = bookingsInPeriod.Count;
            var successfulBookings = bookingsInPeriod.Count(b => b.Status == "Confirmed" || b.Status == "Completed");
            var cancelledBookings = bookingsInPeriod.Count(b => b.Status == "Cancelled");
            var pendingBookings = bookingsInPeriod.Count(b => b.Status == "Pending");

            var successfulBookingTableIds = bookingsInPeriod
                .Where(b => b.Status == "Confirmed" || b.Status == "Completed")
                .Where(b => b.TableId.HasValue)
                .Select(b => b.TableId.Value)
                .ToList();

            var bookingRevenue = 0m;
            if (successfulBookingTableIds.Any())
            {
                bookingRevenue = db.Orders
                    .Where(o => o.OrderTime >= startDate && o.OrderTime <= endDate)
                    .Where(o => successfulBookingTableIds.Contains(o.TableId))
                    .SelectMany(o => db.Bills.Where(b => b.OrderId == o.Id && b.Status == "Paid"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;
            }

            var walkInRevenue = db.Orders
                .Where(o => o.OrderTime >= startDate && o.OrderTime <= endDate)
                .Where(o => !successfulBookingTableIds.Contains(o.TableId))
                .SelectMany(o => db.Bills.Where(b => b.OrderId == o.Id && b.Status == "Paid"))
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var averagePartySize = bookingsInPeriod.Any() ?
                bookingsInPeriod.Average(b => (decimal)b.NumberOfGuests) : 0;

            var totalTables = db.RestaurantTables.Count();
            var occupiedTables = db.RestaurantTables.Count(t => t.Status == "Occupied");
            var tableUtilizationRate = totalTables > 0 ? Math.Round((decimal)occupiedTables / totalTables * 100, 1) : 0;

            var averageBookingValue = totalBookings > 0 && bookingRevenue > 0 ?
                Math.Round(bookingRevenue / totalBookings, 0) : 0;

            return new BookingAnalyticsData
            {
                Period = period,
                StartDate = startDate,
                EndDate = endDate,
                TotalBookings = totalBookings,
                SuccessfulBookings = successfulBookings,
                CancelledBookings = cancelledBookings,
                PendingBookings = pendingBookings,
                BookingRevenue = bookingRevenue,
                WalkInRevenue = walkInRevenue,
                AverageBookingValue = averageBookingValue,
                AveragePartySize = Math.Round(averagePartySize, 1),
                TableUtilizationRate = tableUtilizationRate,
                OccupiedTables = occupiedTables,
                TotalTables = totalTables,
                LastUpdated = DateTime.Now
            };
        }

        public object GetBookingChartData(string period)
        {
            DateTime startDate, endDate;
            GetDateRange(period, null, null, out startDate, out endDate);

            var bookingsInPeriod = db.Bookings
                .Where(b => b.BookingDateTime >= startDate && b.BookingDateTime <= endDate)
                .ToList();

            var peakHours = bookingsInPeriod
                .GroupBy(b => b.BookingDateTime.Hour)
                .Select(g => new { Hour = g.Key + ":00", Bookings = g.Count() })
                .OrderByDescending(x => x.Bookings)
                .Take(6)
                .ToList();

            var tableUtilization = db.RestaurantTables
                .GroupBy(t => t.Capacity <= 2 ? "Bàn đôi" : t.Capacity <= 4 ? "Bàn 4 người" : "Bàn lớn")
                .Select(g => new
                {
                    TableSize = g.Key,
                    Utilization = g.Count(t => t.Status == "Occupied") * 100 / Math.Max(1, g.Count())
                })
                .ToList();

            return new
            {
                TotalBookings = bookingsInPeriod.Count,
                SuccessfulBookings = bookingsInPeriod.Count(b => b.Status == "Confirmed" || b.Status == "Completed"),
                CancelledBookings = bookingsInPeriod.Count(b => b.Status == "Cancelled"),
                PendingBookings = bookingsInPeriod.Count(b => b.Status == "Pending"),
                NoShowBookings = 0,
                Period = period,
                LastUpdated = DateTime.Now,
                PeakHours = peakHours,
                TableUtilization = tableUtilization
            };
        }

        private void GetDateRange(string period, DateTime? fromDate, DateTime? toDate, out DateTime startDate, out DateTime endDate)
        {
            switch (period?.ToLower())
            {
                case "week":
                    startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    endDate = startDate.AddDays(6).AddDays(1).AddTicks(-1);
                    break;
                case "month":
                    startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                    break;
                case "quarter":
                    var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                    startDate = new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3).AddTicks(-1);
                    break;
                case "custom":
                    startDate = fromDate ?? DateTime.Today.AddDays(-30);
                    endDate = (toDate ?? DateTime.Today).AddDays(1).AddTicks(-1);
                    break;
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
            }
        }
    }

    public class BookingAnalyticsData
    {
        public string Period { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
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
        public DateTime LastUpdated { get; set; }
    }
}
