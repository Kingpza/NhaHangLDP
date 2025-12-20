using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Services;

namespace NhaHangLDP.Controllers
{
    public partial class ReportsManagementController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        private AdvancedAnalyticsService _analyticsService;

        // Constructor
        public ReportsManagementController()
        {
            _analyticsService = new AdvancedAnalyticsService();
        }

        public ActionResult Dashboard()
        {
            try
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
                var profitMargin = 40;

                ViewBag.TodayOrders = todayOrders;
                ViewBag.TodayRevenue = todayRevenue;
                ViewBag.TodayRevenueFormatted = FormatCurrency(todayRevenue);
                ViewBag.TableUtilization = tableUtilization;
                ViewBag.OccupiedTables = occupiedTables;
                ViewBag.TotalTables = totalTables;
                ViewBag.AvgOrderValue = avgOrderValue;
                ViewBag.AvgOrderValueFormatted = FormatCurrency(avgOrderValue);
                ViewBag.ProfitMargin = profitMargin;
                ViewBag.LastUpdated = DateTime.Now;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.TodayOrders = 0;
                ViewBag.TodayRevenue = 0m;
                ViewBag.TodayRevenueFormatted = "0";
                ViewBag.TableUtilization = 0;
                ViewBag.OccupiedTables = 0;
                ViewBag.TotalTables = 0;
                ViewBag.AvgOrderValue = 0;
                ViewBag.AvgOrderValueFormatted = "0";
                ViewBag.ProfitMargin = 0;
                ViewBag.LastUpdated = DateTime.Now;

                return View();
            }
        }

        public ActionResult BookingAnalytics(string period = "today", DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                DateTime startDate, endDate;
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

                var bookingsInPeriod = db.Booking
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
                    bookingRevenue = db.Order
                        .Where(o => o.OrderTime >= startDate && o.OrderTime <= endDate)
                        .Where(o => successfulBookingTableIds.Contains(o.TableId))
                        .SelectMany(o => db.Bill.Where(b => b.OrderId == o.Id && b.Status == "Paid"))
                        .Sum(b => (decimal?)b.FinalAmount) ?? 0;
                }

                var walkInRevenue = db.Order
                    .Where(o => o.OrderTime >= startDate && o.OrderTime <= endDate)
                    .Where(o => !successfulBookingTableIds.Contains(o.TableId))
                    .SelectMany(o => db.Bill.Where(b => b.OrderId == o.Id && b.Status == "Paid"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var averagePartySize = bookingsInPeriod.Any() ?
                    bookingsInPeriod.Average(b => (decimal)b.NumberOfGuests) : 0;

                var totalTables = db.RestaurantTable.Count();
                var occupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied");
                var tableUtilizationRate = totalTables > 0 ? Math.Round((decimal)occupiedTables / totalTables * 100, 1) : 0;

                var averageBookingValue = totalBookings > 0 && bookingRevenue > 0 ?
                    Math.Round(bookingRevenue / totalBookings, 0) : 0;

                ViewBag.Period = period;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;
                ViewBag.TotalBookings = totalBookings;
                ViewBag.SuccessfulBookings = successfulBookings;
                ViewBag.CancelledBookings = cancelledBookings;
                ViewBag.PendingBookings = pendingBookings;
                ViewBag.BookingRevenue = bookingRevenue;
                ViewBag.WalkInRevenue = walkInRevenue;
                ViewBag.AverageBookingValue = averageBookingValue;
                ViewBag.AveragePartySize = Math.Round(averagePartySize, 1);
                ViewBag.TableUtilizationRate = tableUtilizationRate;
                ViewBag.OccupiedTables = occupiedTables;
                ViewBag.TotalTables = totalTables;
                ViewBag.LastUpdated = DateTime.Now;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpPost]
        public JsonResult GetBookingAnalyticsData(string period)
        {
            try
            {
                DateTime startDate, endDate;
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
                    default:
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                        break;
                }

                var bookingsInPeriod = db.Booking
                    .Where(b => b.BookingDateTime >= startDate && b.BookingDateTime <= endDate)
                    .ToList();

                var totalBookings = bookingsInPeriod.Count;
                var successfulBookings = bookingsInPeriod.Count(b => b.Status == "Confirmed" || b.Status == "Completed");
                var cancelledBookings = bookingsInPeriod.Count(b => b.Status == "Cancelled");
                var pendingBookings = bookingsInPeriod.Count(b => b.Status == "Pending");

                var peakHours = bookingsInPeriod
                    .GroupBy(b => b.BookingDateTime.Hour)
                    .Select(g => new { Hour = g.Key + ":00", Bookings = g.Count() })
                    .OrderByDescending(x => x.Bookings)
                    .Take(6)
                    .ToList();

                var tableUtilization = db.RestaurantTable
                    .GroupBy(t => t.Capacity <= 2 ? "Bàn đôi" : t.Capacity <= 4 ? "Bàn 4 người" : "Bàn lớn")
                    .Select(g => new {
                        TableSize = g.Key,
                        Utilization = g.Count(t => t.Status == "Occupied") * 100 / Math.Max(1, g.Count())
                    })
                    .ToList();

                var stats = new
                {
                    TotalBookings = totalBookings,
                    SuccessfulBookings = successfulBookings,
                    CancelledBookings = cancelledBookings,
                    PendingBookings = pendingBookings,
                    NoShowBookings = 0,
                    Period = period,
                    LastUpdated = DateTime.Now,
                    PeakHours = peakHours,
                    TableUtilization = tableUtilization
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ExportBookingAnalytics(string period, string format = "excel")
        {
            return Json(new { success = true, message = "Đã xuất file thành công!" });
        }

        public ActionResult SalesAnalytics(string period = "today")
        {
            try
            {
                DateTime startDate, endDate;
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
                    default:
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(1);
                        break;
                }

                var totalOrders = db.Order
                    .Where(o => o.OrderTime >= startDate && o.OrderTime < endDate)
                    .Count();

                var totalRevenue = db.Bill
                    .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0m;

                var averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0m;
                var hoursWorked = Math.Max(1, (DateTime.Now - startDate).Hours);
                var revenuePerHour = totalRevenue / hoursWorked;

                var target = period == "today" ? 2000000m : period == "week" ? 10000000m : 30000000m;
                var achievement = target > 0 ? Math.Round(totalRevenue / target * 100, 1) : 0;
                var remaining = Math.Max(0, target - totalRevenue);

                ViewBag.Period = period;
                ViewBag.LastUpdated = DateTime.Now;
                ViewBag.CashierName = "Thu Ngân";
                ViewBag.TotalOrders = totalOrders;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.AverageOrderValue = averageOrderValue;
                ViewBag.RevenuePerHour = revenuePerHour;
                ViewBag.Target = target;
                ViewBag.Actual = totalRevenue;
                ViewBag.Achievement = achievement;
                ViewBag.Remaining = remaining;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.Period = period;
                ViewBag.LastUpdated = DateTime.Now;
                ViewBag.CashierName = "N/A";
                ViewBag.TotalOrders = 0;
                ViewBag.TotalRevenue = 0m;
                ViewBag.AverageOrderValue = 0m;
                ViewBag.RevenuePerHour = 0m;
                ViewBag.Target = 0m;
                ViewBag.Actual = 0m;
                ViewBag.Achievement = 0m;
                ViewBag.Remaining = 0m;

                return View();
            }
        }

        [HttpPost]
        public JsonResult GetSalesAnalyticsData(string period)
        {
            try
            {
                DateTime startDate, endDate;
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

                var bills = db.Bill
                    .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                    .ToList();

                var totalRevenue = bills.Sum(b => (decimal?)b.FinalAmount) ?? 0m;
                var totalOrders = db.Order.Count(o => o.OrderTime >= startDate && o.OrderTime < endDate);
                var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0m;
                var activeCashiers = db.CashierShift.Count(s => s.Status == "Active");

                var previousStart = GetPreviousPeriodStart(startDate, period);
                var previousEnd = GetPreviousPeriodEnd(previousStart, period);
                var previousRevenue = db.Bill
                    .Where(b => b.BillDate >= previousStart && b.BillDate < previousEnd && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0m;

                var revenueGrowth = CalculateGrowthRate(totalRevenue, previousRevenue);

                var comparison = new object[]
                {
                    new { period = GetCurrentPeriodText(period), value = totalRevenue, change = revenueGrowth },
                    new { period = GetPreviousPeriodText(period), value = previousRevenue, change = 0 }
                };

                var chartData = GenerateChartData(bills, period, startDate, endDate);

                var shifts = db.CashierShift
                    .Include(s => s.Employee)
                    .Where(s => s.StartTime >= startDate && s.StartTime < endDate)
                    .ToList();

                var cashierData = shifts
                    .Select(shift => new
                    {
                        name = shift.Employee?.FullName ?? "Thu Ngân",
                        orders = db.Order.Count(o => o.ShiftId == shift.Id),
                        hours = shift.EndTime.HasValue ? (int)(shift.EndTime.Value - shift.StartTime).TotalHours : (int)(DateTime.Now - shift.StartTime).TotalHours,
                        shifts = 1,
                        revenue = shift.TotalRevenue ?? 0,
                        target = 2000000m
                    })
                    .GroupBy(c => c.name)
                    .Select(g => new {
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

                var resultData = new
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

                return Json(new { success = true, data = resultData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult RevenueAnalytics(string period = "month")
        {
            try
            {
                DateTime startDate, endDate;
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
                    default:
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1);
                        break;
                }

                var totalRevenue = db.Bill
                    .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var totalCost = totalRevenue * 0.6m;

                ViewBag.Period = period;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.TotalCost = totalCost;
                ViewBag.NetProfit = totalRevenue - totalCost;
                ViewBag.ProfitMargin = totalRevenue > 0 ? ((totalRevenue - totalCost) / totalRevenue) * 100 : 0;
                ViewBag.LastUpdated = DateTime.Now;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        public ActionResult DishProfitAnalysis(string period = "month")
        {
            try
            {
                DateTime startDate, endDate;
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
                    default:
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1);
                        break;
                }

                var totalRevenue = db.Bill
                    .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var totalProfit = totalRevenue * 0.4m;
                var averageProfitMargin = 40m;

                ViewBag.Period = period;
                ViewBag.LastUpdated = DateTime.Now;
                ViewBag.TotalProfit = totalProfit;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.AverageProfitMargin = averageProfitMargin;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public JsonResult GetActiveShifts()
        {
            try
            {
                var activeShifts = db.CashierShift
                    .Include(s => s.Employee)
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.StartTime)
                    .ToList();

                var shiftsData = new List<object>();

                foreach (var shift in activeShifts)
                {
                    var orders = db.Order.Where(o => o.ShiftId == shift.Id).ToList();
                    var revenue = 0m;
                    var lastOrderTime = "Chưa có đơn";

                    if (orders.Any())
                    {
                        foreach (var order in orders)
                        {
                            var bills = db.Bill.Where(b => b.OrderId == order.Id && b.Status == "Paid").ToList();
                            revenue += bills.Sum(b => (decimal?)b.FinalAmount) ?? 0;
                        }
                        lastOrderTime = orders.Max(o => o.OrderTime).ToString("HH:mm");
                    }

                    shiftsData.Add(new
                    {
                        ShiftId = shift.Id,
                        CashierName = shift.Employee?.FullName ?? "N/A",
                        StartTime = shift.StartTime.ToString("HH:mm"),
                        Duration = $"{(int)(DateTime.Now - shift.StartTime).TotalHours}h {(DateTime.Now - shift.StartTime).Minutes % 60}m",
                        OrdersCount = orders.Count,
                        Revenue = revenue,
                        LastOrderTime = lastOrderTime
                    });
                }

                return Json(new { success = true, shifts = shiftsData }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public async Task<JsonResult> DeleteShift(int shiftId)
        {
            try
            {
                var shift = await db.CashierShift.FirstOrDefaultAsync(s => s.Id == shiftId);

                if (shift == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc." });
                }

                if (shift.Status == "Active")
                {
                    return Json(new { success = false, message = "Không thể xóa ca đang hoạt động." });
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var orders = db.Order.Where(o => o.ShiftId == shiftId).ToList();
                        foreach (var order in orders)
                        {
                            var bills = db.Bill.Where(b => b.OrderId == order.Id).ToList();
                            db.Bill.RemoveRange(bills);

                            var orderDetails = db.OrderDetail.Where(od => od.OrderId == order.Id).ToList();
                            db.OrderDetail.RemoveRange(orderDetails);

                            db.Order.Remove(order);
                        }

                        db.CashierShift.Remove(shift);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        return Json(new { success = true });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Lỗi khi xóa dữ liệu: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GetShiftPerformanceComparison(DateTime compareDate)
        {
            try
            {
                var startDate = compareDate.Date;
                var endDate = startDate.AddDays(1);

                var shifts = db.CashierShift
                    .Include(s => s.Employee)
                    .Where(s => s.StartTime >= startDate && s.StartTime < endDate)
                    .ToList();

                var comparison = new List<object>();

                foreach (var shift in shifts)
                {
                    var orders = db.Order.Where(o => o.ShiftId == shift.Id).ToList();
                    var revenue = 0m;
                    foreach (var order in orders)
                    {
                        revenue += db.Bill.Where(b => b.OrderId == order.Id && b.Status == "Paid").Sum(b => (decimal?)b.FinalAmount) ?? 0;
                    }

                    comparison.Add(new
                    {
                        ShiftName = GetShiftDisplayName(shift.StartTime),
                        CashierName = shift.Employee?.FullName ?? "N/A",
                        Revenue = revenue,
                        OrdersCount = orders.Count,
                        AverageOrderValue = orders.Count > 0 ? revenue / orders.Count : 0,
                        Duration = shift.EndTime.HasValue ?
                            (int)(shift.EndTime.Value - shift.StartTime).TotalMinutes :
                            (int)(DateTime.Now - shift.StartTime).TotalMinutes
                    });
                }

                return Json(new { success = true, data = comparison });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SendCashierAlert(int shiftId, string alertType, string message)
        {
            try
            {
                var shift = db.CashierShift.Include(s => s.Employee).FirstOrDefault(s => s.Id == shiftId);
                if (shift == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc!" });
                }

                var alertData = new
                {
                    ShiftId = shiftId,
                    CashierName = shift.Employee?.FullName,
                    AlertType = alertType,
                    Message = message,
                    Timestamp = DateTime.Now,
                    Status = "Sent"
                };

                TempData[$"Alert_{shiftId}"] = alertData;

                return Json(new { success = true, message = "Đã gửi thông báo đến ca làm việc!", data = alertData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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

        private string GetCurrentPeriodText(string period)
        {
            if (period?.ToLower() == "today") return "Hôm nay";
            if (period?.ToLower() == "week") return "Tuần này";
            if (period?.ToLower() == "month") return "Tháng này";
            if (period?.ToLower() == "year") return "Năm này";
            return "Hiện tại";
        }

        private string GetPreviousPeriodText(string period)
        {
            if (period?.ToLower() == "today") return "Hôm qua";
            if (period?.ToLower() == "week") return "Tuần trước";
            if (period?.ToLower() == "month") return "Tháng trước";
            if (period?.ToLower() == "year") return "Năm trước";
            return "Kỳ trước";
        }

        private string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ";
        }

        private string GetShiftDisplayName(DateTime startTime)
        {
            var hour = startTime.Hour;
            if (hour >= 6 && hour < 14) return "Ca Sáng";
            if (hour >= 14 && hour < 22) return "Ca Chiều";
            return "Ca Tối";
        }

        [HttpPost]
        public JsonResult GetRevenueAnalyticsData(string period)
        {
            try
            {
                DateTime startDate, endDate;
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

                var result = new
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

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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

        [HttpPost]
        public JsonResult GetDishProfitAnalysisData(string period)
        {
            try
            {
                // 1. Xác định khoảng thời gian (Hiện tại & Kỳ trước)
                DateTime startDate, endDate;
                DateTime prevStartDate, prevEndDate;

                switch (period?.ToLower())
                {
                    case "week":
                        int diff = (7 + (DateTime.Today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        startDate = DateTime.Today.AddDays(-1 * diff);
                        endDate = startDate.AddDays(7); // Hết chủ nhật
                                                        // Kỳ trước: Tuần trước
                        prevStartDate = startDate.AddDays(-7);
                        prevEndDate = startDate;
                        break;
                    case "month":
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1);
                        // Kỳ trước: Tháng trước
                        prevStartDate = startDate.AddMonths(-1);
                        prevEndDate = startDate;
                        break;
                    case "quarter":
                        var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                        startDate = new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                        endDate = startDate.AddMonths(3);
                        // Kỳ trước: Quý trước
                        prevStartDate = startDate.AddMonths(-3);
                        prevEndDate = startDate;
                        break;
                    case "today":
                    default:
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(1);
                        // Kỳ trước: Hôm qua
                        prevStartDate = DateTime.Today.AddDays(-1);
                        prevEndDate = DateTime.Today;
                        break;
                }

                // 2. Lấy dữ liệu thô (Raw Data)
                var currentDetails = GetOrderDetailsInRange(startDate, endDate);
                var prevDetails = GetOrderDetailsInRange(prevStartDate, prevEndDate);

                // 3. Tính toán chi tiết lợi nhuận (Gọi hàm xử lý chung)
                var currentDishProfits = ProcessDishProfits(currentDetails);
                var prevDishProfits = ProcessDishProfits(prevDetails); // Chỉ cần để tính tổng so sánh

                // 4. Tính toán so sánh (Growth Comparison)
                var currentTotalRevenue = currentDishProfits.Sum(x => x.TotalRevenue);
                var currentTotalProfit = currentDishProfits.Sum(x => x.TotalProfit);

                var prevTotalRevenue = prevDishProfits.Sum(x => x.TotalRevenue);
                var prevTotalProfit = prevDishProfits.Sum(x => x.TotalProfit);

                var profitComp = new RevenueComparisonData
                {
                    CurrentPeriod = currentTotalProfit,
                    PreviousPeriod = prevTotalProfit,
                    GrowthAmount = currentTotalProfit - prevTotalProfit,
                    GrowthPercentage = prevTotalProfit != 0 ? ((currentTotalProfit - prevTotalProfit) / Math.Abs(prevTotalProfit)) * 100 : (currentTotalProfit > 0 ? 100 : 0)
                };

                var revenueComp = new RevenueComparisonData
                {
                    CurrentPeriod = currentTotalRevenue,
                    PreviousPeriod = prevTotalRevenue,
                    GrowthAmount = currentTotalRevenue - prevTotalRevenue,
                    GrowthPercentage = prevTotalRevenue != 0 ? ((currentTotalRevenue - prevTotalRevenue) / Math.Abs(prevTotalRevenue)) * 100 : (currentTotalRevenue > 0 ? 100 : 0)
                };

                // 5. Tạo Summary Object
                var summary = new ProfitSummary
                {
                    TotalRevenue = currentTotalRevenue,
                    TotalProfit = currentTotalProfit,
                    TotalDishes = currentDishProfits.Count,
                    AverageProfitMargin = currentTotalRevenue > 0 ? (double)((currentTotalProfit / currentTotalRevenue) * 100) : 0,
                    BestDish = currentDishProfits.OrderByDescending(d => d.TotalProfit).FirstOrDefault(),
                    ProfitComparison = profitComp,
                    RevenueComparison = revenueComp
                };

                // 6. Tính toán Category Profits
                var categoryProfits = currentDishProfits
                    .GroupBy(d => d.Category)
                    .Select(g => new CategoryProfitData
                    {
                        CategoryName = g.Key,
                        TotalRevenue = g.Sum(d => d.TotalRevenue),
                        TotalCost = g.Sum(d => d.TotalCost),
                        TotalProfit = g.Sum(d => d.TotalProfit),
                        ProfitMargin = g.Sum(d => d.TotalRevenue) > 0 ? (double)(g.Sum(d => d.TotalProfit) / g.Sum(d => d.TotalRevenue) * 100) : 0,
                        DishCount = g.Count(),
                        TotalSold = g.Sum(d => d.SoldQuantity)
                    })
                    .OrderByDescending(c => c.TotalProfit)
                    .ToList();

                // 7. Tính toán Biểu đồ xu hướng (Trend Chart)
                // Group theo ngày từ dữ liệu thô currentDetails
                var trends = currentDetails
                    .GroupBy(od => od.Order.OrderTime.Date)
                    .Select(g => {
                        var rev = g.Sum(od => od.Quantity * od.PriceAtTime);
                        // Lưu ý: Tính cost cho trend ở đây là ước tính nhanh để tối ưu hiệu năng
                        // Nếu muốn chính xác tuyệt đối từng nguyên liệu thì phải lặp lại logic tính cost, khá nặng.
                        // Ở đây tôi dùng tỷ lệ cost trung bình từ danh sách chi tiết món đã tính ở trên.
                        var cost = rev * (currentTotalRevenue > 0 ? (currentDishProfits.Sum(x => x.TotalCost) / currentTotalRevenue) : 0.6m);

                        return new ProfitTrendData
                        {
                            Label = g.Key.ToString("dd/MM"),
                            Revenue = rev,
                            Cost = cost,
                            Profit = rev - cost
                        };
                    })
                    .OrderBy(t => t.Label)
                    .ToList();

                // 8. Đóng gói kết quả
                var model = new DishProfitAnalysisViewModel
                {
                    Summary = summary,
                    DishProfits = currentDishProfits.OrderByDescending(d => d.TotalProfit).ToList(),
                    CategoryProfits = categoryProfits,
                    ProfitTrends = trends,
                    DateRange = $"{startDate:dd/MM/yyyy} - {endDate.AddDays(-1):dd/MM/yyyy}"
                };

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi server: " + ex.Message });
            }
        }

        // --- CÁC HÀM HỖ TRỢ (PRIVATE METHODS) ---

        private List<OrderDetail> GetOrderDetailsInRange(DateTime start, DateTime end)
        {
            // Lấy dữ liệu thô từ DB
            return db.OrderDetail
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                // .Include(od => od.Order.Bill) // Tùy chỉnh theo EF của bạn
                .Where(od => od.Order.OrderTime >= start
                          && od.Order.OrderTime < end
                          && od.Order.Bill.Any(b => b.Status == "Paid"))
                .ToList();
        }

        private List<DishProfitData> ProcessDishProfits(List<OrderDetail> details)
        {
            if (details == null || !details.Any()) return new List<DishProfitData>();

            // Pre-fetch tất cả ingredients một lần để tránh query trong vòng lặp (N+1 problem optimization)
            // Lấy danh sách MenuItemId có trong order
            var menuItemIds = details.Select(od => od.MenuItemId).Distinct().ToList();

            // Lấy tất cả công thức của các món này
            var allIngredients = db.MenuItemIngredient
                .Include(mi => mi.Ingredient)
                .Where(mi => menuItemIds.Contains(mi.MenuItemId))
                .ToList();

            return details
                .GroupBy(od => new
                {
                    od.MenuItemId,
                    Name = od.MenuItem.Name,
                    Category = od.MenuItem.Category,
                    Price = od.MenuItem.Price
                })
                .Select(g =>
                {
                    var totalQuantity = g.Sum(od => od.Quantity);
                    var totalRevenue = g.Sum(od => od.Quantity * od.PriceAtTime);
                    decimal totalCost = 0;

                    // Tìm công thức cho món này từ list đã pre-fetch
                    var dishIngredients = allIngredients.Where(mi => mi.MenuItemId == g.Key.MenuItemId).ToList();

                    if (dishIngredients.Any())
                    {
                        // Tính cost dựa trên nguyên liệu
                        var unitCost = dishIngredients.Sum(mi => mi.RequiredQuantity * mi.Ingredient.EstimatedCost);
                        totalCost = unitCost * totalQuantity;
                    }
                    else
                    {
                        // Fallback: 60% doanh thu nếu không có công thức
                        totalCost = totalRevenue * 0.6m;
                    }

                    var totalProfit = totalRevenue - totalCost;

                    return new DishProfitData
                    {
                        DishName = g.Key.Name ?? "Món không tên",
                        Category = g.Key.Category ?? "Khác",
                        SellingPrice = g.Key.Price,
                        CostPrice = totalQuantity > 0 ? totalCost / totalQuantity : 0,
                        UnitProfit = totalQuantity > 0 ? totalProfit / totalQuantity : 0,
                        ProfitMargin = totalRevenue > 0 ? (double)(totalProfit / totalRevenue * 100) : 0,
                        SoldQuantity = totalQuantity,
                        TotalRevenue = totalRevenue,
                        TotalCost = totalCost,
                        TotalProfit = totalProfit
                    };
                }).ToList();
        }

        [HttpPost]
        public JsonResult CompareRevenueReports(string period)
        {
            try
            {
                DateTime startDate, endDate;
                switch (period?.ToLower())
                {
                    case "week":
                        startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        endDate = startDate.AddDays(7);
                        break;
                    case "quarter":
                        var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                        startDate = new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                        endDate = startDate.AddMonths(3);
                        break;
                    default:
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1);
                        break;
                }

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

                var comparison = new
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

                return Json(new { success = true, data = comparison });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Advanced Analytics Endpoints

        /// <summary>
        /// Advanced Dashboard View
        /// </summary>
        public ActionResult AdvancedDashboard(string period = "today")
        {
            ViewBag.Period = period;
            return View();
        }

        /// <summary>
        /// API: Lấy dữ liệu dashboard nâng cao
        /// </summary>
        [HttpPost]
        public JsonResult GetAdvancedDashboardData(string period = "today")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var data = analyticsService.GetDashboardData(period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy dữ liệu biểu đồ doanh thu
        /// </summary>
        [HttpPost]
        public JsonResult GetRevenueChartData(string period = "today")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var data = analyticsService.GetRevenueChart(start, end, period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy dữ liệu heatmap giờ cao điểm
        /// </summary>
        [HttpPost]
        public JsonResult GetPeakHoursHeatmap(string period = "week")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var data = analyticsService.GetPeakHoursHeatmap(start, end);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy top performers
        /// </summary>
        [HttpPost]
        public JsonResult GetTopPerformers(string period = "month", string type = "dish", int limit = 10)
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);

                List<TopPerformer> data;
                if (type == "employee")
                {
                    data = analyticsService.GetTopCashiers(start, end, limit);
                }
                else
                {
                    data = analyticsService.GetTopPerformers(start, end, limit);
                }

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy dự đoán doanh thu
        /// </summary>
        [HttpPost]
        public JsonResult GetRevenuePrediction(string period = "week")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var data = analyticsService.GetPredictions(period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Menu Engineering View
        /// </summary>
        public ActionResult MenuEngineering(string period = "month")
        {
            ViewBag.Period = period;
            return View();
        }

        /// <summary>
        /// API: Lấy dữ liệu Menu Engineering
        /// </summary>
        [HttpPost]
        public JsonResult GetMenuEngineeringData(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var data = analyticsService.GetMenuEngineering(start, end);

                // Thống kê theo classification
                var summary = data.GroupBy(d => d.Classification)
                    .Select(g => new
                    {
                        Classification = g.Key,
                        Count = g.Count(),
                        TotalRevenue = g.Sum(d => d.SoldQuantity * d.Price),
                        TotalProfit = g.Sum(d => d.Profit)
                    })
                    .ToList();

                return Json(new { success = true, data = data, summary = summary });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy dữ liệu ABC Analysis
        /// </summary>
        [HttpPost]
        public JsonResult GetABCAnalysis(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var data = analyticsService.GetABCAnalysis(start, end);

                // Summary
                var summary = new
                {
                    ClassA = new
                    {
                        Count = data.Count(d => d.Classification == "A"),
                        Percentage = data.Any() ? (decimal)data.Count(d => d.Classification == "A") / data.Count * 100 : 0,
                        Revenue = data.Where(d => d.Classification == "A").Sum(d => d.Revenue)
                    },
                    ClassB = new
                    {
                        Count = data.Count(d => d.Classification == "B"),
                        Percentage = data.Any() ? (decimal)data.Count(d => d.Classification == "B") / data.Count * 100 : 0,
                        Revenue = data.Where(d => d.Classification == "B").Sum(d => d.Revenue)
                    },
                    ClassC = new
                    {
                        Count = data.Count(d => d.Classification == "C"),
                        Percentage = data.Any() ? (decimal)data.Count(d => d.Classification == "C") / data.Count * 100 : 0,
                        Revenue = data.Where(d => d.Classification == "C").Sum(d => d.Revenue)
                    }
                };

                return Json(new { success = true, data = data, summary = summary });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Phát hiện bất thường
        /// </summary>
        [HttpPost]
        public JsonResult GetAnomalies(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var data = analyticsService.DetectAnomalies(start, end);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API: Lấy danh sách cảnh báo
        /// </summary>
        [HttpGet]
        public JsonResult GetAlerts()
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var data = analyticsService.GetAlerts();
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// API: Lấy KPIs
        /// </summary>
        [HttpPost]
        public JsonResult GetKPIs(string period = "today")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                var (start, end) = GetDateRangeForPeriod(period);
                var (prevStart, prevEnd) = GetPreviousDateRangeForPeriod(start, period);
                var data = analyticsService.GetKPIs(start, end, prevStart, prevEnd);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// KPI Tracking View
        /// </summary>
        public ActionResult KPITracking(string period = "today")
        {
            ViewBag.Period = period;
            return View();
        }

        /// <summary>
        /// Predictive Analytics View
        /// </summary>
        public ActionResult PredictiveAnalytics(string period = "week")
        {
            ViewBag.Period = period;
            return View();
        }

        /// <summary>
        /// API: So sánh hiệu suất giữa các kỳ
        /// </summary>
        [HttpPost]
        public JsonResult GetPerformanceComparison(string period1 = "month", string period2 = "prev_month")
        {
            try
            {
                var (start1, end1) = GetDateRangeForPeriod(period1);
                var (start2, end2) = period2 == "prev_month" 
                    ? (start1.AddMonths(-1), start1)
                    : GetDateRangeForPeriod(period2);

                // Period 1 data
                var revenue1 = db.Bill
                    .Where(b => b.BillDate >= start1 && b.BillDate < end1 && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var orders1 = db.Order.Count(o => o.OrderTime >= start1 && o.OrderTime < end1);

                // Period 2 data
                var revenue2 = db.Bill
                    .Where(b => b.BillDate >= start2 && b.BillDate < end2 && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var orders2 = db.Order.Count(o => o.OrderTime >= start2 && o.OrderTime < end2);

                var comparison = new ComparativeAnalysis
                {
                    Period1Name = GetPeriodName(period1),
                    Period2Name = GetPeriodName(period2),
                    Metrics = new List<ComparisonMetric>
                    {
                        new ComparisonMetric
                        {
                            Name = "Doanh thu",
                            Period1Value = revenue1,
                            Period2Value = revenue2,
                            Change = revenue1 - revenue2,
                            ChangePercentage = revenue2 > 0 ? (revenue1 - revenue2) / revenue2 * 100 : 0,
                            Trend = revenue1 > revenue2 ? "up" : revenue1 < revenue2 ? "down" : "stable"
                        },
                        new ComparisonMetric
                        {
                            Name = "Đơn hàng",
                            Period1Value = orders1,
                            Period2Value = orders2,
                            Change = orders1 - orders2,
                            ChangePercentage = orders2 > 0 ? (decimal)(orders1 - orders2) / orders2 * 100 : 0,
                            Trend = orders1 > orders2 ? "up" : orders1 < orders2 ? "down" : "stable"
                        },
                        new ComparisonMetric
                        {
                            Name = "TB/Đơn",
                            Period1Value = orders1 > 0 ? revenue1 / orders1 : 0,
                            Period2Value = orders2 > 0 ? revenue2 / orders2 : 0,
                            Change = (orders1 > 0 ? revenue1 / orders1 : 0) - (orders2 > 0 ? revenue2 / orders2 : 0),
                            Trend = "stable"
                        }
                    }
                };

                return Json(new { success = true, data = comparison });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Helper Methods for Advanced Analytics

        private (DateTime start, DateTime end) GetDateRangeForPeriod(string period)
        {
            var today = DateTime.Today;

            switch (period?.ToLower())
            {
                case "week":
                    var weekStart = today.AddDays(-(int)today.DayOfWeek);
                    return (weekStart, weekStart.AddDays(7));
                case "month":
                    return (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1));
                case "quarter":
                    var quarter = (today.Month - 1) / 3 + 1;
                    var quarterStart = new DateTime(today.Year, (quarter - 1) * 3 + 1, 1);
                    return (quarterStart, quarterStart.AddMonths(3));
                case "year":
                    return (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1));
                default: // today
                    return (today, today.AddDays(1));
            }
        }

        private (DateTime start, DateTime end) GetPreviousDateRangeForPeriod(DateTime currentStart, string period)
        {
            switch (period?.ToLower())
            {
                case "week":
                    return (currentStart.AddDays(-7), currentStart);
                case "month":
                    return (currentStart.AddMonths(-1), currentStart);
                case "quarter":
                    return (currentStart.AddMonths(-3), currentStart);
                case "year":
                    return (currentStart.AddYears(-1), currentStart);
                default:
                    return (currentStart.AddDays(-1), currentStart);
            }
        }

        private string GetPeriodName(string period)
        {
            switch (period?.ToLower())
            {
                case "today": return "Hôm nay";
                case "week": return "Tuần này";
                case "month": return "Tháng này";
                case "quarter": return "Quý này";
                case "year": return "Năm này";
                case "prev_month": return "Tháng trước";
                case "prev_week": return "Tuần trước";
                default: return period;
            }
        }

        #endregion
    }
}