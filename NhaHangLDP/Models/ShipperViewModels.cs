using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho Dashboard của Shipper
    /// </summary>
    public class ShipperDashboardViewModel
    {
        public Shipper Shipper { get; set; }
        public ShipperActiveOrderViewModel ActiveOrder { get; set; }
        public int TodayOrderCount { get; set; }
        public int TodayCompletedCount { get; set; }
        public decimal TodayEarnings { get; set; }
    }

    /// <summary>
    /// ViewModel cho đơn hàng đang xử lý của Shipper
    /// </summary>
    public class ShipperActiveOrderViewModel
    {
        public int AssignmentId { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public DateTime AssignedTime { get; set; }
        public DateTime? EstimatedArrival { get; set; }
        public List<OrderItemSummary> Items { get; set; }
    }

    /// <summary>
    /// ViewModel cho danh sách đơn hàng của Shipper
    /// </summary>
    public class ShipperOrderViewModel
    {
        public int AssignmentId { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string District { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal ShipperEarning { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public DateTime AssignedTime { get; set; }
        public DateTime? EstimatedArrival { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public int? CustomerRating { get; set; }
    }

    /// <summary>
    /// ViewModel chi tiết đơn hàng cho Shipper
    /// </summary>
    public class ShipperOrderDetailViewModel
    {
        public DeliveryAssignment Assignment { get; set; }
        public CustomerOrder Order { get; set; }
        public List<CustomerOrderDetail> OrderItems { get; set; }
    }

    /// <summary>
    /// ViewModel thống kê thu nhập Shipper
    /// </summary>
    public class ShipperEarningsViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal AverageEarningPerOrder { get; set; }
        public List<DailyEarning> DailyEarnings { get; set; }
    }

    /// <summary>
    /// Thu nhập theo ngày
    /// </summary>
    public class DailyEarning
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Earnings { get; set; }
    }
}
