using System;
using System.Collections.Generic;
using System.Linq;
using NhaHangLDP.Models;
using NhaHangLDP.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    public class SettingsController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        
        private bool IsAuthorized()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole))
                return false;
            var role = userRole.ToLower();
            return role == "admin" || role == "manager";
        }
        
        private bool IsAdmin()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(userRole) && userRole.ToLower() == "admin";
        }
        
        public ActionResult Index()
        {
            if (!IsAuthorized())
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này. Vui lòng đăng nhập với tài khoản Admin hoặc Manager.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var allSettings = db.AppSetting.ToList();

                var viewModel = new SettingsViewModel
                {
                    RestaurantName = GetSettingValue(allSettings, "RestaurantName", " Restaurant"),
                    Address = GetSettingValue(allSettings, "Address", "123 Đường ABC, Quận 1, TP.HCM"),
                    PhoneNumber = GetSettingValue(allSettings, "PhoneNumber", "0123456789"),
                    Email = GetSettingValue(allSettings, "Email", "info@ldprestaurant.com"),
                    DefaultVAT = decimal.Parse(GetSettingValue(allSettings, "DefaultVAT", "10")),
                    OpenTime = TimeSpan.Parse(GetSettingValue(allSettings, "OpenTime", "06:00:00")),
                    CloseTime = TimeSpan.Parse(GetSettingValue(allSettings, "CloseTime", "22:00:00")),
                    Currency = GetSettingValue(allSettings, "Currency", "VNĐ"),
                    Language = GetSettingValue(allSettings, "Language", "vi-VN"),
                    MaxTables = int.Parse(GetSettingValue(allSettings, "MaxTables", "50")),
                    OrderTimeout = int.Parse(GetSettingValue(allSettings, "OrderTimeout", "30")),
                    AllowReservation = bool.Parse(GetSettingValue(allSettings, "AllowReservation", "true")),
                    MaintenanceMode = bool.Parse(GetSettingValue(allSettings, "MaintenanceMode", "false")),
                    MaintenanceMessage = GetSettingValue(allSettings, "MaintenanceMessage", "Hệ thống đang bảo trì, vui lòng thử lại sau."),
                    LogoUrl = GetSettingValue(allSettings, "LogoUrl", ""),
                    Website = GetSettingValue(allSettings, "Website", "")
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải cài đặt: " + ex.Message;
                return View(new SettingsViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(SettingsViewModel model)
        {
            if (!IsAuthorized())
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var currentUser = HttpContext.Session.GetString("Username") ?? "Admin";

                    UpsertSetting("RestaurantName", model.RestaurantName, "Tên nhà hàng", currentUser);
                    UpsertSetting("Address", model.Address, "Địa chỉ nhà hàng", currentUser);
                    UpsertSetting("PhoneNumber", model.PhoneNumber, "Số điện thoại liên hệ", currentUser);
                    UpsertSetting("Email", model.Email, "Email liên hệ", currentUser);
                    UpsertSetting("DefaultVAT", model.DefaultVAT.ToString(), "Thuế VAT mặc định (%)", currentUser);
                    UpsertSetting("OpenTime", model.OpenTime.ToString(), "Giờ mở cửa", currentUser);
                    UpsertSetting("CloseTime", model.CloseTime.ToString(), "Giờ đóng cửa", currentUser);
                    UpsertSetting("Currency", model.Currency, "Đơn vị tiền tệ", currentUser);
                    UpsertSetting("Language", model.Language, "Ngôn ngữ hệ thống", currentUser);
                    UpsertSetting("MaxTables", model.MaxTables.ToString(), "Số bàn tối đa", currentUser);
                    UpsertSetting("OrderTimeout", model.OrderTimeout.ToString(), "Thời gian chờ đơn hàng (phút)", currentUser);
                    UpsertSetting("AllowReservation", model.AllowReservation.ToString(), "Cho phép đặt bàn trước", currentUser);
                    UpsertSetting("MaintenanceMode", model.MaintenanceMode.ToString(), "Chế độ bảo trì", currentUser);
                    UpsertSetting("MaintenanceMessage", model.MaintenanceMessage ?? "", "Thông báo bảo trì", currentUser);
                    UpsertSetting("LogoUrl", model.LogoUrl ?? "", "URL logo nhà hàng", currentUser);
                    UpsertSetting("Website", model.Website ?? "", "Website nhà hàng", currentUser);

                    db.SaveChanges();

                    TempData["Success"] = "Cập nhật cài đặt thành công!";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Error"] = "Vui lòng kiểm tra lại thông tin đã nhập.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi lưu cài đặt: " + ex.Message;
            }

            return View(model);
        }

        public ActionResult System()
        {
            if (!IsAuthorized())
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var systemInfo = new SystemInfoViewModel
                {
                    SystemVersion = "1.0.0",
                    DatabaseVersion = "SQL Server 2019",
                    TotalOrders = db.Order.Count(),
                    TotalCustomers = db.Order.Select(o => o.Id).Distinct().Count(),
                    TotalRevenue = db.Bill.Where(b => b.Status == "Paid").Sum(b => (decimal?)b.FinalAmount) ?? 0,
                    LastStartupTime = DateTime.Now.AddHours(-2),
                    DatabaseSize = "128 MB",
                    TableCount = db.RestaurantTable.Count(),
                    EmployeeCount = db.Employee.Count(e => e.IsActive),
                    MenuItemCount = db.MenuItem.Count(m => m.IsAvailable)
                };

                return View(systemInfo);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin hệ thống: " + ex.Message;
                return View(new SystemInfoViewModel());
            }
        }

        public ActionResult Advanced()
        {
            if (!IsAdmin())
            {
                TempData["Error"] = "Chỉ Admin mới có quyền truy cập cài đặt nâng cao.";
                return RedirectToAction("Index");
            }

            try
            {
                var allSettings = db.AppSetting.ToList();

                var groups = new List<SettingsGroupViewModel>
                {
                    new SettingsGroupViewModel
                    {
                        GroupName = "Cài đặt hệ thống",
                        GroupDescription = "Các cài đặt cơ bản của hệ thống",
                        Settings = allSettings.Where(s => IsSystemSetting(s.SettingKey))
                            .Select(s => new AppSettingItem
                            {
                                Key = s.SettingKey,
                                Value = s.SettingValue,
                                Description = s.Description
                            }).ToList()
                    },
                    new SettingsGroupViewModel
                    {
                        GroupName = "Cài đặt bảo mật",
                        GroupDescription = "Các cài đặt liên quan đến bảo mật",
                        Settings = allSettings.Where(s => IsSecuritySetting(s.SettingKey))
                            .Select(s => new AppSettingItem
                            {
                                Key = s.SettingKey,
                                Value = s.SettingValue,
                                Description = s.Description
                            }).ToList()
                    }
                };

                return View(groups);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(new List<SettingsGroupViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetDefaults()
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Chỉ Admin mới có quyền reset cài đặt." });
            }

            try
            {
                var currentUser = HttpContext.Session.GetString("Username") ?? "Admin";

                UpsertSetting("RestaurantName", "LDP Restaurant", "Tên nhà hàng", currentUser);
                UpsertSetting("Address", "123 Đường ABC, Quận 1, TP.HCM", "Địa chỉ nhà hàng", currentUser);
                UpsertSetting("PhoneNumber", "0123456789", "Số điện thoại liên hệ", currentUser);
                UpsertSetting("Email", "info@ldprestaurant.com", "Email liên hệ", currentUser);
                UpsertSetting("DefaultVAT", "10", "Thuế VAT mặc định (%)", currentUser);
                UpsertSetting("Currency", "VNĐ", "Đơn vị tiền tệ", currentUser);
                UpsertSetting("Language", "vi-VN", "Ngôn ngữ hệ thống", currentUser);

                db.SaveChanges();

                return Json(new { success = true, message = "Đã reset về cài đặt mặc định!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #region Helper Methods
        private string GetSettingValue(List<AppSetting> settings, string key, string defaultValue)
        {
            var setting = settings.FirstOrDefault(s => s.SettingKey == key);
            return setting?.SettingValue ?? defaultValue;
        }

        private void UpsertSetting(string key, string value, string description, string updatedBy)
        {
            var existing = db.AppSetting.FirstOrDefault(s => s.SettingKey == key);

            if (existing != null)
            {
                existing.SettingValue = value;
                existing.UpdatedDate = DateTime.Now;
                existing.UpdatedBy = updatedBy;
            }
            else
            {
                var newSetting = new AppSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    Description = description,
                    CreatedDate = DateTime.Now,
                    CreatedBy = updatedBy
                };
                db.AppSetting.Add(newSetting);
            }
        }

        private bool IsSystemSetting(string key)
        {
            var systemKeys = new[] { "RestaurantName", "Address", "PhoneNumber", "Email", "Currency", "Language" };
            return systemKeys.Contains(key);
        }

        private bool IsSecuritySetting(string key)
        {
            var securityKeys = new[] { "MaintenanceMode", "OrderTimeout", "MaxTables" };
            return securityKeys.Contains(key);
        }

        #endregion
        
        public ActionResult Debug()
        {
            var debugInfo = new
            {
                UserRole = HttpContext.Session.GetString("UserRole") ?? "NULL",
                Username = HttpContext.Session.GetString("Username") ?? "NULL",
                UserId = HttpContext.Session.GetString("UserId") ?? "NULL",
                EmployeeId = HttpContext.Session.GetString("EmployeeId") ?? "NULL",
                FullName = HttpContext.Session.GetString("FullName") ?? "NULL",
                IsAuthenticated = User?.Identity?.IsAuthenticated ?? false,
                AuthenticationType = User?.Identity?.AuthenticationType ?? "NULL",
                SessionId = HttpContext.Session.Id,
                AllSessionKeys = HttpContext.Session.Keys.ToArray()
            };

            ViewBag.DebugInfo = debugInfo;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}