using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    
    public class ShiftDetailsViewModel
    {
        
        public CashierShift ShiftInfo { get; set; }

        /// <summary>
    /// Danh sách hóa đơn trong ca
      /// </summary>
        public List<ShiftOrderViewModel> Orders { get; set; } = new List<ShiftOrderViewModel>();

 /// <summary>
        /// Tổng doanh thu ca
    /// </summary>
        public decimal TotalRevenue { get; set; }

   /// <summary>
    /// Tổng số đơn hàng
        /// </summary>
        public int TotalOrders { get; set; }

        /// <summary>
        /// Số đơn hàng hoàn thành
        /// </summary>
        public int CompletedOrders { get; set; }

        /// <summary>
        /// Số đơn hàng đã hủy
        /// </summary>
      public int CancelledOrders { get; set; }

        /// <summary>
     /// Giá trị trung bình mỗi đơn hàng
        /// </summary>
        public decimal AverageOrderValue => TotalOrders > 0 ? TotalRevenue / TotalOrders : 0;

        /// <summary>
        /// Thời gian làm việc (phút)
    /// </summary>
        public int WorkingMinutes
        {
            get
 {
                if (ShiftInfo?.StartTime != null)
            {
      var endTime = ShiftInfo.EndTime ?? DateTime.Now;
          return (int)(endTime - ShiftInfo.StartTime).TotalMinutes;
          }
          return 0;
  }
  }

        /// <summary>
        /// Thời gian làm việc định dạng hiển thị
        /// </summary>
        public string WorkingTimeDisplay
{
       get
        {
     var minutes = WorkingMinutes;
         var hours = minutes / 60;
     var mins = minutes % 60;
       return $"{hours:D2}:{mins:D2}";
            }
      }
  }

    /// <summary>
    /// ViewModel cho đơn hàng trong ca làm việc
    /// </summary>
    public class ShiftOrderViewModel
    {
        public int Id { get; set; }
        public string OrderId => $"DH{Id:D6}";
        public string TableNumber { get; set; }
        public DateTime OrderTime { get; set; }
        public string Date => OrderTime.ToString("dd/MM/yyyy");
        public string Time => OrderTime.ToString("HH:mm");
        public string Status { get; set; }
        public string StatusDisplay { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public int ItemCount { get; set; }
        public List<ShiftOrderItemViewModel> Items { get; set; } = new List<ShiftOrderItemViewModel>();
        public bool IsPaid { get; set; }
        public string CashierName { get; set; }

        public int? BillId { get; set; }
    }

  /// <summary>
    /// ViewModel cho món ăn trong đơn hàng
    /// </summary>
    public class ShiftOrderItemViewModel
    {
     public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
        public string Description { get; set; }
    }

    /// <summary>
    /// ViewModel cho thống kê tóm tắt ca làm việc
    /// </summary>
    public class ShiftSummaryViewModel
    {
        public int TotalOrders { get; set; }
      public decimal TotalRevenue { get; set; }
        public int CompletedOrders { get; set; }
   public int CancelledOrders { get; set; }
 public decimal AvgOrderValue { get; set; }
        public string WorkingTime { get; set; }
    }

    /// <summary>
    /// Filter parameters cho API GetShiftOrdersData
    /// </summary>
    public class ShiftOrdersFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Search { get; set; }
        public string Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    //In chi tiết hóa đơn
    public class InvoiceViewModel
    {
        public string OrderId { get; set; }
        public string BillId { get; set; }
        public string TableNumber { get; set; }
        public DateTime OrderTime { get; set; }
        public DateTime BillDate { get; set; }
        public string CashierName { get; set; }
        public string PaymentMethod { get; set; }
        public List<InvoiceItemViewModel> Items { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VAT { get; set; }
        public decimal TotalAmount { get; set; }

        // Bạn có thể thêm các thông tin nhà hàng ở đây
        public string RestaurantName { get; set; } = "LDP Restaurant";
        public string RestaurantAddress { get; set; } = "123 Đường ABC, Quận 1, TP.HCM";
        public string RestaurantPhone { get; set; } = "0123 456 789";
    }

    /// <summary>
    /// ViewModel cho các món ăn trong hóa đơn
    /// </summary>
    public class InvoiceItemViewModel
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

}