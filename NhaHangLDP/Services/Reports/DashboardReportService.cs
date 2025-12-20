using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class DashboardReportService
    {
        private readonly NhaHangLDPEntities db;

        public DashboardReportService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public DashboardSummary GetDashboardSummary()
        {
            var today = DateTime.Today;
            var todayEnd = today.AddDays(1);

            var todayOrders = db.Order
                .Where(o => o.OrderTime >= today && o.OrderTime < todayEnd)
                .Count();

            var todayRevenue = db.Bill
                .Where(b => b.BillDate >= today && b.BillDate < todayEnd && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var totalTables = db.RestaurantTable.Count();
            var occupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied");
            var tableUtilization = totalTables > 0 ? (occupiedTables * 100 / totalTables) : 0;

            var avgOrderValue = todayOrders > 0 ? todayRevenue / todayOrders : 0;

            return new DashboardSummary
            {
                TodayOrders = todayOrders,
                TodayRevenue = todayRevenue,
                TableUtilization = tableUtilization,
                OccupiedTables = occupiedTables,
                TotalTables = totalTables,
                AvgOrderValue = avgOrderValue,
                ProfitMargin = 40,
                LastUpdated = DateTime.Now
            };
        }

        public string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ";
        }
    }

    public class DashboardSummary
    {
        public int TodayOrders { get; set; }
        public decimal TodayRevenue { get; set; }
        public int TableUtilization { get; set; }
        public int OccupiedTables { get; set; }
        public int TotalTables { get; set; }
        public decimal AvgOrderValue { get; set; }
        public int ProfitMargin { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
