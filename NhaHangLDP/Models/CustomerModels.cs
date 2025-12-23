using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace NhaHangLDP.Models
{
    #region Customer Entity

    /// <summary>
    /// Khách hàng
    /// </summary>
    [Table("Customer")]
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(256)]
        public string PasswordHash { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string Gender { get; set; }

        [StringLength(500)]
        public string AvatarUrl { get; set; }

        public int LoyaltyPoints { get; set; } = 0;

        [StringLength(20)]
        public string MembershipLevel { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum

        public bool IsActive { get; set; } = true;

        public bool EmailVerified { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? LastLoginDate { get; set; }

        // Navigation properties
        public virtual ICollection<CustomerAddress> Addresses { get; set; }
        public virtual ICollection<CustomerOrder> CustomerOrders { get; set; }
        public virtual ICollection<Reservation> Reservations { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<Wishlist> Wishlists { get; set; }
        public virtual Cart Cart { get; set; }
    }

    #endregion

    #region Address

    /// <summary>
    /// Địa chỉ giao hàng
    /// </summary>
    [Table("CustomerAddress")]
    public class CustomerAddress
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string ReceiverName { get; set; }

        [Required]
        [StringLength(20)]
        public string ReceiverPhone { get; set; }

        [Required]
        [StringLength(500)]
        public string AddressLine { get; set; }

        [StringLength(100)]
        public string Ward { get; set; }

        [StringLength(100)]
        public string District { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(50)]
        public string AddressType { get; set; } = "Home"; // Home, Office, Other

        public bool IsDefault { get; set; } = false;

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }
    }

    #endregion

    #region Cart

    /// <summary>
    /// Giỏ hàng
    /// </summary>
    [Table("Cart")]
    public class Cart
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        [StringLength(100)]
        public string SessionId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        public virtual ICollection<CartItem> Items { get; set; }

        [NotMapped]
        public decimal TotalAmount
        {
            get { return Items != null ? Items.Sum(i => i.Subtotal) : 0; }
        }

        [NotMapped]
        public int TotalItems
        {
            get { return Items != null ? Items.Sum(i => i.Quantity) : 0; }
        }
    }

    /// <summary>
    /// Món trong giỏ hàng
    /// </summary>
    [Table("CartItem")]
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        public int CartId { get; set; }

        public int MenuItemId { get; set; }

        public int Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        [StringLength(500)]
        public string SpecialInstructions { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.Now;

        [ForeignKey("CartId")]
        public virtual Cart Cart { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem MenuItem { get; set; }

        [NotMapped]
        public decimal Subtotal => UnitPrice * Quantity;
    }

    #endregion

    #region Customer Order (renamed to avoid conflict)

    /// <summary>
    /// Đơn hàng khách hàng (CustomerOrder để tránh conflict với Order)
    /// </summary>
    [Table("CustomerOrder")]
    public class CustomerOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string OrderCode { get; set; }

        public int? CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; }

        [StringLength(100)]
        public string CustomerEmail { get; set; }

        [StringLength(20)]
        public string OrderType { get; set; } = "Delivery"; // Delivery, TakeAway, DineIn

        // Địa chỉ giao hàng
        [StringLength(500)]
        public string DeliveryAddress { get; set; }

        [StringLength(100)]
        public string Ward { get; set; }

        [StringLength(100)]
        public string District { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        // Thông tin đơn hàng
        public decimal SubTotal { get; set; }

        public decimal DeliveryFee { get; set; } = 0;

        public decimal Discount { get; set; } = 0;

        [StringLength(50)]
        public string VoucherCode { get; set; }

        public decimal TotalAmount { get; set; }

        // Thanh toán
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "COD"; // COD, VNPay, MoMo, Card

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Failed, Refunded

        public DateTime? PaidDate { get; set; }

        [StringLength(100)]
        public string TransactionId { get; set; }

        // Trạng thái
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Preparing, Ready, Delivering, Completed, Cancelled

        [StringLength(500)]
        public string Note { get; set; }

        [StringLength(500)]
        public string CancelReason { get; set; }

        // Thời gian
        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime? ConfirmedDate { get; set; }

        public DateTime? PreparingDate { get; set; }

        public DateTime? ReadyDate { get; set; }

        public DateTime? DeliveringDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        public DateTime? EstimatedDeliveryTime { get; set; }

        // Đánh giá
        public int? Rating { get; set; }

        [StringLength(500)]
        public string ReviewComment { get; set; }

        // Điểm thưởng
        public int EarnedPoints { get; set; } = 0;

        public int UsedPoints { get; set; } = 0;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        public virtual ICollection<CustomerOrderDetail> OrderDetails { get; set; }
    }

    /// <summary>
    /// Chi tiết đơn hàng (CustomerOrderDetail để tránh conflict với OrderDetail)
    /// </summary>
    [Table("CustomerOrderDetail")]
    public class CustomerOrderDetail
    {
        [Key]
        public int Id { get; set; }

        public int CustomerOrderId { get; set; }

        public int MenuItemId { get; set; }

        [StringLength(200)]
        public string ItemName { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }

        [StringLength(500)]
        public string SpecialInstructions { get; set; }

        [ForeignKey("CustomerOrderId")]
        public virtual CustomerOrder CustomerOrder { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem MenuItem { get; set; }
    }

    #endregion

    #region Reservation

    /// <summary>
    /// Đặt bàn
    /// </summary>
    [Table("Reservation")]
    public class Reservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string ReservationCode { get; set; }

        public int? CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; }

        [StringLength(100)]
        public string CustomerEmail { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan ReservationTime { get; set; }

        public int NumberOfGuests { get; set; }

        public int? TableId { get; set; }

        [StringLength(50)]
        public string TablePreference { get; set; } // Window, VIP, Outdoor, Private

        [StringLength(500)]
        public string SpecialRequests { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled, NoShow

        public decimal? DepositAmount { get; set; }

        public bool DepositPaid { get; set; } = false;

        [StringLength(500)]
        public string CancelReason { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ConfirmedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        // Note: Table navigation removed to avoid dependency on Table entity
    }

    #endregion

    #region Review

    /// <summary>
    /// Đánh giá món ăn
    /// </summary>
    [Table("Review")]
    public class Review
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public int MenuItemId { get; set; }

        public int? OrderId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }

        [StringLength(500)]
        public string ImageUrls { get; set; } // JSON array of image URLs

        public int LikesCount { get; set; } = 0;

        public bool IsVerifiedPurchase { get; set; } = false;

        [StringLength(500)]
        public string AdminReply { get; set; }

        public DateTime? AdminReplyDate { get; set; }

        public bool IsApproved { get; set; } = true;

        public bool IsHidden { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem MenuItem { get; set; }

        [ForeignKey("OrderId")]
        public virtual CustomerOrder Order { get; set; }
    }

    #endregion

    #region Wishlist

    /// <summary>
    /// Danh sách yêu thích
    /// </summary>
    [Table("Wishlist")]
    public class Wishlist
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public int MenuItemId { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.Now;

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem MenuItem { get; set; }
    }

    #endregion

    #region Voucher

    /// <summary>
    /// Mã giảm giá
    /// </summary>
    [Table("Voucher")]
    public class Voucher
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(20)]
        public string DiscountType { get; set; } = "Percentage"; // Percentage, FixedAmount

        public decimal DiscountValue { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        public decimal? MinOrderAmount { get; set; }

        public int? UsageLimit { get; set; }

        public int UsedCount { get; set; } = 0;

        public int? PerCustomerLimit { get; set; } = 1;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(50)]
        public string ApplicableTo { get; set; } = "All"; // All, NewCustomer, SpecificItems

        [StringLength(500)]
        public string ApplicableItemIds { get; set; } // JSON array

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Lịch sử sử dụng voucher
    /// </summary>
    [Table("VoucherUsage")]
    public class VoucherUsage
    {
        [Key]
        public int Id { get; set; }

        public int VoucherId { get; set; }

        public int? CustomerId { get; set; }

        public int OrderId { get; set; }

        public decimal DiscountAmount { get; set; }

        public DateTime UsedDate { get; set; } = DateTime.Now;

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [ForeignKey("OrderId")]
        public virtual CustomerOrder Order { get; set; }
    }

    #endregion

    #region Notification

    /// <summary>
    /// Thông báo
    /// </summary>
    [Table("CustomerNotification")]
    public class CustomerNotification
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string Message { get; set; }

        [StringLength(50)]
        public string Type { get; set; } = "General"; // General, Order, Promotion, Reservation

        [StringLength(500)]
        public string ActionUrl { get; set; }

        [StringLength(200)]
        public string ImageUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ReadDate { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }
    }

    #endregion
}
