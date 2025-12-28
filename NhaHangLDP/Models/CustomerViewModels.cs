using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    #region Cart ViewModels

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();
        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Discount { get; set; }
        public string VoucherCode { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalItems { get; set; }
    }

    public class CartItemViewModel
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public string SpecialInstructions { get; set; }
    }

    public class AddToCartRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; } = 1;
        public string SpecialInstructions { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
    }

    #endregion

    #region Checkout ViewModels

    public class CheckoutViewModel
    {
        public CartViewModel Cart { get; set; }
        public List<CustomerAddressViewModel> SavedAddresses { get; set; }
        public List<VoucherViewModel> AvailableVouchers { get; set; }
        public CustomerInfoViewModel CustomerInfo { get; set; }
        public CheckoutFormModel Form { get; set; }
    }

    public class CheckoutFormModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100)]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string CustomerPhone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string CustomerEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại đơn hàng")]
        public string OrderType { get; set; } = "Delivery";

        public int? AddressId { get; set; }

        [StringLength(500)]
        public string DeliveryAddress { get; set; }

        public string Ward { get; set; }
        public string District { get; set; }
        public string City { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD";

        public string VoucherCode { get; set; }

        [StringLength(500)]
        public string Note { get; set; }

        public bool SaveAddress { get; set; } = false;
    }

    public class CustomerAddressViewModel
    {
        public int Id { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverPhone { get; set; }
        public string FullAddress { get; set; }
        public string AddressLine { get; set; }
        public string Ward { get; set; }
        public string District { get; set; }
        public string City { get; set; }
        public string AddressType { get; set; }
        public bool IsDefault { get; set; }
    }

    public class CustomerInfoViewModel
    {
        public int? Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public bool IsLoggedIn { get; set; }
    }

    #endregion

    #region Order ViewModels

    public class OrderConfirmationViewModel
    {
        public string OrderCode { get; set; }
        public string Status { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? EstimatedDeliveryTime { get; set; }
        public List<OrderItemViewModel> Items { get; set; }
    }

    public class OrderItemViewModel
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class OrderListViewModel
    {
        public List<OrderSummaryItem> Orders { get; set; }
        public int TotalOrders { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class OrderSummaryItem
    {
        public int Id { get; set; }
        public string OrderCode { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public string StatusText { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public string FirstItemImage { get; set; }
        public bool CanCancel { get; set; }
        public bool CanReview { get; set; }
    }

    public class OrderTrackingViewModel
    {
        public string OrderCode { get; set; }
        public string Status { get; set; }
        public List<OrderTrackingStep> Steps { get; set; }
        public OrderConfirmationViewModel OrderInfo { get; set; }
    }

    public class OrderTrackingStep
    {
        public string Status { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? Time { get { return CompletedAt; } set { CompletedAt = value; } }
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
    }

    #endregion

    #region Reservation ViewModels

    public class ReservationFormModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone]
        public string CustomerPhone { get; set; }

        [EmailAddress]
        public string CustomerEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt bàn")]
        public DateTime ReservationDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ")]
        public string ReservationTime { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số khách")]
        [Range(1, 50, ErrorMessage = "Số khách từ 1-50")]
        public int NumberOfGuests { get; set; }

        public string TablePreference { get; set; }

        public string SpecialRequests { get; set; }
    }

    public class ReservationViewModel
    {
        public int Id { get; set; }
        public string ReservationCode { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan ReservationTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string TableInfo { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public string StatusText { get; set; }
        public bool CanCancel { get; set; }
        public bool CanModify { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MyReservationViewModel
    {
        public int Id { get; set; }
        public string ReservationCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan ReservationTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string TablePreference { get; set; }
        public string TableName { get; set; }
        public string SpecialRequests { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public string StatusText { get; set; }
        public bool CanCancel { get; set; }
        public bool CanModify { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsUpcoming { get; set; }

        public string ReservationTimeFormatted => ReservationTime.ToString(@"hh\:mm");
        public string ReservationDateFormatted => ReservationDate.ToString("dd/MM/yyyy");
    }

    public class TimeSlotViewModel
    {
        public string Time { get; set; }
        public bool IsAvailable { get; set; }
        public int AvailableTables { get; set; }
    }

    #endregion

    #region Review ViewModels

    public class ReviewFormModel
    {
        public int MenuItemId { get; set; }
        public int? OrderId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }

        public List<string> ImageUrls { get; set; }
    }

    public class ReviewViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAvatar { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public List<string> ImageUrls { get; set; }
        public int LikesCount { get; set; }
        public bool IsVerifiedPurchase { get; set; }
        public string AdminReply { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsOwnReview { get; set; }
        public bool HasLiked { get; set; }
    }

    public class MenuItemReviewSummary
    {
        public int MenuItemId { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingBreakdown { get; set; } // Rating -> Count
        public List<ReviewViewModel> RecentReviews { get; set; }
    }

    #endregion

    #region Voucher ViewModels

    public class VoucherViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DiscountText { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsApplicable { get; set; }
        public string NotApplicableReason { get; set; }
    }

    public class ApplyVoucherResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NewTotal { get; set; }
    }

    #endregion

    #region Account ViewModels

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại")]
        public string EmailOrPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        public bool AgreeTerms { get; set; }
    }

    public class CustomerProfileViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string AvatarUrl { get; set; }
        public int LoyaltyPoints { get; set; }
        public string MembershipLevel { get; set; }
        public DateTime CreatedDate { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public List<CustomerAddressViewModel> Addresses { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }
    }

    public class WishlistItemViewModel
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public DateTime AddedDate { get; set; }
    }

    #endregion

    #region Menu ViewModels

    public class MenuDetailViewModel
    {
        public MenuItem Item { get; set; }
        public MenuItemReviewSummary ReviewSummary { get; set; }
        public List<MenuItem> RelatedItems { get; set; }
        public bool IsInWishlist { get; set; }
        public bool CanReview { get; set; }
    }

    public class MenuFilterModel
    {
        public string Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string SortBy { get; set; } = "Name"; // Name, PriceAsc, PriceDesc, Popular, Newest
        public string SearchTerm { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public class MenuPageViewModel
    {
        public List<MenuItem> Items { get; set; }
        public List<string> Categories { get; set; }
        public MenuFilterModel Filter { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    #endregion

    #region Common Result Models

    public class ApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    #endregion
}
