using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    public class SettingsViewModel
    {
    [Display(Name = "Tên nhà hàng")]
     [Required(ErrorMessage = "Tên nhà hàng không được để trống")]
     [StringLength(200, ErrorMessage = "Tên nhà hàng không được vượt quá 200 ký tự")]
        public string RestaurantName { get; set; }

        [Display(Name = "Địa chỉ")]
        [Required(ErrorMessage = "Địa chỉ không được để trống")]
      [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
        public string Address { get; set; }

  [Display(Name = "Số điện thoại")]
        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
     [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Email liên hệ")]
     [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự")]
        public string Email { get; set; }

      [Display(Name = "Thuế VAT (%)")]
        [Required(ErrorMessage = "Thuế VAT không được để trống")]
     [Range(0, 100, ErrorMessage = "Thuế VAT phải từ 0% đến 100%")]
        public decimal DefaultVAT { get; set; }

        [Display(Name = "Giờ mở cửa")]
      [Required(ErrorMessage = "Giờ mở cửa không được để trống")]
        public TimeSpan OpenTime { get; set; }

     [Display(Name = "Giờ đóng cửa")]
   [Required(ErrorMessage = "Giờ đóng cửa không được để trống")]
   public TimeSpan CloseTime { get; set; }

        [Display(Name = "Tiền tệ")]
        [Required(ErrorMessage = "Tiền tệ không được để trống")]
    [StringLength(10, ErrorMessage = "Tiền tệ không được vượt quá 10 ký tự")]
        public string Currency { get; set; }

   [Display(Name = "Ngôn ngữ")]
        [Required(ErrorMessage = "Ngôn ngữ không được để trống")]
        [StringLength(10, ErrorMessage = "Ngôn ngữ không được vượt quá 10 ký tự")]
        public string Language { get; set; }

   [Display(Name = "Số bàn tối đa")]
        [Required(ErrorMessage = "Số bàn tối đa không được để trống")]
        [Range(1, 1000, ErrorMessage = "Số bàn tối đa phải từ 1 đến 1000")]
      public int MaxTables { get; set; }

    [Display(Name = "Thời gian chờ đơn hàng (phút)")]
        [Required(ErrorMessage = "Thời gian chờ không được để trống")]
   [Range(1, 120, ErrorMessage = "Thời gian chờ phải từ 1 đến 120 phút")]
        public int OrderTimeout { get; set; }

        [Display(Name = "Cho phép đặt bàn trước")]
        public bool AllowReservation { get; set; }

        [Display(Name = "Bật chế độ bảo trì")]
 public bool MaintenanceMode { get; set; }

      [Display(Name = "Thông báo bảo trì")]
        [StringLength(1000, ErrorMessage = "Thông báo bảo trì không được vượt quá 1000 ký tự")]
      public string MaintenanceMessage { get; set; }

  [Display(Name = "Logo nhà hàng (URL)")]
        [Url(ErrorMessage = "URL không hợp lệ")]
        [StringLength(500, ErrorMessage = "URL không được vượt quá 500 ký tự")]
        public string LogoUrl { get; set; }

  [Display(Name = "Website")]
        [Url(ErrorMessage = "URL website không hợp lệ")]
        [StringLength(200, ErrorMessage = "URL website không được vượt quá 200 ký tự")]
 public string Website { get; set; }
    }

    public class SettingsGroupViewModel
  {
        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
     public List<AppSettingItem> Settings { get; set; }

        public SettingsGroupViewModel()
        {
            Settings = new List<AppSettingItem>();
      }
    }

    public class AppSettingItem
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
public string DataType { get; set; } // "string", "number", "boolean", "time", "url"
   public bool IsRequired { get; set; }
        public string ValidationRule { get; set; }
    }

    public class SystemInfoViewModel
    {
    [Display(Name = "Phiên bản hệ thống")]
        public string SystemVersion { get; set; }

        [Display(Name = "Cơ sở dữ liệu")]
        public string DatabaseVersion { get; set; }

   [Display(Name = "Tổng số đơn hàng")]
   public int TotalOrders { get; set; }

        [Display(Name = "Tổng số khách hàng")]
    public int TotalCustomers { get; set; }

        [Display(Name = "Tổng doanh thu")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Ngày khởi động cuối")]
        public DateTime LastStartupTime { get; set; }

      [Display(Name = "Dung lượng cơ sở dữ liệu")]
        public string DatabaseSize { get; set; }

      [Display(Name = "Số lượng bảng")]
        public int TableCount { get; set; }

        [Display(Name = "Số lượng nhân viên")]
        public int EmployeeCount { get; set; }

 [Display(Name = "Số lượng món ăn")]
     public int MenuItemCount { get; set; }
    }
}