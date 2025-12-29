using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class ManagementDashboardDataService
    {
        private readonly MyDbContext db;

        public ManagementDashboardDataService(MyDbContext context)
        {
            db = context;
        }

        public ManagementDashboardViewModel GetDashboardData()
        {
            var viewModel = new ManagementDashboardViewModel();
            var today = DateTime.Today;
            var sevenDaysAgo = today.AddDays(-7);

            viewModel.RecentOrders = db.Orders
                .Include(o => o.Table)
                .Include(o => o.Bills)
                .OrderByDescending(o => o.OrderTime)
                .Take(5)
                .ToList();

            var paidBills = db.Bills
                .Where(b => b.BillDate >= sevenDaysAgo && b.Status == "Paid")
                .ToList();

            var revenueData = new List<DailyRevenue>();
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var revenue = paidBills
                    .Where(b => b.BillDate.Date == date)
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                revenueData.Add(new DailyRevenue
                {
                    Date = date.ToString("dd/MM"),
                    Revenue = revenue
                });
            }
            viewModel.RevenueLast7Days = revenueData;

            viewModel.PopularItems = db.OrderDetails
                .Where(od => od.Order.OrderTime >= sevenDaysAgo)
                .GroupBy(od => od.MenuItem.Name)
                .Select(g => new PopularItem
                {
                    ItemName = g.Key,
                    Quantity = g.Sum(od => od.Quantity)
                })
                .OrderByDescending(pi => pi.Quantity)
                .Take(5)
                .ToList();

            return viewModel;
        }

        public DashboardViewModel GetDashboardStats()
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var tomorrow = today.AddDays(1);
                var dayAfterYesterday = yesterday.AddDays(1);

                var todayRevenue = db.Orders
                    .Where(o => o.OrderTime >= today && o.OrderTime < tomorrow && o.Status == "Completed")
                    .SelectMany(o => o.OrderDetails)
                    .Sum(od => (decimal?)od.Quantity * od.PriceAtTime) ?? 0;

                var yesterdayRevenue = db.Orders
                    .Where(o => o.OrderTime >= yesterday && o.OrderTime < dayAfterYesterday && o.Status == "Completed")
                    .SelectMany(o => o.OrderDetails)
                    .Sum(od => (decimal?)od.Quantity * od.PriceAtTime) ?? 0;

                var todayOrders = db.Orders
                    .Count(o => o.OrderTime >= today && o.OrderTime < tomorrow);

                var yesterdayOrders = db.Orders
                    .Count(o => o.OrderTime >= yesterday && o.OrderTime < dayAfterYesterday);

                var todayCustomers = db.Orders
                    .Where(o => o.OrderTime >= today && o.OrderTime < tomorrow && (object)o.TableId != null)
                    .Select(o => o.TableId)
                    .Distinct()
                    .Count();

                var yesterdayCustomers = db.Orders
                    .Where(o => o.OrderTime >= yesterday && o.OrderTime < dayAfterYesterday && (object)o.TableId != null)
                    .Select(o => o.TableId)
                    .Distinct()
                    .Count();

                var occupiedTables = db.RestaurantTables.Count(t => t.Status == "Occupied");
                var totalTables = db.RestaurantTables.Count();

                return new DashboardViewModel
                {
                    TodayRevenue = todayRevenue,
                    RevenueChangePercent = yesterdayRevenue > 0 ? ((todayRevenue - yesterdayRevenue) / yesterdayRevenue * 100) : 0,
                    OrdersChangePercent = yesterdayOrders > 0 ? ((float)(todayOrders - yesterdayOrders) / yesterdayOrders * 100) : 0,
                    CustomersChangePercent = yesterdayCustomers > 0 ? ((float)(todayCustomers - yesterdayCustomers) / yesterdayCustomers * 100) : 0,
                    TodayOrders = todayOrders,
                    TodayCustomers = todayCustomers,
                    OccupiedTables = occupiedTables,
                    TotalTables = totalTables,
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetDashboardStats: {ex.Message}");
                return new DashboardViewModel();
            }
        }

        public InventoryReportViewModel GetInventoryReportData()
        {
            try
            {
                var ingredients = db.Ingredients.ToList();
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);

                var totalIngredients = ingredients.Count;
                var totalValue = ingredients.Sum(i => i.AvailableStock * i.EstimatedCost);
                var lowStockCount = ingredients.Count(i => i.LowStockThreshold.HasValue && i.AvailableStock <= i.LowStockThreshold.Value);
                var outOfStockCount = ingredients.Count(i => i.AvailableStock <= 0);

                var monthlyInbound = db.StockInbounds
                    .Where(s => s.InboundDate >= startOfMonth)
                    .Sum(s => (decimal?)s.TotalCost) ?? 0;

                var monthlyDamage = db.DamagedStocks
                    .Where(d => d.DamageDate >= startOfMonth)
                    .Join(db.Ingredients, d => d.IngredientId, i => i.Id, (d, i) => new { d.Quantity, i.EstimatedCost })
                    .Sum(x => (decimal?)(x.Quantity * x.EstimatedCost)) ?? 0;

                var highValueIngredients = ingredients
                    .Select(i => new InventoryItemReportModel
                    {
                        IngredientName = i.Name,
                        Unit = i.Unit,
                        AvailableStock = i.AvailableStock,
                        EstimatedCost = i.EstimatedCost,
                        TotalValue = i.AvailableStock * i.EstimatedCost,
                        LowStockThreshold = i.LowStockThreshold ?? 0,
                        StockStatus = GetStockStatus(i)
                    })
                    .OrderByDescending(i => i.TotalValue)
                    .Take(10)
                    .ToList();

                var alertIngredients = ingredients
                    .Where(i => i.LowStockThreshold.HasValue && i.AvailableStock <= i.LowStockThreshold.Value)
                    .Select(i => new InventoryItemReportModel
                    {
                        IngredientName = i.Name,
                        Unit = i.Unit,
                        AvailableStock = i.AvailableStock,
                        EstimatedCost = i.EstimatedCost,
                        TotalValue = i.AvailableStock * i.EstimatedCost,
                        LowStockThreshold = i.LowStockThreshold ?? 0,
                        StockStatus = GetStockStatus(i)
                    })
                    .OrderBy(i => i.AvailableStock)
                    .ToList();

                var recentInbounds = db.StockInbounds
                    .Include(s => s.Cashier)
                    .Include(s => s.Supplier)
                    .OrderByDescending(s => s.InboundDate)
                    .Take(10)
                    .ToList()
                    .Select(s => new InboundActivityModel
                    {
                        InboundDate = s.InboundDate,
                        EmployeeName = s.Cashier?.FullName ?? "N/A",
                        SupplierName = s.Supplier?.Name ?? "Không có",
                        TotalCost = s.TotalCost,
                        ItemCount = s.StockInboundDetails?.Count ?? 0
                    })
                    .ToList();

                var recentDamages = db.DamagedStocks
                    .Include(d => d.Ingredient)
                    .Include(d => d.ReportedByEmployee)
                    .OrderByDescending(d => d.DamageDate)
                    .Take(10)
                    .ToList()
                    .Select(d => new DamageReportModel
                    {
                        DamageDate = d.DamageDate,
                        IngredientName = d.Ingredient?.Name ?? "N/A",
                        Quantity = d.Quantity,
                        Unit = d.Ingredient?.Unit ?? "",
                        Reason = d.Reason ?? "Không có lý do",
                        ReportedBy = d.ReportedByEmployee?.FullName ?? "N/A",
                        EstimatedLoss = d.Quantity * (d.Ingredient?.EstimatedCost ?? 0)
                    })
                    .ToList();

                return new InventoryReportViewModel
                {
                    TotalIngredients = totalIngredients,
                    TotalInventoryValue = totalValue,
                    LowStockCount = lowStockCount,
                    OutOfStockCount = outOfStockCount,
                    MonthlyInboundValue = monthlyInbound,
                    MonthlyDamageValue = monthlyDamage,
                    HighValueIngredients = highValueIngredients,
                    AlertIngredients = alertIngredients,
                    RecentInbounds = recentInbounds,
                    RecentDamages = recentDamages,
                    ReportGeneratedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetInventoryReportData: {ex.Message}");
                return new InventoryReportViewModel();
            }
        }

        private string GetStockStatus(Ingredient ingredient)
        {
            if (ingredient.AvailableStock <= 0)
                return "out-of-stock";

            if (ingredient.LowStockThreshold.HasValue && ingredient.AvailableStock <= ingredient.LowStockThreshold.Value)
                return "low-stock";

            return "in-stock";
        }
    }
}
