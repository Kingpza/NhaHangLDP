using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class DishProfitAnalysisService
    {
        private readonly NhaHangLDPEntities db;

        public DishProfitAnalysisService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public DishProfitAnalysisResult GetDishProfitAnalysis(string period)
        {
            DateTime startDate, endDate, prevStartDate, prevEndDate;
            GetDateRanges(period, out startDate, out endDate, out prevStartDate, out prevEndDate);

            // Lấy dữ liệu thô
            var currentDetails = GetOrderDetailsInRange(startDate, endDate);
            var prevDetails = GetOrderDetailsInRange(prevStartDate, prevEndDate);

            // Tính toán chi tiết lợi nhuận
            var currentDishProfits = ProcessDishProfits(currentDetails);
            var prevDishProfits = ProcessDishProfits(prevDetails);

            // Tính toán so sánh
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

            // Summary
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

            // Category Profits
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

            // Trend Chart
            var trends = currentDetails
                .GroupBy(od => od.Order.OrderTime.Date)
                .Select(g =>
                {
                    var rev = g.Sum(od => od.Quantity * od.PriceAtTime);
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

            return new DishProfitAnalysisResult
            {
                Summary = summary,
                DishProfits = currentDishProfits.OrderByDescending(d => d.TotalProfit).ToList(),
                CategoryProfits = categoryProfits,
                ProfitTrends = trends,
                DateRange = $"{startDate:dd/MM/yyyy} - {endDate.AddDays(-1):dd/MM/yyyy}"
            };
        }

        public DishProfitViewData GetDishProfitViewData(string period)
        {
            DateTime startDate, endDate, prevStartDate, prevEndDate;
            GetDateRanges(period, out startDate, out endDate, out prevStartDate, out prevEndDate);

            var totalRevenue = db.Bill
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .Sum(b => (decimal?)b.FinalAmount) ?? 0;

            return new DishProfitViewData
            {
                Period = period,
                TotalProfit = totalRevenue * 0.4m,
                TotalRevenue = totalRevenue,
                AverageProfitMargin = 40m,
                LastUpdated = DateTime.Now
            };
        }

        private List<OrderDetail> GetOrderDetailsInRange(DateTime start, DateTime end)
        {
            return db.OrderDetail
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.OrderTime >= start
                          && od.Order.OrderTime < end
                          && od.Order.Bill.Any(b => b.Status == "Paid"))
                .ToList();
        }

        private List<DishProfitData> ProcessDishProfits(List<OrderDetail> details)
        {
            if (details == null || !details.Any()) return new List<DishProfitData>();

            var menuItemIds = details.Select(od => od.MenuItemId).Distinct().ToList();

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

                    var dishIngredients = allIngredients.Where(mi => mi.MenuItemId == g.Key.MenuItemId).ToList();

                    if (dishIngredients.Any())
                    {
                        var unitCost = dishIngredients.Sum(mi => mi.RequiredQuantity * mi.Ingredient.EstimatedCost);
                        totalCost = unitCost * totalQuantity;
                    }
                    else
                    {
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

        private void GetDateRanges(string period, out DateTime startDate, out DateTime endDate, out DateTime prevStartDate, out DateTime prevEndDate)
        {
            switch (period?.ToLower())
            {
                case "week":
                    int diff = (7 + (DateTime.Today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = DateTime.Today.AddDays(-1 * diff);
                    endDate = startDate.AddDays(7);
                    prevStartDate = startDate.AddDays(-7);
                    prevEndDate = startDate;
                    break;
                case "month":
                    startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    endDate = startDate.AddMonths(1);
                    prevStartDate = startDate.AddMonths(-1);
                    prevEndDate = startDate;
                    break;
                case "quarter":
                    var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
                    startDate = new DateTime(DateTime.Today.Year, (currentQuarter - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3);
                    prevStartDate = startDate.AddMonths(-3);
                    prevEndDate = startDate;
                    break;
                default:
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1);
                    prevStartDate = DateTime.Today.AddDays(-1);
                    prevEndDate = DateTime.Today;
                    break;
            }
        }
    }

    public class DishProfitAnalysisResult
    {
        public ProfitSummary Summary { get; set; }
        public List<DishProfitData> DishProfits { get; set; }
        public List<CategoryProfitData> CategoryProfits { get; set; }
        public List<ProfitTrendData> ProfitTrends { get; set; }
        public string DateRange { get; set; }
    }

    public class DishProfitViewData
    {
        public string Period { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageProfitMargin { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
