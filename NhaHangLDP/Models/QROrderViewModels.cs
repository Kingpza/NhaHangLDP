using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho trang menu QR
    /// </summary>
    public class QRMenuViewModel
    {
        public int TableId { get; set; }
        public string TableNumber { get; set; }
        public string SessionToken { get; set; }
        public string TableAreaName { get; set; }
        public List<QRMenuCategoryViewModel> Categories { get; set; }
        public List<MenuItemViewModel> FeaturedItems { get; set; }
        public List<MenuItemViewModel> NewItems { get; set; }
        public QROrderViewModel CurrentOrder { get; set; }
        public string RestaurantName { get; set; }
        public string RestaurantPhone { get; set; }

        public QRMenuViewModel()
        {
            Categories = new List<QRMenuCategoryViewModel>();
            FeaturedItems = new List<MenuItemViewModel>();
            NewItems = new List<MenuItemViewModel>();
        }
    }

    /// <summary>
    /// ViewModel cho danh mục món ăn trong QR menu
    /// </summary>
    public class QRMenuCategoryViewModel
    {
        public string CategoryName { get; set; }
        public string CategoryIcon { get; set; }
        public List<MenuItemViewModel> Items { get; set; }

        public QRMenuCategoryViewModel()
        {
            Items = new List<MenuItemViewModel>();
        }
    }

    /// <summary>
    /// ViewModel cho món ăn
    /// </summary>
    public class MenuItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public int PreparationTime { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsNew { get; set; }
        public decimal Rating { get; set; }
        public int ReviewCount { get; set; }
        public int SoldCount { get; set; }

        public bool HasDiscount => OriginalPrice > Price;
        public int DiscountPercent => HasDiscount && OriginalPrice > 0 
            ? (int)Math.Round((1 - Price / OriginalPrice) * 100) 
            : 0;
    }

    /// <summary>
    /// ViewModel cho đơn hàng QR
    /// </summary>
    public class QROrderViewModel
    {
        public int Id { get; set; }
        public string QROrderCode { get; set; }
        public string SessionToken { get; set; }
        public int TableId { get; set; }
        public string TableNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? SubmittedTime { get; set; }
        public DateTime? ConfirmedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int? LinkedOrderId { get; set; }
        public string AppliedPromotionCode { get; set; }
        public List<QROrderItemViewModel> Items { get; set; }

        // Computed properties
        public decimal SubTotal => Items != null ? Items.Sum(i => i.TotalPrice) : 0;
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount => SubTotal - DiscountAmount;
        public int TotalItems => Items != null ? Items.Sum(i => i.Quantity) : 0;

        public string StatusText
        {
            get
            {
                switch (Status?.ToLower())
                {
                    case "draft": return "Đang chọn món";
                    case "submitted": return "Đã gửi, chờ xác nhận";
                    case "confirmed": return "Đã xác nhận";
                    case "preparing": return "Đang chuẩn bị";
                    case "ready": return "Sẵn sàng phục vụ";
                    case "completed": return "Hoàn thành";
                    case "cancelled": return "Đã hủy";
                    default: return Status;
                }
            }
        }

        public string StatusClass
        {
            get
            {
                switch (Status?.ToLower())
                {
                    case "draft": return "secondary";
                    case "submitted": return "info";
                    case "confirmed": return "primary";
                    case "preparing": return "warning";
                    case "ready": return "success";
                    case "completed": return "success";
                    case "cancelled": return "danger";
                    default: return "secondary";
                }
            }
        }

        public QROrderViewModel()
        {
            Items = new List<QROrderItemViewModel>();
            Status = "Draft";
            CreatedTime = DateTime.Now;
        }
    }

    /// <summary>
    /// ViewModel cho chi tiết món trong đơn QR
    /// </summary>
    public class QROrderItemViewModel
    {
        public int Id { get; set; }
        public int QROrderId { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }
        public string MenuItemImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ItemNotes { get; set; }
        public string ItemStatus { get; set; }
        public DateTime AddedTime { get; set; }

        public decimal TotalPrice => Quantity * UnitPrice;

        public string StatusText
        {
            get
            {
                switch (ItemStatus?.ToLower())
                {
                    case "pending": return "Chờ xác nhận";
                    case "confirmed": return "Đã xác nhận";
                    case "preparing": return "Đang chuẩn bị";
                    case "ready": return "Đã sẵn sàng";
                    case "served": return "Đã phục vụ";
                    case "cancelled": return "Đã hủy";
                    default: return "Chờ gửi";
                }
            }
        }
    }

    /// <summary>
    /// DTO để thêm món vào đơn QR
    /// </summary>
    public class AddToQROrderDto
    {
        [Required]
        public int MenuItemId { get; set; }
        
        [Required]
        [Range(1, 99)]
        public int Quantity { get; set; }
        
        public string Notes { get; set; }
        
        [Required]
        public string SessionToken { get; set; }
        
        [Required]
        public int TableId { get; set; }
    }

    /// <summary>
    /// DTO để cập nhật số lượng món trong đơn QR
    /// </summary>
    public class UpdateQROrderItemDto
    {
        [Required]
        public int OrderDetailId { get; set; }
        
        [Required]
        [Range(0, 99)]
        public int Quantity { get; set; }
        
        [Required]
        public string SessionToken { get; set; }
    }

    /// <summary>
    /// DTO để gửi đơn QR
    /// </summary>
    public class SubmitQROrderDto
    {
        [Required]
        public string SessionToken { get; set; }
        
        [Required]
        public int TableId { get; set; }
        
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string Notes { get; set; }
        public string PromotionCode { get; set; }
    }

    /// <summary>
    /// ViewModel cho danh sách đơn QR (quản lý)
    /// </summary>
    public class QROrderListViewModel
    {
        public List<QROrderViewModel> Orders { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int ConfirmedOrders { get; set; }
        public string StatusFilter { get; set; }
        public DateTime? DateFilter { get; set; }

        public QROrderListViewModel()
        {
            Orders = new List<QROrderViewModel>();
        }
    }

    /// <summary>
    /// Response khi scan QR code
    /// </summary>
    public class QRScanResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TableId { get; set; }
        public string TableNumber { get; set; }
        public string SessionToken { get; set; }
        public string RedirectUrl { get; set; }
        public bool HasExistingSession { get; set; }
        public QROrderViewModel ExistingOrder { get; set; }
    }
}
