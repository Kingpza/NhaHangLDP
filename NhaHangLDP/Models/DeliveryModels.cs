using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    #region ViewModels

    /// <summary>
    /// ViewModel cho Dashboard giao hàng
    /// </summary>
    public class DeliveryDashboardViewModel
    {
        public int PendingOrders { get; set; }
        public int DeliveringOrders { get; set; }
        public int CompletedOrdersToday { get; set; }
        public int FailedOrdersToday { get; set; }
        public int AvailableShippers { get; set; }
        public int BusyShippers { get; set; }
        public int TotalShippers { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal TodayDeliveryFees { get; set; }
        public decimal AverageDeliveryTime { get; set; }
        public List<RecentDeliveryViewModel> RecentDeliveries { get; set; }
        public List<ShipperStatusViewModel> ShipperStatuses { get; set; }
    }

    /// <summary>
    /// ViewModel cho đơn giao gần đây
    /// </summary>
    public class RecentDeliveryViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string ShipperName { get; set; }
        public string Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? EstimatedDelivery { get; set; }
        public decimal TotalAmount { get; set; }
    }

    /// <summary>
    /// ViewModel cho trạng thái shipper
    /// </summary>
    public class ShipperStatusViewModel
    {
        public int ShipperId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
        public string VehicleType { get; set; }
        public decimal Rating { get; set; }
        public int TodayDeliveries { get; set; }
        public int? CurrentOrderId { get; set; }
        public string CurrentOrderCode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    /// <summary>
    /// ViewModel cho danh sách đơn giao
    /// </summary>
    public class DeliveryOrderListViewModel
    {
        public List<DeliveryOrderItemViewModel> Orders { get; set; }
        public string SelectedStatus { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>
    /// ViewModel cho item đơn giao
    /// </summary>
    public class DeliveryOrderItemViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public string Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? EstimatedDeliveryTime { get; set; }
        public string Note { get; set; }

        // Shipper info
        public int? ShipperId { get; set; }
        public string ShipperName { get; set; }
        public string ShipperPhone { get; set; }
        public string DeliveryStatus { get; set; }

        // Order items
        public List<OrderItemSummary> Items { get; set; }
    }

    public class OrderItemSummary
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string SpecialInstructions { get; set; }
    }

    /// <summary>
    /// ViewModel cho chi tiết đơn giao
    /// </summary>
    public class DeliveryOrderDetailViewModel
    {
        public CustomerOrder Order { get; set; }
        public DeliveryAssignment Assignment { get; set; }
        public Shipper Shipper { get; set; }
        public List<CustomerOrderDetail> OrderItems { get; set; }
        public List<OrderTrackingStep> TrackingSteps { get; set; }
    }

    /// <summary>
    /// ViewModel cho quản lý shipper
    /// </summary>
    public class ShipperManagementViewModel
    {
        public List<Shipper> Shippers { get; set; }
        public int TotalActive { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalBusy { get; set; }
        public int TotalOffline { get; set; }
    }

    /// <summary>
    /// ViewModel cho tạo/sửa shipper
    /// </summary>
    public class ShipperFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100)]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(20)]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(50)]
        [Display(Name = "Loại xe")]
        public string VehicleType { get; set; }

        [StringLength(20)]
        [Display(Name = "Biển số xe")]
        public string LicensePlate { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// ViewModel cho theo dõi realtime
    /// </summary>
    public class DeliveryTrackingViewModel
    {
        public List<ActiveDeliveryViewModel> ActiveDeliveries { get; set; }
        public List<ShipperLocationViewModel> ShipperLocations { get; set; }
    }

    public class ActiveDeliveryViewModel
    {
        public int AssignmentId { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public int ShipperId { get; set; }
        public string ShipperName { get; set; }
        public string ShipperPhone { get; set; }
        public string Status { get; set; }
        public DateTime? EstimatedArrival { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? DestLatitude { get; set; }
        public decimal? DestLongitude { get; set; }
    }

    public class ShipperLocationViewModel
    {
        public int ShipperId { get; set; }
        public string ShipperName { get; set; }
        public string Status { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public DateTime? LastUpdate { get; set; }
    }

    /// <summary>
    /// ViewModel cho thống kê giao hàng
    /// </summary>
    public class DeliveryStatisticsViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int FailedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalDeliveryFees { get; set; }
        public decimal AverageOrderValue { get; set; }
        public double AverageDeliveryTime { get; set; } // minutes
        public List<DailyDeliveryStats> DailyStats { get; set; }
        public List<ShipperPerformance> TopShippers { get; set; }
        public List<ZoneStats> ZoneStatistics { get; set; }
    }

    public class DailyDeliveryStats
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ShipperPerformance
    {
        public int ShipperId { get; set; }
        public string ShipperName { get; set; }
        public int TotalDeliveries { get; set; }
        public int CompletedDeliveries { get; set; }
        public decimal Rating { get; set; }
        public double AverageTime { get; set; }
        public decimal TotalEarnings { get; set; }
    }

    public class ZoneStats
    {
        public string ZoneName { get; set; }
        public string District { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public double AverageDeliveryTime { get; set; }
    }

    #endregion

    #region API DTOs

    /// <summary>
    /// DTO cho gán shipper
    /// </summary>
    public class AssignShipperDto
    {
        public int OrderId { get; set; }
        public int ShipperId { get; set; }
    }

    /// <summary>
    /// DTO cho cập nhật vị trí
    /// </summary>
    public class UpdateLocationDto
    {
        public int ShipperId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    /// <summary>
    /// DTO cho cập nhật trạng thái delivery
    /// </summary>
    public class UpdateDeliveryStatusDto
    {
        public int AssignmentId { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string ProofImageUrl { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// DTO cho đánh giá shipper
    /// </summary>
    public class RateDeliveryDto
    {
        public int AssignmentId { get; set; }
        public int Rating { get; set; }
        public string Feedback { get; set; }
    }

    /// <summary>
    /// DTO cho tính phí giao hàng
    /// </summary>
    public class CalculateDeliveryFeeDto
    {
        public string District { get; set; }
        public string Ward { get; set; }
        public decimal OrderAmount { get; set; }
    }

    /// <summary>
    /// Response cho tính phí giao hàng
    /// </summary>
    public class DeliveryFeeResponse
    {
        public decimal DeliveryFee { get; set; }
        public int EstimatedTime { get; set; }
        public bool IsFreeDelivery { get; set; }
        public decimal MinOrderForFree { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
