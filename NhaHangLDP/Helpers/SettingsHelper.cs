using System;
using System.Linq;

using NhaHangLDP.Models;

namespace NhaHangLDP.Helpers
{
    /// <summary>
    /// Helper class để truy xuất cài đặt hệ thống từ bất kỳ đâu trong ứng dụng
    /// </summary>
  public static class SettingsHelper
    {
        private static readonly object _lock = new object();
   private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(5); // Cache trong 5 phút
        
  /// <summary>
   /// Lấy giá trị cài đặt theo key
        /// </summary>
        /// <param name="key">Khóa cài đặt</param>
        /// <param name="defaultValue">Giá trị mặc định nếu không tìm thấy</param>
 /// <returns>Giá trị cài đặt</returns>
        public static string GetSetting(string key, string defaultValue = "")
        {
            try
      {
   using (var db = new MyDbContext())
     {
          var setting = db.AppSettings.FirstOrDefault(s => s.SettingKey == key);
     return setting?.SettingValue ?? defaultValue;
     }
    }
            catch (Exception)
 {
  return defaultValue;
 }
        }
        
     /// <summary>
        /// Lấy giá trị cài đặt dạng số nguyên
      /// </summary>
      /// <param name="key">Khóa cài đặt</param>
        /// <param name="defaultValue">Giá trị mặc định</param>
        /// <returns>Giá trị số nguyên</returns>
     public static int GetIntSetting(string key, int defaultValue = 0)
    {
         var value = GetSetting(key);
       return int.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Lấy giá trị cài đặt dạng decimal
 /// </summary>
        /// <param name="key">Khóa cài đặt</param>
        /// <param name="defaultValue">Giá trị mặc định</param>
        /// <returns>Giá trị decimal</returns>
        public static decimal GetDecimalSetting(string key, decimal defaultValue = 0)
   {
            var value = GetSetting(key);
    return decimal.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
     /// Lấy giá trị cài đặt dạng boolean
        /// </summary>
        /// <param name="key">Khóa cài đặt</param>
      /// <param name="defaultValue">Giá trị mặc định</param>
        /// <returns>Giá trị boolean</returns>
 public static bool GetBoolSetting(string key, bool defaultValue = false)
   {
         var value = GetSetting(key);
      return bool.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Lấy giá trị cài đặt dạng TimeSpan
        /// </summary>
        /// <param name="key">Khóa cài đặt</param>
   /// <param name="defaultValue">Giá trị mặc định</param>
        /// <returns>Giá trị TimeSpan</returns>
        public static TimeSpan GetTimeSetting(string key, TimeSpan defaultValue)
        {
     var value = GetSetting(key);
            return TimeSpan.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Cập nhật hoặc tạo mới một cài đặt
        /// </summary>
        /// <param name="key">Khóa cài đặt</param>
        /// <param name="value">Giá trị</param>
        /// <param name="description">Mô tả</param>
        /// <param name="updatedBy">Người cập nhật</param>
        /// <returns>True nếu thành công</returns>
        public static bool UpdateSetting(string key, string value, string description = "", string updatedBy = "System")
        {
   try
        {
          using (var db = new MyDbContext())
                {
                    var existing = db.AppSettings.FirstOrDefault(s => s.SettingKey == key);
 
    if (existing != null)
             {
      // Cập nhật
   existing.SettingValue = value;
             existing.UpdatedDate = DateTime.Now;
              existing.UpdatedBy = updatedBy;
}
          else
            {
           // Tạo mới
 var newSetting = new AppSetting
 {
     SettingKey = key,
     SettingValue = value,
     Description = description,
  CreatedDate = DateTime.Now,
      CreatedBy = updatedBy
     };
           db.AppSettings.Add(newSetting);
           }
 
      db.SaveChanges();
   return true;
    }
   }
            catch (Exception)
        {
          return false;
     }
        }
     
        // Các property tiện lợi để truy cập nhanh các cài đặt thường dùng
        public static string RestaurantName => GetSetting("RestaurantName", "LDP Restaurant");
        public static string Address => GetSetting("Address", "123 Đường ABC, Quận 1, TP.HCM");
        public static string PhoneNumber => GetSetting("PhoneNumber", "0123456789");
    public static string Email => GetSetting("Email", "info@ldprestaurant.com");
        public static decimal DefaultVAT => GetDecimalSetting("DefaultVAT", 10);
        public static string Currency => GetSetting("Currency", "VNĐ");
   public static string Language => GetSetting("Language", "vi-VN");
        public static TimeSpan OpenTime => GetTimeSetting("OpenTime", new TimeSpan(6, 0, 0));
      public static TimeSpan CloseTime => GetTimeSetting("CloseTime", new TimeSpan(22, 0, 0));
        public static int MaxTables => GetIntSetting("MaxTables", 50);
        public static int OrderTimeout => GetIntSetting("OrderTimeout", 30);
     public static bool AllowReservation => GetBoolSetting("AllowReservation", true);
        public static bool MaintenanceMode => GetBoolSetting("MaintenanceMode", false);
      public static string MaintenanceMessage => GetSetting("MaintenanceMessage", "Hệ thống đang bảo trì, vui lòng thử lại sau.");
        public static string LogoUrl => GetSetting("LogoUrl", "");
        public static string Website => GetSetting("Website", "");
        
        /// <summary>
        /// Kiểm tra xem nhà hàng có đang mở cửa không
        /// </summary>
        /// <returns>True nếu đang mở cửa</returns>
public static bool IsRestaurantOpen()
   {
    if (MaintenanceMode)
             return false;
                
 var currentTime = DateTime.Now.TimeOfDay;
  var openTime = OpenTime;
   var closeTime = CloseTime;
    
  // Xử lý trường hợp mở cửa qua đêm (ví dụ: 22:00 - 06:00)
if (closeTime < openTime)
            {
          return currentTime >= openTime || currentTime <= closeTime;
    }
else
            {
         return currentTime >= openTime && currentTime <= closeTime;
            }
  }
        
        /// <summary>
   /// Lấy thông tin đầy đủ về nhà hàng
        /// </summary>
      /// <returns>Object chứa thông tin nhà hàng</returns>
    public static dynamic GetRestaurantInfo()
        {
     return new
      {
         Name = RestaurantName,
       Address = Address,
         Phone = PhoneNumber,
          Email = Email,
        Website = Website,
   LogoUrl = LogoUrl,
   OpenTime = OpenTime.ToString(@"hh\:mm"),
CloseTime = CloseTime.ToString(@"hh\:mm"),
    Currency = Currency,
        DefaultVAT = DefaultVAT,
         IsOpen = IsRestaurantOpen(),
                MaintenanceMode = MaintenanceMode,
          MaintenanceMessage = MaintenanceMessage
            };
        }
    }
}