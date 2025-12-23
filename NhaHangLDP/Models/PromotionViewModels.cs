using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho trang danh sách khuyến mãi
    /// </summary>
    public class PromotionListViewModel
    {
        public List<PromotionItemViewModel> Promotions { get; set; }
        public PromotionStatsViewModel Stats { get; set; }
        public string SearchTerm { get; set; }
        public string StatusFilter { get; set; }
        public string TypeFilter { get; set; }

        public PromotionListViewModel()
        {
            Promotions = new List<PromotionItemViewModel>();
            Stats = new PromotionStatsViewModel();
        }
    }

    /// <summary>
    /// ViewModel cho từng khuyến mãi trong danh sách
    /// </summary>
    public class PromotionItemViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? MaxUsageCount { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; }
        public string ApplicableTo { get; set; }
        public string ApplicableDays { get; set; }
        public TimeSpan? HappyHourStart { get; set; }
        public TimeSpan? HappyHourEnd { get; set; }

        // Computed properties
        public string StatusText
        {
            get
            {
                if (!IsActive) return "Tạm dừng";
                if (DateTime.Now < StartDate) return "Chưa bắt đầu";
                if (DateTime.Now > EndDate) return "Hết hạn";
                if (MaxUsageCount.HasValue && UsedCount >= MaxUsageCount.Value) return "Hết lượt";
                return "Đang hoạt động";
            }
        }

        public string StatusClass
        {
            get
            {
                var status = StatusText;
                switch (status)
                {
                    case "Đang hoạt động": return "success";
                    case "Chưa bắt đầu": return "info";
                    case "Hết hạn": return "secondary";
                    case "Hết lượt": return "warning";
                    default: return "danger";
                }
            }
        }

        public string DiscountDisplay
        {
            get
            {
                if (DiscountType == "Percentage")
                    return $"Giảm {DiscountValue}%";
                return $"Giảm {DiscountValue:N0}đ";
            }
        }

        public int UsagePercentage
        {
            get
            {
                if (!MaxUsageCount.HasValue || MaxUsageCount.Value == 0) return 0;
                return (int)((UsedCount * 100.0) / MaxUsageCount.Value);
            }
        }
    }

    /// <summary>
    /// Thống kê tổng quan khuyến mãi
    /// </summary>
    public class PromotionStatsViewModel
    {
        public int TotalPromotions { get; set; }
        public int ActivePromotions { get; set; }
        public int ExpiredPromotions { get; set; }
        public int TotalUsageCount { get; set; }
        public decimal TotalDiscountGiven { get; set; }
    }

    /// <summary>
    /// ViewModel cho form tạo/sửa khuyến mãi
    /// </summary>
    public class PromotionFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi")]
        [StringLength(20, ErrorMessage = "Mã khuyến mãi tối đa 20 ký tự")]
        [Display(Name = "Mã khuyến mãi")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên khuyến mãi")]
        [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
        [Display(Name = "Tên khuyến mãi")]
        public string Name { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại giảm giá")]
        [Display(Name = "Loại giảm giá")]
        public string DiscountType { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá trị giảm")]
        [Range(0.01, 100000000, ErrorMessage = "Giá trị giảm không hợp lệ")]
        [Display(Name = "Giá trị giảm")]
        public decimal DiscountValue { get; set; }

        [Display(Name = "Giá trị đơn hàng tối thiểu")]
        public decimal MinOrderValue { get; set; }

        [Display(Name = "Giảm tối đa")]
        public decimal? MaxDiscountAmount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Số lượt sử dụng tối đa")]
        public int? MaxUsageCount { get; set; }

        [Display(Name = "Số lượt/khách hàng")]
        public int? MaxUsagePerCustomer { get; set; }

        [Display(Name = "Áp dụng cho")]
        public string ApplicableTo { get; set; }

        [Display(Name = "Danh sách ID áp dụng")]
        public string ApplicableIds { get; set; }

        [Display(Name = "Chỉ khách mới")]
        public bool IsNewCustomerOnly { get; set; }

        [Display(Name = "Happy Hour bắt đầu")]
        public TimeSpan? HappyHourStart { get; set; }

        [Display(Name = "Happy Hour kết thúc")]
        public TimeSpan? HappyHourEnd { get; set; }

        [Display(Name = "Áp dụng các ngày")]
        public string ApplicableDays { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }

        public bool IsEdit { get; set; }

        public PromotionFormViewModel()
        {
            StartDate = DateTime.Now;
            EndDate = DateTime.Now.AddMonths(1);
            DiscountType = "Percentage";
            ApplicableTo = "All";
            IsActive = true;
        }
    }

    /// <summary>
    /// ViewModel kết quả validate mã khuyến mãi
    /// </summary>
    public class PromotionValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public int? PromotionId { get; set; }
        public string PromotionName { get; set; }
        public string DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal CalculatedDiscount { get; set; }
        public decimal NewTotal { get; set; }
    }

    /// <summary>
    /// ViewModel cho báo cáo sử dụng khuyến mãi
    /// </summary>
    public class PromotionUsageReportViewModel
    {
        public int PromotionId { get; set; }
        public string PromotionCode { get; set; }
        public string PromotionName { get; set; }
        public List<PromotionUsageDetailViewModel> Usages { get; set; }
        public decimal TotalDiscountGiven { get; set; }
        public int TotalUsageCount { get; set; }
        public DateTime? FirstUsageDate { get; set; }
        public DateTime? LastUsageDate { get; set; }

        public PromotionUsageReportViewModel()
        {
            Usages = new List<PromotionUsageDetailViewModel>();
        }
    }

    /// <summary>
    /// Chi tiết mỗi lần sử dụng khuyến mãi
    /// </summary>
    public class PromotionUsageDetailViewModel
    {
        public int Id { get; set; }
        public int? BillId { get; set; }
        public string CustomerPhone { get; set; }
        public decimal DiscountApplied { get; set; }
        public DateTime UsedDate { get; set; }
        public string BillCode { get; set; }
    }
}
