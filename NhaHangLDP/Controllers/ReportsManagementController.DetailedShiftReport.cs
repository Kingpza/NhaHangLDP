using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using NhaHangLDP.Models;

namespace NhaHangLDP.Controllers
{
    public partial class ReportsManagementController
    {
        // GET: ReportsManagement/DetailedShiftReport
        public ActionResult DetailedShiftReport(DateTime? shiftDate, string cashierName = "")
        {
            ViewBag.ShiftDate = shiftDate ?? DateTime.Today;
            ViewBag.CashierName = cashierName;
            
            // Lấy danh sách thu ngân để filter
            var cashiers = db.Employee
                .Where(e => e.Role.RoleName == "Cashier" || e.Role.RoleName == "Manager")
                .Select(e => e.FullName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ViewBag.CashierList = cashiers;
            
            return View();
        }

        // GET: ReportsManagement/LiveShiftMonitor - Trang giám sát ca trực tiếp
        public ActionResult LiveShiftMonitor()
        {
            return View();
        }

        #region Detailed Shift Report API - Real Database Implementation

        [HttpPost]
        public JsonResult GetDetailedShiftReportData(DateTime? shiftDate, string cashierName = "")
        {
            try
            {
                var targetDate = shiftDate ?? DateTime.Today;
                var data = GetShiftReportDataFromDatabase(targetDate, cashierName);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private object GetShiftReportDataFromDatabase(DateTime shiftDate, string cashierName)
        {
            var startDate = shiftDate.Date;
            var endDate = startDate.AddDays(1);

            // Query shifts from database
            var shiftsQuery = db.CashierShift
                .Include(s => s.Employee)
                .Include(s => s.Order.Select(o => o.OrderDetail))
                .Include(s => s.Order.Select(o => o.Bill))
                .Include(s => s.Order.Select(o => o.RestaurantTable))
                .Where(s => s.StartTime >= startDate && s.StartTime < endDate);

            // Filter by cashier name if provided
            if (!string.IsNullOrEmpty(cashierName))
            {
                shiftsQuery = shiftsQuery.Where(s => s.Employee.FullName.Contains(cashierName));
            }

            var shifts = shiftsQuery.OrderBy(s => s.StartTime).ToList();

            var shiftData = shifts.Select(shift => new
            {
                ShiftId = shift.Id,
                ShiftName = GetShiftDisplayNameInternal(shift.StartTime),
                StartTime = shift.StartTime.ToString("HH:mm"),
                EndTime = shift.EndTime?.ToString("HH:mm") ?? "Đang làm việc",
                Duration = shift.EndTime.HasValue ? 
                    $"{(int)(shift.EndTime.Value - shift.StartTime).TotalHours}h {(shift.EndTime.Value - shift.StartTime).Minutes % 60}m" :
                    $"{(int)(DateTime.Now - shift.StartTime).TotalHours}h {(DateTime.Now - shift.StartTime).Minutes % 60}m",
                MainCashier = shift.Employee?.FullName ?? "N/A",
                Status = shift.Status,
                StatusDisplay = shift.Status == "Active" ? "Đang hoạt động" : 
                               shift.Status == "Closed" ? "Đã đóng" : shift.Status,

                // Tính toán từ orders thực tế
                TotalOrders = shift.Order.Count,
                CompletedOrders = shift.Order.Count(o => o.Status == "Completed"),
                CancelledOrders = shift.Order.Count(o => o.Status == "Cancelled"),
                PendingOrders = shift.Order.Count(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready"),

                // Doanh thu từ bills đã thanh toán
                TotalRevenue = shift.Order
                    .SelectMany(o => o.Bill.Where(b => b.Status == "Paid"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                // Doanh thu tiền mặt
                CashRevenue = shift.Order
                    .SelectMany(o => o.Bill.Where(b => b.Status == "Paid" && b.PaymentMethod == "cash"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                // Doanh thu thẻ/chuyển khoản
                CardRevenue = shift.Order
                    .SelectMany(o => o.Bill.Where(b => b.Status == "Paid" && b.PaymentMethod != "cash"))
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,

                // Giá trị đơn hàng trung bình
                AverageOrderValue = shift.Order.Any(o => o.Bill.Any(b => b.Status == "Paid")) ?
                    shift.Order.Where(o => o.Bill.Any(b => b.Status == "Paid"))
                        .Average(o => o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount)) : 0,

                // Số lượng bàn phục vụ
                TablesServed = shift.Order.Select(o => o.TableId).Distinct().Count(),

                // Số khách hàng (ước tính từ orders)
                CustomersServed = shift.Order.Sum(o => o.OrderDetail.Sum(od => od.Quantity)) / 2, // Ước tính

                // Tiền mặt đầu ca và cuối ca
                InitialCash = shift.InitialCash,
                FinalCash = shift.FinalCash,

                // Top món bán chạy trong ca
                TopDishes = shift.Order
                    .SelectMany(o => o.OrderDetail)
                    .Where(od => od.Order.Bill.Any(b => b.Status == "Paid"))
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

                // Phân tích theo giờ trong ca
                HourlyBreakdown = GetHourlyBreakdown(shift),

                // Phương thức thanh toán
                PaymentMethods = shift.Order
                    .SelectMany(o => o.Bill.Where(b => b.Status == "Paid"))
                    .GroupBy(b => b.PaymentMethod)
                    .Select(g => new
                    {
                        Method = GetPaymentMethodDisplayInternal(g.Key),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.FinalAmount)
                    })
                    .ToList()
            }).ToList();

            // Tính tổng kết
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
            var orders = shift.Order.Where(o => o.Bill.Any(b => b.Status == "Paid")).ToList();
            
            return orders
                .GroupBy(o => o.OrderTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key + ":00",
                    OrderCount = g.Count(),
                    Revenue = g.SelectMany(o => o.Bill.Where(b => b.Status == "Paid")).Sum(b => b.FinalAmount)
                })
                .OrderBy(x => x.Hour)
                .ToList();
        }

        private string GetShiftDisplayNameInternal(DateTime startTime)
        {
            var hour = startTime.Hour;
            if (hour >= 6 && hour < 14) return "Ca Sáng";
            if (hour >= 14 && hour < 22) return "Ca Chiều";
            return "Ca Tối";
        }

        private string GetPaymentMethodDisplayInternal(string method)
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

        #endregion

        #region Link to Cashier System

        [HttpPost]
        public JsonResult GetShiftCashierLink(int shiftId)
        {
            try
            {
                var shift = db.CashierShift.Include(s => s.Employee).FirstOrDefault(s => s.Id == shiftId);
                if (shift == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc!" });
                }

                var cashierUrl = Url.Action("ShiftDetails", "Cashier", new { shiftId = shiftId });
                
                return Json(new 
                { 
                    success = true, 
                    cashierUrl = cashierUrl,
                    shiftInfo = new
                    {
                        Id = shift.Id,
                        CashierName = shift.Employee?.FullName,
                        StartTime = shift.StartTime.ToString("dd/MM/yyyy HH:mm"),
                        Status = shift.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}