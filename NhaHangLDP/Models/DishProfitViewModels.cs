using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NhaHangLDP.Models.ViewModels
{
    public class DishProfitAnalysisViewModel
    {
        public ProfitSummaryViewModel Summary { get; set; }
        public List<DishProfitItemViewModel> DishProfits { get; set; }
        public List<ProfitTrendViewModel> ProfitTrends { get; set; }
        public List<CategoryProfitViewModel> CategoryProfits { get; set; }
        public string DateRange { get; set; }

        public DishProfitAnalysisViewModel()
        {
            Summary = new ProfitSummaryViewModel();
            DishProfits = new List<DishProfitItemViewModel>();
            ProfitTrends = new List<ProfitTrendViewModel>();
            CategoryProfits = new List<CategoryProfitViewModel>();
        }
    }

    public class ProfitSummaryViewModel
    {
        public decimal TotalProfit { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageProfitMargin { get; set; }
        public int TotalDishes { get; set; }
        public DishProfitItemViewModel BestDish { get; set; }
    }

    public class DishProfitItemViewModel
    {
        public string DishName { get; set; }
        public string Category { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal UnitProfit { get; set; }
        public double ProfitMargin { get; set; }
        public int SoldQuantity { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class ProfitTrendViewModel
    {
        public string Label { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
    }

    public class CategoryProfitViewModel
    {
        public string CategoryName { get; set; }
        public int DishCount { get; set; }
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public double ProfitMargin { get; set; }
    }
}