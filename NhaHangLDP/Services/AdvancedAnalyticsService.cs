using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service phân tích nâng cao - Miễn phí, không cần ML.NET
    /// Bao gồm: Prediction, Anomaly Detection, Menu Engineering, ABC Analysis
    /// </summary>
    public class AdvancedAnalyticsService
    {
        private readonly MyDbContext _db;

        public AdvancedAnalyticsService()
        {
            _db = new MyDbContext();
        }

        public AdvancedAnalyticsService(MyDbContext db)
        {
            _db = db;
        }

        #region Dashboard & Summary

        /// <summary>
        /// Lấy dữ liệu dashboard tổng hợp
        /// </summary>
        public AdvancedDashboardViewModel GetDashboardData(string period = "today")
        {
            var (startDate, endDate) = GetDateRange(period);
            var (prevStart, prevEnd) = GetPreviousDateRange(startDate, period);

            var dashboard = new AdvancedDashboardViewModel
            {
                Period = period,
                DateRange = $"{startDate:dd/MM/yyyy} - {endDate.AddDays(-1):dd/MM/yyyy}",
                LastUpdated = DateTime.Now
            };

            // Summary
            dashboard.Summary = GetSummary(startDate, endDate, prevStart, prevEnd);

            // Revenue Chart
            dashboard.RevenueChart = GetRevenueChart(startDate, endDate, period);

            // Category Breakdown
            dashboard.CategoryBreakdown = GetCategoryBreakdown(startDate, endDate);

            // Top Performers
            dashboard.TopPerformers = GetTopPerformers(startDate, endDate);

            // Alerts
            dashboard.Alerts = GetAlerts();

            // KPIs
            dashboard.KPIs = GetKPIs(startDate, endDate, prevStart, prevEnd);

            // Predictions
            dashboard.Predictions = GetPredictions(period);

            return dashboard;
        }

        private DashboardSummary GetSummary(DateTime start, DateTime end, DateTime prevStart, DateTime prevEnd)
        {
            var summary = new DashboardSummary();

            // Current period
            var currentBills = _db.Bills
                .Where(b => b.BillDate >= start && b.BillDate < end && b.Status == "Paid")
                .ToList();

            var currentOrders = _db.Orders
                .Where(o => o.OrderTime >= start && o.OrderTime < end)
                .ToList();

            summary.TotalRevenue = currentBills.Sum(b => b.FinalAmount);
            summary.TotalOrders = currentOrders.Count;
            summary.AvgOrderValue = summary.TotalOrders > 0 ? summary.TotalRevenue / summary.TotalOrders : 0;

            // Calculate profit (estimate 40% margin)
            summary.NetProfit = summary.TotalRevenue * 0.4m;
            summary.ProfitMargin = summary.TotalRevenue > 0 ? (summary.NetProfit / summary.TotalRevenue) * 100 : 0;

            // Previous period for growth calculation
            var prevBills = _db.Bills
                .Where(b => b.BillDate >= prevStart && b.BillDate < prevEnd && b.Status == "Paid")
                .ToList();

            var prevRevenue = prevBills.Sum(b => b.FinalAmount);
            var prevOrders = _db.Orders.Count(o => o.OrderTime >= prevStart && o.OrderTime < prevEnd);

            // Growth rates
            summary.RevenueGrowth = CalculateGrowth(summary.TotalRevenue, prevRevenue);
            summary.OrdersGrowth = prevOrders > 0 ? (int)(((double)summary.TotalOrders - prevOrders) / prevOrders * 100) : 0;

            var prevAvgOrder = prevOrders > 0 ? prevRevenue / prevOrders : 0;
            summary.AvgOrderGrowth = CalculateGrowth(summary.AvgOrderValue, prevAvgOrder);

            // Table utilization
            var totalTables = _db.RestaurantTables.Count();
            var occupiedTables = _db.RestaurantTables.Count(t => t.Status == "Occupied");
            summary.TableUtilization = totalTables > 0 ? (decimal)occupiedTables / totalTables * 100 : 0;

            // Table turnover
            var totalSessions = _db.Orders.Count(o => o.OrderTime >= start && o.OrderTime < end);
            var hoursOpen = Math.Max(1, (end - start).TotalHours);
            summary.TableTurnover = totalTables > 0 ? (int)(totalSessions / (totalTables * (hoursOpen / 8))) : 0;

            // RevPASH (Revenue per Available Seat Hour)
            var totalSeats = _db.RestaurantTables.Sum(t => (int?)t.Capacity) ?? 0;
            summary.RevPASH = totalSeats > 0 && hoursOpen > 0 ? summary.TotalRevenue / (totalSeats * (decimal)hoursOpen) : 0;

            // Customer counts
            summary.TotalCustomers = _db.Bookings
                .Where(b => b.BookingDateTime >= start && b.BookingDateTime < end)
                .Select(b => b.CustomerPhone)
                .Distinct()
                .Count();

            summary.NewCustomers = summary.TotalCustomers; // Simplified - all customers in period

            return summary;
        }

        #endregion

        #region Charts & Visualizations

        /// <summary>
        /// Lấy dữ liệu biểu đồ doanh thu
        /// </summary>
        public List<RevenueDataPoint> GetRevenueChart(DateTime start, DateTime end, string period)
        {
            var dataPoints = new List<RevenueDataPoint>();
            var bills = _db.Bills
                .Where(b => b.BillDate >= start && b.BillDate < end && b.Status == "Paid")
                .ToList();

            switch (period?.ToLower())
            {
                case "today":
                    // Hourly data
                    for (int hour = 6; hour <= 23; hour++)
                    {
                        var hourStart = start.Date.AddHours(hour);
                        var hourEnd = hourStart.AddHours(1);
                        var hourBills = bills.Where(b => b.BillDate >= hourStart && b.BillDate < hourEnd).ToList();

                        dataPoints.Add(new RevenueDataPoint
                        {
                            Label = $"{hour}:00",
                            Revenue = hourBills.Sum(b => b.FinalAmount),
                            Cost = hourBills.Sum(b => b.FinalAmount) * 0.6m,
                            Profit = hourBills.Sum(b => b.FinalAmount) * 0.4m,
                            OrderCount = hourBills.Count,
                            Date = hourStart
                        });
                    }
                    break;

                case "week":
                    // Daily data
                    for (int day = 0; day < 7; day++)
                    {
                        var dayStart = start.AddDays(day);
                        var dayEnd = dayStart.AddDays(1);
                        var dayBills = bills.Where(b => b.BillDate >= dayStart && b.BillDate < dayEnd).ToList();

                        dataPoints.Add(new RevenueDataPoint
                        {
                            Label = dayStart.ToString("ddd dd/MM"),
                            Revenue = dayBills.Sum(b => b.FinalAmount),
                            Cost = dayBills.Sum(b => b.FinalAmount) * 0.6m,
                            Profit = dayBills.Sum(b => b.FinalAmount) * 0.4m,
                            OrderCount = dayBills.Count,
                            Date = dayStart
                        });
                    }
                    break;

                case "month":
                    // Weekly data
                    var currentWeek = start;
                    int weekNum = 1;
                    while (currentWeek < end)
                    {
                        var weekEnd = currentWeek.AddDays(7);
                        if (weekEnd > end) weekEnd = end;
                        var weekBills = bills.Where(b => b.BillDate >= currentWeek && b.BillDate < weekEnd).ToList();

                        dataPoints.Add(new RevenueDataPoint
                        {
                            Label = $"Tuần {weekNum}",
                            Revenue = weekBills.Sum(b => b.FinalAmount),
                            Cost = weekBills.Sum(b => b.FinalAmount) * 0.6m,
                            Profit = weekBills.Sum(b => b.FinalAmount) * 0.4m,
                            OrderCount = weekBills.Count,
                            Date = currentWeek
                        });

                        currentWeek = weekEnd;
                        weekNum++;
                    }
                    break;

                case "quarter":
                case "year":
                    // Monthly data
                    for (int month = 1; month <= 12; month++)
                    {
                        var monthStart = new DateTime(start.Year, month, 1);
                        var monthEnd = monthStart.AddMonths(1);
                        var monthBills = bills.Where(b => b.BillDate >= monthStart && b.BillDate < monthEnd).ToList();

                        dataPoints.Add(new RevenueDataPoint
                        {
                            Label = $"T{month}",
                            Revenue = monthBills.Sum(b => b.FinalAmount),
                            Cost = monthBills.Sum(b => b.FinalAmount) * 0.6m,
                            Profit = monthBills.Sum(b => b.FinalAmount) * 0.4m,
                            OrderCount = monthBills.Count,
                            Date = monthStart
                        });
                    }
                    break;
            }

            return dataPoints;
        }

        /// <summary>
        /// Lấy phân bố doanh thu theo danh mục
        /// </summary>
        public List<CategoryRevenue> GetCategoryBreakdown(DateTime start, DateTime end)
        {
            var orderDetails = _db.OrderDetails
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.OrderTime >= start && od.Order.OrderTime < end)
                .Where(od => od.Order.Bills.Any(b => b.Status == "Paid"))
                .ToList();

            var totalRevenue = orderDetails.Sum(od => od.Quantity * od.PriceAtTime);
            var colors = new[] { "#3b82f6", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6", "#06b6d4" };

            return orderDetails
                .GroupBy(od => od.MenuItem?.Category ?? "Khác")
                .Select((g, index) => new CategoryRevenue
                {
                    Category = g.Key,
                    Revenue = g.Sum(od => od.Quantity * od.PriceAtTime),
                    Percentage = totalRevenue > 0 ? g.Sum(od => od.Quantity * od.PriceAtTime) / totalRevenue * 100 : 0,
                    ItemCount = g.Count(),
                    Color = colors[index % colors.Length]
                })
                .OrderByDescending(c => c.Revenue)
                .ToList();
        }

        /// <summary>
        /// Lấy heatmap giờ cao điểm
        /// </summary>
        public List<HeatmapData> GetPeakHoursHeatmap(DateTime start, DateTime end)
        {
            var orders = _db.Orders
                .Where(o => o.OrderTime >= start && o.OrderTime < end)
                .ToList();

            var heatmapData = new List<HeatmapData>();
            var dayNames = new[] { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

            for (int day = 0; day < 7; day++)
            {
                for (int hour = 6; hour <= 23; hour++)
                {
                    var dayOrders = orders
                        .Where(o => (int)o.OrderTime.DayOfWeek == day && o.OrderTime.Hour == hour)
                        .ToList();

                    var revenue = 0m;
                    foreach (var order in dayOrders)
                    {
                        revenue += _db.Bills
                            .Where(b => b.OrderId == order.Id && b.Status == "Paid")
                            .Sum(b => (decimal?)b.FinalAmount) ?? 0;
                    }

                    heatmapData.Add(new HeatmapData
                    {
                        DayOfWeek = dayNames[day],
                        Hour = hour,
                        Value = dayOrders.Count,
                        Revenue = revenue
                    });
                }
            }

            return heatmapData;
        }

        #endregion

        #region Top Performers

        /// <summary>
        /// Lấy top performers (món, nhân viên)
        /// </summary>
        public List<TopPerformer> GetTopPerformers(DateTime start, DateTime end, int limit = 10)
        {
            var performers = new List<TopPerformer>();

            // Top dishes by revenue
            var topDishes = _db.OrderDetails
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.OrderTime >= start && od.Order.OrderTime < end)
                .Where(od => od.Order.Bills.Any(b => b.Status == "Paid"))
                .GroupBy(od => new { od.MenuItemId, od.MenuItem.Name, od.MenuItem.ImageUrl })
                .Select(g => new
                {
                    Name = g.Key.Name,
                    ImageUrl = g.Key.ImageUrl,
                    Revenue = g.Sum(od => od.Quantity * od.PriceAtTime),
                    Count = g.Sum(od => od.Quantity)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(limit)
                .ToList();

            int rank = 1;
            foreach (var dish in topDishes)
            {
                performers.Add(new TopPerformer
                {
                    Rank = rank++,
                    Name = dish.Name,
                    Type = "dish",
                    Value = dish.Revenue,
                    Count = dish.Count,
                    ImageUrl = dish.ImageUrl
                });
            }

            return performers;
        }

        /// <summary>
        /// Lấy top nhân viên thu ngân
        /// </summary>
        public List<TopPerformer> GetTopCashiers(DateTime start, DateTime end, int limit = 5)
        {
            var shifts = _db.CashierShifts
                .Include(s => s.Cashier)
                .Where(s => s.StartTime >= start && s.StartTime < end)
                .ToList();

            var cashierPerformance = new Dictionary<string, (decimal Revenue, int Orders)>();

            foreach (var shift in shifts)
            {
                var orders = _db.Orders.Where(o => o.ShiftId == shift.Id).ToList();
                var revenue = 0m;
                foreach (var order in orders)
                {
                    revenue += _db.Bills
                        .Where(b => b.OrderId == order.Id && b.Status == "Paid")
                        .Sum(b => (decimal?)b.FinalAmount) ?? 0;
                }

                var name = shift.Cashier?.FullName ?? "N/A";
                if (cashierPerformance.ContainsKey(name))
                {
                    cashierPerformance[name] = (
                        cashierPerformance[name].Revenue + revenue,
                        cashierPerformance[name].Orders + orders.Count
                    );
                }
                else
                {
                    cashierPerformance[name] = (revenue, orders.Count);
                }
            }

            return cashierPerformance
                .OrderByDescending(c => c.Value.Revenue)
                .Take(limit)
                .Select((c, index) => new TopPerformer
                {
                    Rank = index + 1,
                    Name = c.Key,
                    Type = "employee",
                    Value = c.Value.Revenue,
                    Count = c.Value.Orders
                })
                .ToList();
        }

        #endregion

        #region Predictions (Simple Linear Regression)

        /// <summary>
        /// Dự đoán doanh thu - Simple Linear Regression
        /// </summary>
        public PredictionResult GetPredictions(string period)
        {
            var result = new PredictionResult { Period = period };

            // Lấy dữ liệu lịch sử 30 ngày
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-30);

            var dailyRevenue = _db.Bills
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .GroupBy(b => b.BillDate.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(b => b.FinalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            if (dailyRevenue.Count < 7)
            {
                result.Confidence = 0;
                return result;
            }

            // Simple Linear Regression
            var n = dailyRevenue.Count;
            var xValues = Enumerable.Range(1, n).Select(x => (double)x).ToArray();
            var yValues = dailyRevenue.Select(d => (double)d.Revenue).ToArray();

            var xMean = xValues.Average();
            var yMean = yValues.Average();

            var slope = xValues.Zip(yValues, (x, y) => (x - xMean) * (y - yMean)).Sum() /
                       xValues.Sum(x => Math.Pow(x - xMean, 2));
            var intercept = yMean - slope * xMean;

            // Predict next day/week
            int daysToPredict = period == "week" ? 7 : period == "month" ? 30 : 1;
            var predictions = new List<PredictionDataPoint>();

            // Add historical data (last 7 days)
            var lastDays = dailyRevenue.Skip(Math.Max(0, dailyRevenue.Count - 7)).ToList();
            foreach (var day in lastDays)
            {
                predictions.Add(new PredictionDataPoint
                {
                    Label = day.Date.ToString("dd/MM"),
                    Value = day.Revenue,
                    IsActual = true
                });
            }

            // Add predictions
            decimal totalPredicted = 0;
            for (int i = 1; i <= daysToPredict; i++)
            {
                var predicted = (decimal)(intercept + slope * (n + i));
                predicted = Math.Max(0, predicted); // Không âm
                totalPredicted += predicted;

                predictions.Add(new PredictionDataPoint
                {
                    Label = DateTime.Today.AddDays(i).ToString("dd/MM"),
                    Value = predicted,
                    IsActual = false
                });
            }

            result.PredictedRevenue = totalPredicted;
            result.PredictedProfit = totalPredicted * 0.4m;
            
            // Calculate average order value
            var avgRevenue = dailyRevenue.Count > 0 ? dailyRevenue.Average(d => d.Revenue) : 0m;
            var totalOrdersInPeriod = _db.Orders.Count(o => o.OrderTime >= startDate && o.OrderTime < endDate);
            var avgOrderValue = totalOrdersInPeriod > 0 && avgRevenue > 0 
                ? avgRevenue / (totalOrdersInPeriod / (decimal)n) 
                : 100000m;
            result.PredictedOrders = avgOrderValue > 0 ? (int)(totalPredicted / avgOrderValue) : 0;

            // Calculate R-squared for confidence
            var ssRes = xValues.Zip(yValues, (x, y) => Math.Pow(y - (intercept + slope * x), 2)).Sum();
            var ssTot = yValues.Sum(y => Math.Pow(y - yMean, 2));
            result.Confidence = ssTot > 0 ? (decimal)(1 - ssRes / ssTot) * 100 : 0;
            result.Confidence = Math.Max(0, Math.Min(100, result.Confidence));

            result.TrendPrediction = predictions;

            return result;
        }

        #endregion

        #region Menu Engineering

        /// <summary>
        /// Menu Engineering Analysis - BCG Matrix style
        /// </summary>
        public List<MenuEngineeringItem> GetMenuEngineering(DateTime start, DateTime end)
        {
            var orderDetails = _db.OrderDetails
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.OrderTime >= start && od.Order.OrderTime < end)
                .Where(od => od.Order.Bills.Any(b => b.Status == "Paid"))
                .ToList();

            var menuItems = orderDetails
                .GroupBy(od => new
                {
                    od.MenuItemId,
                    od.MenuItem.Name,
                    od.MenuItem.Category,
                    od.MenuItem.Price,
                    od.MenuItem.ImageUrl
                })
                .Select(g =>
                {
                    var soldQty = g.Sum(od => od.Quantity);
                    var revenue = g.Sum(od => od.Quantity * od.PriceAtTime);
                    
                    // Calculate cost from ingredients
                    var ingredients = _db.MenuItemIngredients
                        .Include(mi => mi.Ingredient)
                        .Where(mi => mi.MenuItemId == g.Key.MenuItemId)
                        .ToList();
                    
                    var unitCost = ingredients.Any() 
                        ? ingredients.Sum(mi => mi.RequiredQuantity * mi.Ingredient.EstimatedCost)
                        : g.Key.Price * 0.6m;

                    var totalCost = unitCost * soldQty;
                    var profit = revenue - totalCost;

                    return new MenuEngineeringItem
                    {
                        DishId = g.Key.MenuItemId,
                        DishName = g.Key.Name,
                        Category = g.Key.Category,
                        Price = g.Key.Price,
                        Cost = unitCost,
                        SoldQuantity = soldQty,
                        Profit = profit,
                        ProfitMargin = revenue > 0 ? profit / revenue * 100 : 0,
                        ImageUrl = g.Key.ImageUrl
                    };
                })
                .ToList();

            var totalSold = menuItems.Sum(m => m.SoldQuantity);
            var avgPopularity = totalSold > 0 ? (decimal)totalSold / menuItems.Count : 0;
            var avgProfitMargin = menuItems.Any() ? menuItems.Average(m => m.ProfitMargin) : 0;

            foreach (var item in menuItems)
            {
                item.Popularity = totalSold > 0 ? (decimal)item.SoldQuantity / totalSold * 100 : 0;

                bool highPopularity = item.SoldQuantity >= (int)avgPopularity;
                bool highProfit = item.ProfitMargin >= avgProfitMargin;

                if (highPopularity && highProfit)
                {
                    item.Classification = "Star";
                    item.Recommendation = "Giữ nguyên, đẩy mạnh marketing";
                }
                else if (highPopularity && !highProfit)
                {
                    item.Classification = "Plow Horse";
                    item.Recommendation = "Tăng giá hoặc giảm chi phí nguyên liệu";
                }
                else if (!highPopularity && highProfit)
                {
                    item.Classification = "Puzzle";
                    item.Recommendation = "Đẩy mạnh quảng bá, đào tạo nhân viên giới thiệu";
                }
                else
                {
                    item.Classification = "Dog";
                    item.Recommendation = "Xem xét loại bỏ hoặc làm mới công thức";
                }
            }

            return menuItems.OrderByDescending(m => m.Profit).ToList();
        }

        #endregion

        #region ABC Analysis

        /// <summary>
        /// ABC Analysis - Pareto (80/20 rule)
        /// </summary>
        public List<ABCAnalysisItem> GetABCAnalysis(DateTime start, DateTime end)
        {
            var items = _db.OrderDetails
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.OrderTime >= start && od.Order.OrderTime < end)
                .Where(od => od.Order.Bills.Any(b => b.Status == "Paid"))
                .GroupBy(od => new { od.MenuItemId, od.MenuItem.Name })
                .Select(g => new
                {
                    Id = g.Key.MenuItemId,
                    Name = g.Key.Name,
                    Revenue = g.Sum(od => od.Quantity * od.PriceAtTime)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            var totalRevenue = items.Sum(i => i.Revenue);
            var result = new List<ABCAnalysisItem>();
            decimal cumulative = 0;

            foreach (var item in items)
            {
                cumulative += item.Revenue;
                var cumulativePercent = totalRevenue > 0 ? cumulative / totalRevenue * 100 : 0;

                string classification;
                if (cumulativePercent <= 80)
                    classification = "A";
                else if (cumulativePercent <= 95)
                    classification = "B";
                else
                    classification = "C";

                result.Add(new ABCAnalysisItem
                {
                    ItemId = item.Id,
                    Name = item.Name,
                    Revenue = item.Revenue,
                    CumulativeRevenue = cumulative,
                    CumulativePercentage = cumulativePercent,
                    Classification = classification
                });
            }

            return result;
        }

        #endregion

        #region Anomaly Detection

        /// <summary>
        /// Phát hiện bất thường trong doanh thu
        /// Sử dụng Z-Score method
        /// </summary>
        public List<AnomalyResult> DetectAnomalies(DateTime start, DateTime end)
        {
            var anomalies = new List<AnomalyResult>();

            var dailyRevenue = _db.Bills
                .Where(b => b.BillDate >= start && b.BillDate < end && b.Status == "Paid")
                .GroupBy(b => b.BillDate.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(b => b.FinalAmount) })
                .ToList();

            if (dailyRevenue.Count < 7) return anomalies;

            var revenues = dailyRevenue.Select(d => (double)d.Revenue).ToArray();
            var mean = revenues.Average();
            var stdDev = Math.Sqrt(revenues.Average(v => Math.Pow(v - mean, 2)));

            if (stdDev == 0) return anomalies;

            foreach (var day in dailyRevenue)
            {
                var zScore = Math.Abs(((double)day.Revenue - mean) / stdDev);

                if (zScore > 2)
                {
                    string severity = zScore > 3 ? "high" : zScore > 2.5 ? "medium" : "low";
                    string cause = day.Revenue > (decimal)mean 
                        ? "Doanh thu cao bất thường - có thể do sự kiện đặc biệt" 
                        : "Doanh thu thấp bất thường - cần xem xét nguyên nhân";

                    anomalies.Add(new AnomalyResult
                    {
                        Date = day.Date,
                        Metric = "Doanh thu",
                        ActualValue = day.Revenue,
                        ExpectedValue = (decimal)mean,
                        Deviation = (decimal)zScore,
                        Severity = severity,
                        PossibleCause = cause
                    });
                }
            }

            return anomalies;
        }

        #endregion

        #region Alerts

        /// <summary>
        /// Lấy các cảnh báo quan trọng
        /// </summary>
        public List<AlertItem> GetAlerts()
        {
            var alerts = new List<AlertItem>();
            var today = DateTime.Today;

            // 1. Low stock alerts - use AvailableStock and LowStockThreshold
            var lowStockItems = _db.Ingredients
                .Where(i => i.LowStockThreshold.HasValue && i.AvailableStock <= i.LowStockThreshold.Value)
                .ToList();

            foreach (var item in lowStockItems.Take(5))
            {
                alerts.Add(new AlertItem
                {
                    Id = $"low_stock_{item.Id}",
                    Type = item.AvailableStock <= 0 ? "danger" : "warning",
                    Title = item.AvailableStock <= 0 ? "Hết hàng" : "Tồn kho thấp",
                    Message = $"{item.Name}: Còn {item.AvailableStock} {item.Unit}",
                    Action = "Nhập kho ngay",
                    ActionUrl = "/Management/StockInbound",
                    Timestamp = DateTime.Now
                });
            }

            // 2. Revenue target alert
            var todayRevenue = _db.Bills
                .Where(b => b.BillDate >= today && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var hourOfDay = DateTime.Now.Hour;
            var expectedRevenue = 2000000m * (hourOfDay - 6) / 16; // Assume 16 hours operation

            if (hourOfDay > 12 && todayRevenue < expectedRevenue * 0.7m)
            {
                alerts.Add(new AlertItem
                {
                    Id = "revenue_alert",
                    Type = "warning",
                    Title = "Doanh thu dưới kỳ vọng",
                    Message = $"Đạt {todayRevenue:N0}đ / Kỳ vọng {expectedRevenue:N0}đ",
                    Timestamp = DateTime.Now
                });
            }

            // 3. Pending bookings
            var pendingBookings = _db.Bookings
                .Count(b => b.Status == "Pending" && b.BookingDateTime >= today);

            if (pendingBookings > 0)
            {
                alerts.Add(new AlertItem
                {
                    Id = "pending_bookings",
                    Type = "info",
                    Title = "Đặt bàn chờ xác nhận",
                    Message = $"Có {pendingBookings} yêu cầu đặt bàn mới",
                    Action = "Xem chi tiết",
                    Timestamp = DateTime.Now
                });
            }

            return alerts.OrderBy(a => a.Type == "danger" ? 0 : a.Type == "warning" ? 1 : 2).ToList();
        }

        #endregion

        #region KPIs

        /// <summary>
        /// Lấy các KPI metrics
        /// </summary>
        public List<KPIMetric> GetKPIs(DateTime start, DateTime end, DateTime prevStart, DateTime prevEnd)
        {
            var kpis = new List<KPIMetric>();

            // 1. Revenue
            var currentRevenue = _db.Bills
                .Where(b => b.BillDate >= start && b.BillDate < end && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var prevRevenue = _db.Bills
                .Where(b => b.BillDate >= prevStart && b.BillDate < prevEnd && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            var revenueTarget = 5000000m; // Daily target

            kpis.Add(new KPIMetric
            {
                Id = "revenue",
                Name = "Doanh thu",
                CurrentValue = currentRevenue,
                TargetValue = revenueTarget,
                PreviousValue = prevRevenue,
                Unit = "VNĐ",
                Format = "currency",
                Achievement = revenueTarget > 0 ? currentRevenue / revenueTarget * 100 : 0,
                Trend = currentRevenue > prevRevenue ? "up" : currentRevenue < prevRevenue ? "down" : "stable",
                Color = "#3b82f6"
            });

            // 2. Orders
            var currentOrders = _db.Orders.Count(o => o.OrderTime >= start && o.OrderTime < end);
            var prevOrders = _db.Orders.Count(o => o.OrderTime >= prevStart && o.OrderTime < prevEnd);
            var ordersTarget = 50;

            kpis.Add(new KPIMetric
            {
                Id = "orders",
                Name = "Đơn hàng",
                CurrentValue = currentOrders,
                TargetValue = ordersTarget,
                PreviousValue = prevOrders,
                Unit = "đơn",
                Format = "number",
                Achievement = ordersTarget > 0 ? (decimal)currentOrders / ordersTarget * 100 : 0,
                Trend = currentOrders > prevOrders ? "up" : currentOrders < prevOrders ? "down" : "stable",
                Color = "#22c55e"
            });

            // 3. Average Order Value
            var avgOrder = currentOrders > 0 ? currentRevenue / currentOrders : 0;
            var prevAvgOrder = prevOrders > 0 ? prevRevenue / prevOrders : 0;

            kpis.Add(new KPIMetric
            {
                Id = "avg_order",
                Name = "TB/Đơn",
                CurrentValue = avgOrder,
                TargetValue = 150000,
                PreviousValue = prevAvgOrder,
                Unit = "VNĐ",
                Format = "currency",
                Achievement = 150000 > 0 ? avgOrder / 150000 * 100 : 0,
                Trend = avgOrder > prevAvgOrder ? "up" : avgOrder < prevAvgOrder ? "down" : "stable",
                Color = "#f59e0b"
            });

            // 4. Table Utilization
            var totalTables = _db.RestaurantTables.Count();
            var occupiedTables = _db.RestaurantTables.Count(t => t.Status == "Occupied");
            var utilization = totalTables > 0 ? (decimal)occupiedTables / totalTables * 100 : 0;

            kpis.Add(new KPIMetric
            {
                Id = "table_util",
                Name = "Sử dụng bàn",
                CurrentValue = utilization,
                TargetValue = 80,
                PreviousValue = 0,
                Unit = "%",
                Format = "percentage",
                Achievement = utilization,
                Trend = "stable",
                Color = "#8b5cf6"
            });

            return kpis;
        }

        #endregion

        #region Helpers

        private (DateTime start, DateTime end) GetDateRange(string period)
        {
            DateTime start, end;
            var today = DateTime.Today;

            switch (period?.ToLower())
            {
                case "week":
                    start = today.AddDays(-(int)today.DayOfWeek);
                    end = start.AddDays(7);
                    break;
                case "month":
                    start = new DateTime(today.Year, today.Month, 1);
                    end = start.AddMonths(1);
                    break;
                case "quarter":
                    var quarter = (today.Month - 1) / 3 + 1;
                    start = new DateTime(today.Year, (quarter - 1) * 3 + 1, 1);
                    end = start.AddMonths(3);
                    break;
                case "year":
                    start = new DateTime(today.Year, 1, 1);
                    end = start.AddYears(1);
                    break;
                default: // today
                    start = today;
                    end = today.AddDays(1);
                    break;
            }

            return (start, end);
        }

        private (DateTime start, DateTime end) GetPreviousDateRange(DateTime currentStart, string period)
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

        private decimal CalculateGrowth(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return Math.Round((current - previous) / previous * 100, 2);
        }

        #endregion
    }
}
