using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    #region Payment ViewModels

    /// <summary>
    /// ViewModel cho trang thanh toán đơn hàng
    /// </summary>
    public class PaymentPageViewModel
    {
        public OrderPaymentInfo Order { get; set; }
        public List<PaymentMethodOption> PaymentMethods { get; set; }
        public List<AvailablePromotion> AvailablePromotions { get; set; }
        public PaymentSummary Summary { get; set; }
        public string RestaurantName { get; set; }
        public string RestaurantPhone { get; set; }
    }

    /// <summary>
    /// Thông tin đơn hàng để thanh toán
    /// </summary>
    public class OrderPaymentInfo
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string TableNumber { get; set; }
        public string TableArea { get; set; }
        public DateTime OrderTime { get; set; }
        public string WaiterName { get; set; }
        public string Status { get; set; }
        public List<PaymentOrderItem> Items { get; set; } = new List<PaymentOrderItem>();
        public int TotalItems { get; set; }
        public decimal SubTotal { get; set; }
    }

    /// <summary>
    /// Món ăn trong đơn thanh toán
    /// </summary>
    public class PaymentOrderItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// Tùy chọn phương thức thanh toán
    /// </summary>
    public class PaymentMethodOption
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public bool IsDefault { get; set; }
        public bool IsEnabled { get; set; }
        public decimal? ServiceFee { get; set; }
    }

    /// <summary>
    /// Khuyến mãi có thể áp dụng
    /// </summary>
    public class AvailablePromotion
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscount { get; set; }
        public decimal MinOrderValue { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsApplicable { get; set; }
        public string NotApplicableReason { get; set; }
    }

    /// <summary>
    /// Tổng hợp thanh toán
    /// </summary>
    public class PaymentSummary
    {
        public decimal SubTotal { get; set; }
        public decimal VATPercent { get; set; } = 10;
        public decimal VATAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountCode { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }
    }

    #endregion

    #region Payment Request/Response DTOs

    /// <summary>
    /// Request thanh toán
    /// </summary>
    public class ProcessPaymentRequest
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public string PaymentMethod { get; set; }

        public decimal ReceivedAmount { get; set; }

        public string PromotionCode { get; set; }

        public string CustomerName { get; set; }

        public string CustomerPhone { get; set; }

        public string CustomerEmail { get; set; }

        public string Notes { get; set; }

        public bool PrintBill { get; set; } = true;

        public bool SendEmailReceipt { get; set; } = false;

        // Cho thanh toán tách bill
        public bool IsSplitPayment { get; set; } = false;
        public List<int> ItemIds { get; set; }
        public decimal? SplitAmount { get; set; }
    }

    /// <summary>
    /// Response thanh toán
    /// </summary>
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? BillId { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal ChangeAmount { get; set; }
        public string PrintUrl { get; set; }
        public DateTime? PaymentTime { get; set; }
        public int? EarnedPoints { get; set; }
    }

    /// <summary>
    /// Request validate promotion
    /// </summary>
    public class ValidatePromotionRequest
    {
        public string Code { get; set; }
        public int OrderId { get; set; }
        public decimal OrderTotal { get; set; }
        public string CustomerPhone { get; set; }
    }

    #endregion

    #region Invoice/Bill ViewModels

    /// <summary>
    /// ViewModel đầy đủ cho hóa đơn
    /// </summary>
    public class BillDetailViewModel
    {
        // Thông tin hóa đơn
        public int BillId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime BillDate { get; set; }
        public string Status { get; set; }

        // Thông tin đơn hàng
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public DateTime OrderTime { get; set; }

        // Thông tin bàn
        public string TableNumber { get; set; }
        public string TableArea { get; set; }

        // Thông tin nhân viên
        public string CashierName { get; set; }
        public string WaiterName { get; set; }

        // Thông tin khách hàng
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }

        // Chi tiết món ăn
        public List<BillItemViewModel> Items { get; set; } = new List<BillItemViewModel>();

        // Tính tiền
        public decimal SubTotal { get; set; }
        public decimal VATPercent { get; set; }
        public decimal VATAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountCode { get; set; }
        public string DiscountDescription { get; set; }
        public decimal ServiceCharge { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }

        // Thanh toán
        public string PaymentMethod { get; set; }
        public string PaymentMethodDisplay { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }

        // Thông tin nhà hàng
        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantPhone { get; set; }
        public string RestaurantEmail { get; set; }
        public string RestaurantTaxCode { get; set; }
        public string RestaurantLogo { get; set; }

        // Thông tin bổ sung
        public string Notes { get; set; }
        public string QRCodeData { get; set; }
        public int? EarnedPoints { get; set; }
    }

    /// <summary>
    /// Món ăn trong hóa đơn chi tiết
    /// </summary>
    public class BillItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// ViewModel cho danh sách hóa đơn
    /// </summary>
    public class BillListViewModel
    {
        public List<BillSummaryItem> Bills { get; set; } = new List<BillSummaryItem>();
        public BillStatistics Statistics { get; set; }
        public BillFilterModel Filter { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    /// <summary>
    /// Item trong danh sách hóa đơn
    /// </summary>
    public class BillSummaryItem
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string TableNumber { get; set; }
        public DateTime BillDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public string CashierName { get; set; }
        public string CustomerName { get; set; }
        public int ItemCount { get; set; }
    }

    /// <summary>
    /// Thống kê hóa đơn
    /// </summary>
    public class BillStatistics
    {
        public int TotalBills { get; set; }
        public int PaidBills { get; set; }
        public int RefundedBills { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalVAT { get; set; }
        public decimal AverageOrderValue { get; set; }
        public Dictionary<string, decimal> RevenueByPaymentMethod { get; set; } = new Dictionary<string, decimal>();
    }

    /// <summary>
    /// Filter cho danh sách hóa đơn
    /// </summary>
    public class BillFilterModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public string Search { get; set; }
        public int? CashierId { get; set; }
        public int? ShiftId { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "BillDate";
        public string SortOrder { get; set; } = "desc";
    }

    #endregion

    #region Split Bill ViewModels

    /// <summary>
    /// ViewModel cho tách bill
    /// </summary>
    public class SplitBillViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string TableNumber { get; set; }
        public List<SplitBillItem> Items { get; set; } = new List<SplitBillItem>();
        public decimal TotalAmount { get; set; }
        public List<SplitBillPart> Parts { get; set; } = new List<SplitBillPart>();
    }

    /// <summary>
    /// Món ăn trong tách bill
    /// </summary>
    public class SplitBillItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int RemainingQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsAssigned { get; set; }
        public int? AssignedToPart { get; set; }
    }

    /// <summary>
    /// Phần bill sau khi tách
    /// </summary>
    public class SplitBillPart
    {
        public int PartNumber { get; set; }
        public string CustomerName { get; set; }
        public List<SplitBillPartItem> Items { get; set; } = new List<SplitBillPartItem>();
        public decimal SubTotal { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentMethod { get; set; }
    }

    /// <summary>
    /// Món trong phần bill
    /// </summary>
    public class SplitBillPartItem
    {
        public int OrderDetailId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// Request tách bill
    /// </summary>
    public class SplitBillRequest
    {
        public int OrderId { get; set; }
        public string SplitType { get; set; } // "ByItems", "Equal", "ByAmount"
        public int? NumberOfParts { get; set; }
        public List<SplitBillPartRequest> Parts { get; set; }
    }

    /// <summary>
    /// Request cho mỗi phần bill
    /// </summary>
    public class SplitBillPartRequest
    {
        public int PartNumber { get; set; }
        public string CustomerName { get; set; }
        public List<SplitItemRequest> Items { get; set; }
        public decimal? FixedAmount { get; set; }
    }

    /// <summary>
    /// Request cho món trong phần bill
    /// </summary>
    public class SplitItemRequest
    {
        public int OrderDetailId { get; set; }
        public int Quantity { get; set; }
    }

    #endregion

    #region Refund ViewModels

    /// <summary>
    /// Request hoàn tiền
    /// </summary>
    public class RefundRequest
    {
        [Required]
        public int BillId { get; set; }

        [Required]
        public string Reason { get; set; }

        public decimal? RefundAmount { get; set; }

        public bool IsFullRefund { get; set; } = true;

        public List<RefundItemRequest> Items { get; set; }

        public string RefundMethod { get; set; }

        public string Notes { get; set; }
    }

    /// <summary>
    /// Món hoàn tiền
    /// </summary>
    public class RefundItemRequest
    {
        public int OrderDetailId { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Kết quả hoàn tiền
    /// </summary>
    public class RefundResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? RefundId { get; set; }
        public decimal RefundAmount { get; set; }
        public DateTime? RefundTime { get; set; }
    }

    /// <summary>
    /// Thông tin hoàn tiền
    /// </summary>
    public class RefundViewModel
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal RefundAmount { get; set; }
        public string Reason { get; set; }
        public string RefundMethod { get; set; }
        public string Status { get; set; }
        public DateTime RefundDate { get; set; }
        public string ProcessedBy { get; set; }
        public List<RefundItemViewModel> Items { get; set; }
    }

    /// <summary>
    /// Món đã hoàn tiền
    /// </summary>
    public class RefundItemViewModel
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
    }

    #endregion

    #region Payment History

    /// <summary>
    /// Lịch sử thanh toán
    /// </summary>
    public class PaymentHistoryViewModel
    {
        public List<PaymentHistoryItem> Payments { get; set; } = new List<PaymentHistoryItem>();
        public decimal TotalAmount { get; set; }
        public int TotalTransactions { get; set; }
    }

    /// <summary>
    /// Item lịch sử thanh toán
    /// </summary>
    public class PaymentHistoryItem
    {
        public int Id { get; set; }
        public string TransactionId { get; set; }
        public string Type { get; set; } // Payment, Refund, Adjustment
        public int BillId { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public DateTime TransactionTime { get; set; }
        public string ProcessedBy { get; set; }
        public string Notes { get; set; }
    }

    #endregion

    #region Daily Summary

    /// <summary>
    /// Tổng kết thanh toán theo ngày
    /// </summary>
    public class DailyPaymentSummary
    {
        public DateTime Date { get; set; }
        public int TotalBills { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalVAT { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<PaymentMethodSummary> PaymentBreakdown { get; set; } = new List<PaymentMethodSummary>();
        public List<HourlyRevenue> HourlyBreakdown { get; set; } = new List<HourlyRevenue>();
    }

    /// <summary>
    /// Doanh thu theo giờ
    /// </summary>
    public class HourlyRevenue
    {
        public int Hour { get; set; }
        public string HourDisplay { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    #endregion
}
