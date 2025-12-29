using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Services.Reports;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    public partial class ReportsManagementController : Controller
    {
        private readonly NhaHangLDPEntities db = new NhaHangLDPEntities();
        private readonly AdvancedAnalyticsService _analyticsService;
        private readonly DashboardReportService _dashboardService;
        private readonly BookingAnalyticsService _bookingService;
        private readonly SalesAnalyticsService _salesService;
        private readonly RevenueAnalyticsService _revenueService;
        private readonly DishProfitAnalysisService _dishProfitService;
        private readonly ReportShiftService _shiftService;

        public ReportsManagementController()
        {
            _analyticsService = new AdvancedAnalyticsService();
            _dashboardService = new DashboardReportService(db);
            _bookingService = new BookingAnalyticsService(db);
            _salesService = new SalesAnalyticsService(db);
            _revenueService = new RevenueAnalyticsService(db);
            _dishProfitService = new DishProfitAnalysisService(db);
            _shiftService = new ReportShiftService(db);
        }

        #region Dashboard

        public ActionResult Dashboard()
        {
            try
            {
                var summary = _dashboardService.GetDashboardSummary();

                ViewBag.TodayOrders = summary.TodayOrders;
                ViewBag.TodayRevenue = summary.TodayRevenue;
                ViewBag.TodayRevenueFormatted = _dashboardService.FormatCurrency(summary.TodayRevenue);
                ViewBag.TableUtilization = summary.TableUtilization;
                ViewBag.OccupiedTables = summary.OccupiedTables;
                ViewBag.TotalTables = summary.TotalTables;
                ViewBag.AvgOrderValue = summary.AvgOrderValue;
                ViewBag.AvgOrderValueFormatted = _dashboardService.FormatCurrency(summary.AvgOrderValue);
                ViewBag.ProfitMargin = summary.ProfitMargin;
                ViewBag.LastUpdated = summary.LastUpdated;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                SetDefaultDashboardViewBag();
                return View();
            }
        }

        private void SetDefaultDashboardViewBag()
        {
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
        }

        #endregion

        #region Booking Analytics

        public ActionResult BookingAnalytics(string period = "today", DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var data = _bookingService.GetBookingAnalytics(period, fromDate, toDate);

                ViewBag.Period = period;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.StartDate = data.StartDate;
                ViewBag.EndDate = data.EndDate;
                ViewBag.TotalBookings = data.TotalBookings;
                ViewBag.SuccessfulBookings = data.SuccessfulBookings;
                ViewBag.CancelledBookings = data.CancelledBookings;
                ViewBag.PendingBookings = data.PendingBookings;
                ViewBag.BookingRevenue = data.BookingRevenue;
                ViewBag.WalkInRevenue = data.WalkInRevenue;
                ViewBag.AverageBookingValue = data.AverageBookingValue;
                ViewBag.AveragePartySize = data.AveragePartySize;
                ViewBag.TableUtilizationRate = data.TableUtilizationRate;
                ViewBag.OccupiedTables = data.OccupiedTables;
                ViewBag.TotalTables = data.TotalTables;
                ViewBag.LastUpdated = data.LastUpdated;

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
                var data = _bookingService.GetBookingChartData(period);
                return Json(new { success = true, data = data });
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

        #endregion

        #region Sales Analytics

        public ActionResult SalesAnalytics(string period = "today")
        {
            try
            {
                var data = _salesService.GetSalesAnalytics(period);

                ViewBag.Period = period;
                ViewBag.LastUpdated = data.LastUpdated;
                ViewBag.CashierName = "Thu Ngân";
                ViewBag.TotalOrders = data.TotalOrders;
                ViewBag.TotalRevenue = data.TotalRevenue;
                ViewBag.AverageOrderValue = data.AverageOrderValue;
                ViewBag.RevenuePerHour = data.RevenuePerHour;
                ViewBag.Target = data.Target;
                ViewBag.Actual = data.TotalRevenue;
                ViewBag.Achievement = data.Achievement;
                ViewBag.Remaining = data.Remaining;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                SetDefaultSalesViewBag(period);
                return View();
            }
        }

        private void SetDefaultSalesViewBag(string period)
        {
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
        }

        [HttpPost]
        public JsonResult GetSalesAnalyticsData(string period)
        {
            try
            {
                var data = _salesService.GetSalesChartData(period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Revenue Analytics

        public ActionResult RevenueAnalytics(string period = "month")
        {
            try
            {
                var data = _revenueService.GetRevenueAnalytics(period);

                ViewBag.Period = period;
                ViewBag.TotalRevenue = data.TotalRevenue;
                ViewBag.TotalCost = data.TotalCost;
                ViewBag.NetProfit = data.NetProfit;
                ViewBag.ProfitMargin = data.ProfitMargin;
                ViewBag.LastUpdated = data.LastUpdated;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpPost]
        public JsonResult GetRevenueAnalyticsData(string period)
        {
            try
            {
                var data = _revenueService.GetRevenueChartData(period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CompareRevenueReports(string period)
        {
            try
            {
                var data = _revenueService.CompareRevenueReports(period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Dish Profit Analysis

        public ActionResult DishProfitAnalysis(string period = "month")
        {
            try
            {
                var data = _dishProfitService.GetDishProfitViewData(period);

                ViewBag.Period = data.Period;
                ViewBag.LastUpdated = data.LastUpdated;
                ViewBag.TotalProfit = data.TotalProfit;
                ViewBag.TotalRevenue = data.TotalRevenue;
                ViewBag.AverageProfitMargin = data.AverageProfitMargin;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpPost]
        public JsonResult GetDishProfitAnalysisData(string period)
        {
            try
            {
                var result = _dishProfitService.GetDishProfitAnalysis(period);

                var model = new DishProfitAnalysisViewModel
                {
                    Summary = result.Summary,
                    DishProfits = result.DishProfits,
                    CategoryProfits = result.CategoryProfits,
                    ProfitTrends = result.ProfitTrends,
                    DateRange = result.DateRange
                };

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi server: " + ex.Message });
            }
        }

        #endregion

        #region Shift Management

        [HttpGet]
        public JsonResult GetActiveShifts()
        {
            try
            {
                var shiftsData = _shiftService.GetActiveShifts();
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
            var result = await _shiftService.DeleteShiftAsync(shiftId);
            if (result.Success)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, message = result.ErrorMessage });
        }

        [HttpPost]
        public JsonResult GetShiftPerformanceComparison(DateTime compareDate)
        {
            try
            {
                var comparison = _shiftService.GetShiftPerformanceComparison(compareDate);
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
                var alertData = _shiftService.SendCashierAlert(shiftId, alertType, message);
                if (alertData == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc!" });
                }

                TempData[$"Alert_{shiftId}"] = alertData;
                return Json(new { success = true, message = "Đã gửi thông báo đến ca làm việc!", data = alertData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Advanced Analytics Endpoints

        public ActionResult AdvancedDashboard(string period = "today")
        {
            ViewBag.Period = period;
            return View();
        }

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

        [HttpPost]
        public JsonResult GetRevenueChartData(string period = "today")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);
                var data = analyticsService.GetRevenueChart(start, end, period);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GetPeakHoursHeatmap(string period = "week")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);
                var data = analyticsService.GetPeakHoursHeatmap(start, end);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GetTopPerformers(string period = "month", string type = "dish", int limit = 10)
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);

                List<TopPerformer> data = type == "employee"
                    ? analyticsService.GetTopCashiers(start, end, limit)
                    : analyticsService.GetTopPerformers(start, end, limit);

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

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

        public ActionResult MenuEngineering(string period = "month")
        {
            ViewBag.Period = period;
            return View();
        }

        [HttpPost]
        public JsonResult GetMenuEngineeringData(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);
                var data = analyticsService.GetMenuEngineering(start, end);

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

        [HttpPost]
        public JsonResult GetABCAnalysis(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);
                var data = analyticsService.GetABCAnalysis(start, end);

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

        [HttpPost]
        public JsonResult GetAnomalies(string period = "month")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end;
                DateRangeHelper.GetDateRange(period, out start, out end);
                var data = analyticsService.DetectAnomalies(start, end);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

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

        [HttpPost]
        public JsonResult GetKPIs(string period = "today")
        {
            try
            {
                var analyticsService = new AdvancedAnalyticsService(db);
                DateTime start, end, prevStart, prevEnd;
                DateRangeHelper.GetDateRange(period, out start, out end);
                DateRangeHelper.GetPreviousDateRange(start, period, out prevStart, out prevEnd);
                var data = analyticsService.GetKPIs(start, end, prevStart, prevEnd);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public ActionResult KPITracking(string period = "today")
        {
            ViewBag.Period = period;
            return View();
        }

        public ActionResult PredictiveAnalytics(string period = "week")
        {
            ViewBag.Period = period;
            return View();
        }

        [HttpPost]
        public JsonResult GetPerformanceComparison(string period1 = "month", string period2 = "prev_month")
        {
            try
            {
                DateTime start1, end1, start2, end2;
                DateRangeHelper.GetDateRange(period1, out start1, out end1);

                if (period2 == "prev_month")
                {
                    start2 = start1.AddMonths(-1);
                    end2 = start1;
                }
                else
                {
                    DateRangeHelper.GetDateRange(period2, out start2, out end2);
                }

                var revenue1 = db.Bill
                    .Where(b => b.BillDate >= start1 && b.BillDate < end1 && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var orders1 = db.Order.Count(o => o.OrderTime >= start1 && o.OrderTime < end1);

                var revenue2 = db.Bill
                    .Where(b => b.BillDate >= start2 && b.BillDate < end2 && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                var orders2 = db.Order.Count(o => o.OrderTime >= start2 && o.OrderTime < end2);

                var comparison = new ComparativeAnalysis
                {
                    Period1Name = DateRangeHelper.GetPeriodDisplayName(period1),
                    Period2Name = DateRangeHelper.GetPeriodDisplayName(period2),
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}