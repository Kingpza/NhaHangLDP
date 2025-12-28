using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho cấu hình Email
    /// </summary>
    public class EmailConfigViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập SMTP Server")]
        [Display(Name = "SMTP Server")]
        public string SmtpServer { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập cổng SMTP")]
        [Range(1, 65535, ErrorMessage = "Cổng phải từ 1 đến 65535")]
        [Display(Name = "Cổng SMTP")]
        public int SmtpPort { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập username")]
        [Display(Name = "Tên đăng nhập")]
        public string SmtpUsername { get; set; }

        [Display(Name = "Mật khẩu")]
        public string SmtpPassword { get; set; }

        [Display(Name = "Sử dụng SSL")]
        public bool EnableSsl { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email gửi")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email gửi")]
        public string FromEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hiển thị")]
        [Display(Name = "Tên hiển thị")]
        public string FromName { get; set; }

        [Display(Name = "CC mặc định")]
        public string DefaultCc { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }

        public EmailConfigViewModel()
        {
            SmtpPort = 587;
            EnableSsl = true;
            IsActive = true;
        }
    }

    /// <summary>
    /// ViewModel cho template email
    /// </summary>
    public class EmailTemplateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã template")]
        [StringLength(50)]
        [Display(Name = "Mã Template")]
        public string TemplateCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên template")]
        [StringLength(100)]
        [Display(Name = "Tên Template")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề email")]
        [StringLength(200)]
        [Display(Name = "Tiêu đề Email")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung HTML")]
        [Display(Name = "Nội dung HTML")]
        public string HtmlBody { get; set; }

        [Display(Name = "Nội dung Text")]
        public string TextBody { get; set; }

        [Display(Name = "Các placeholder khả dụng")]
        public string AvailablePlaceholders { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }

        public bool IsEdit { get; set; }

        public EmailTemplateViewModel()
        {
            IsActive = true;
        }
    }

    /// <summary>
    /// ViewModel cho danh sách template
    /// </summary>
    public class EmailTemplateListViewModel
    {
        public List<EmailTemplateViewModel> Templates { get; set; }
        public int TotalTemplates { get; set; }
        public int ActiveTemplates { get; set; }

        public EmailTemplateListViewModel()
        {
            Templates = new List<EmailTemplateViewModel>();
        }
    }

    /// <summary>
    /// ViewModel cho log gửi email
    /// </summary>
    public class EmailLogViewModel
    {
        public int Id { get; set; }
        public string EmailType { get; set; }
        public string ToEmail { get; set; }
        public string ToName { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SentDate { get; set; }
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; }

        public string StatusText
        {
            get
            {
                switch (Status?.ToLower())
                {
                    case "sent": return "Đã gửi";
                    case "failed": return "Thất bại";
                    case "pending": return "Đang chờ";
                    case "retrying": return "Đang thử lại";
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
                    case "sent": return "success";
                    case "failed": return "danger";
                    case "pending": return "warning";
                    case "retrying": return "info";
                    default: return "secondary";
                }
            }
        }
    }

    /// <summary>
    /// ViewModel cho danh sách log email
    /// </summary>
    public class EmailLogListViewModel
    {
        public List<EmailLogViewModel> Logs { get; set; }
        public int TotalLogs { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public string StatusFilter { get; set; }
        public string TypeFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }

        public EmailLogListViewModel()
        {
            Logs = new List<EmailLogViewModel>();
            PageSize = 20;
            CurrentPage = 1;
        }
    }

    /// <summary>
    /// DTO để gửi email
    /// </summary>
    public class SendEmailDto
    {
        [Required(ErrorMessage = "Vui lòng nhập email người nhận")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string ToEmail { get; set; }

        public string ToName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        public string Body { get; set; }

        public bool IsHtml { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string EmailType { get; set; }
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; }

        public SendEmailDto()
        {
            IsHtml = true;
        }
    }

    /// <summary>
    /// DTO để gửi email từ template
    /// </summary>
    public class SendTemplateEmailDto
    {
        [Required]
        public string TemplateCode { get; set; }

        [Required]
        [EmailAddress]
        public string ToEmail { get; set; }

        public string ToName { get; set; }
        public Dictionary<string, string> Placeholders { get; set; }
        public int? ReferenceId { get; set; }
        public string ReferenceType { get; set; }

        public SendTemplateEmailDto()
        {
            Placeholders = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Response kết quả gửi email
    /// </summary>
    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? EmailLogId { get; set; }
        public DateTime? SentTime { get; set; }
        public string ErrorDetails { get; set; }
    }

    /// <summary>
    /// ViewModel cho gửi email test
    /// </summary>
    public class TestEmailViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email nhận test")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email nhận test")]
        public string TestEmail { get; set; }

        [Display(Name = "Tiêu đề")]
        public string Subject { get; set; }

        [Display(Name = "Nội dung")]
        public string Body { get; set; }

        public TestEmailViewModel()
        {
            Subject = "Email test từ hệ thống Nhà Hàng LDP";
            Body = "Đây là email test. Nếu bạn nhận được email này, cấu hình SMTP đã hoạt động đúng!";
        }
    }

    /// <summary>
    /// Các loại email predefined
    /// </summary>
    public static class EmailTypes
    {
        public const string BookingConfirmation = "BOOKING_CONFIRMATION";
        public const string BookingReminder = "BOOKING_REMINDER";
        public const string BookingCancellation = "BOOKING_CANCELLATION";
        public const string OrderConfirmation = "ORDER_CONFIRMATION";
        public const string OrderReady = "ORDER_READY";
        public const string PromotionNotification = "PROMOTION_NOTIFICATION";
        public const string Welcome = "WELCOME";
        public const string PasswordReset = "PASSWORD_RESET";
        public const string Feedback = "FEEDBACK";
        public const string Invoice = "INVOICE";
        public const string Newsletter = "NEWSLETTER";
        public const string Test = "TEST";
    }

    /// <summary>
    /// ViewModel cho cài đặt email tổng quan
    /// </summary>
    public class EmailSettingsViewModel
    {
        public EmailConfigViewModel Config { get; set; }
        public List<EmailTemplateViewModel> Templates { get; set; }
        public EmailLogListViewModel RecentLogs { get; set; }
        public EmailStatsViewModel Stats { get; set; }

        public EmailSettingsViewModel()
        {
            Config = new EmailConfigViewModel();
            Templates = new List<EmailTemplateViewModel>();
            RecentLogs = new EmailLogListViewModel();
            Stats = new EmailStatsViewModel();
        }
    }

    /// <summary>
    /// Thống kê email
    /// </summary>
    public class EmailStatsViewModel
    {
        public int TotalSentToday { get; set; }
        public int TotalSentThisWeek { get; set; }
        public int TotalSentThisMonth { get; set; }
        public int FailedToday { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, int> EmailsByType { get; set; }

        public EmailStatsViewModel()
        {
            EmailsByType = new Dictionary<string, int>();
        }
    }
}
