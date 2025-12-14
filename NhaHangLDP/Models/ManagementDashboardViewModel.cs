using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    // Lớp chính chứa toàn bộ dữ liệu cho Dashboard
    public class ManagementDashboardViewModel
    {
        public List<Order> RecentOrders { get; set; }
        public List<DailyRevenue> RevenueLast7Days { get; set; }
        public List<PopularItem> PopularItems { get; set; }

        public ManagementDashboardViewModel()
        {
            RecentOrders = new List<Order>();
            RevenueLast7Days = new List<DailyRevenue>();
            PopularItems = new List<PopularItem>();
        }
    }

    // Lớp phụ trợ cho biểu đồ doanh thu
    public class DailyRevenue
    {
        public string Date { get; set; }
        public decimal Revenue { get; set; }
    }

    // Lớp phụ trợ cho biểu đồ món ăn
    public class PopularItem
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
    }
}