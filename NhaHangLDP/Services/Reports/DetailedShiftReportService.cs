using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class DetailedShiftReportService
    {
        private readonly MyDbContext db;

        public DetailedShiftReportService(MyDbContext context)
        {
            db = context;
        }

        public object GetDetailedShiftReportData(DateTime shiftDate, string cashierName)
        {
            var startDate = shiftDate.Date;
            var endDate = startDate.AddDays(1);

            var shiftsQuery = db.CashierShifts
                .Include(s => s.Cashier)
                .Include(s => s.Orders).ThenInclude(o => o.OrderDetails)
                .Include(s => s.Orders).ThenInclude(o => o.Bills)
                .Include(s => s.Orders).ThenInclude(o => o.Table)
                .Where(s => s.StartTime >= startDate && s.StartTime < endDate);

            if (!string.IsNullOrEmpty(cashierName))
            {
                shiftsQuery = shiftsQuery.Where(s => s.Cashier != null && s.Cashier.FullName.Contains(cashierName));
            }

            var shifts = shiftsQuery.OrderBy(s => s.StartTime).ToList();

            var shiftData = shifts.Select(shift => new
            {
                ShiftId = shift.Id,
                ShiftName = GetShiftDisplayName(shift.StartTime),
                StartTime = shift.StartTime.ToString("HH:mm"),
                EndTime = shift.EndTime?.ToString("HH:mm") ?? "Đang làm việc",
                Duration = shift.EndTime.HasValue ?
                    $"{(int)(shift.EndTime.Value - shift.StartTime).TotalHours}h {(shift.EndTime.Value - shift.StartTime).Minutes % 60}m" :
                    $"{(int)(DateTime.Now - shift.StartTime).TotalHours}h {(DateTime.Now - shift.StartTime).Minutes % 60}m",
                MainCashier = shift.Cashier?.FullName ?? "N/A",
                Status = shift.Status,
                StatusDisplay = shift.Status == "Active" ? "Đang hoạt động" :
                               shift.Status == "Closed" ? "Đã đóng" : shift.Status,

                TotalOrders = shift.Orders.Count,
                CompletedOrders = shift.Orders.Count(o => o.Status == "Completed"),
                CancelledOrders = shift.Orders.Count(o => o.Status == "Cancelled"),
                PendingOrders = shift.Orders.Count(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready"),

                TotalRevenue = shift.Orders
                    .SelectMany(o => o.Bills.Where(b => b.Status == "Paid"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                CashRevenue = shift.Orders
                    .SelectMany(o => o.Bills.Where(b => b.Status == "Paid" && b.PaymentMethod == "cash"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                CardRevenue = shift.Orders
                    .SelectMany(o => o.Bills.Where(b => b.Status == "Paid" && b.PaymentMethod != "cash"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                AverageOrderValue = shift.Orders.Any(o => o.Bills.Any(b => b.Status == "Paid")) ?
                    shift.Orders.Where(o => o.Bills.Any(b => b.Status == "Paid"))
                        .Average(o => o.Bills.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount)) : 0,

                TablesServed = shift.Orders.Select(o => o.TableId).Distinct().Count(),
                CustomersServed = shift.Orders.Sum(o => o.OrderDetails.Sum(od => od.Quantity)) / 2,

                InitialCash = shift.InitialCash,
                FinalCash = shift.FinalCash,

                TopDishes = shift.Orders
                    .SelectMany(o => o.OrderDetails)
                    .Where(od => od.Order.Bills.Any(b => b.Status == "Paid"))
                    .GroupBy(od => od.MenuItem.Name)
                    .Select(g => new
                    {
                        DishName = g.Key,
                        Quantity = g.Sum(od => od.Quantity),
                        Revenue = g.Sum(od => od.Quantity * od.PriceAtTime)
                    })
                    .OrderByDescending(x => x.Quantity)
                    .Take(5)
                    .ToList(),

                HourlyBreakdown = GetHourlyBreakdown(shift),

                PaymentMethods = shift.Orders
                    .SelectMany(o => o.Bills.Where(b => b.Status == "Paid"))
                    .GroupBy(b => b.PaymentMethod)
                    .Select(g => new
                    {
                        Method = GetPaymentMethodDisplay(g.Key),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.FinalAmount)
                    })
                    .ToList()
            }).ToList();

            var totalRevenue = shiftData.Sum(s => s.TotalRevenue);
            var totalOrders = shiftData.Sum(s => s.TotalOrders);
            var totalCustomers = shiftData.Sum(s => s.CustomersServed);
            var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            return new
            {
                ShiftDate = shiftDate.ToString("yyyy-MM-dd"),
                DateDisplay = shiftDate.ToString("dd/MM/yyyy"),
                CashierName = cashierName,
                Shifts = shiftData,
                Summary = new
                {
                    TotalShifts = shiftData.Count,
                    TotalRevenue = totalRevenue,
                    TotalOrders = totalOrders,
                    TotalCustomers = totalCustomers,
                    AverageOrderValue = averageOrderValue,
                    ActiveShifts = shiftData.Count(s => s.Status == "Active"),
                    CompletedShifts = shiftData.Count(s => s.Status == "Closed")
                },
                LastUpdated = DateTime.Now
            };
        }

        private object GetHourlyBreakdown(CashierShift shift)
        {
            var orders = shift.Orders.Where(o => o.Bills.Any(b => b.Status == "Paid")).ToList();

            return orders
                .GroupBy(o => o.OrderTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key + ":00",
                    OrderCount = g.Count(),
                    Revenue = g.SelectMany(o => o.Bills.Where(b => b.Status == "Paid")).Sum(b => b.FinalAmount)
                })
                .OrderBy(x => x.Hour)
                .ToList();
        }

        private string GetShiftDisplayName(DateTime startTime)
        {
            var hour = startTime.Hour;
            if (hour >= 6 && hour < 14) return "Ca Sáng";
            if (hour >= 14 && hour < 22) return "Ca Chiều";
            return "Ca Tối";
        }

        private string GetPaymentMethodDisplay(string method)
        {
            switch (method?.ToLower())
            {
                case "cash": return "Tiền mặt";
                case "card": return "Thẻ tín dụng";
                case "transfer": return "Chuyển khoản";
                case "ewallet": return "Ví điện tử";
                default: return method ?? "Không xác định";
            }
        }
    }
}
