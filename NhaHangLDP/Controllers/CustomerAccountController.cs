using NhaHangLDP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller xác thực khách hàng
    /// </summary>
    public class CustomerAccountController : Controller
    {
        private readonly MyDbContext _db = new MyDbContext();

        #region Login

        /// <summary>
        /// Trang đăng nhập
        /// </summary>
        public ActionResult Login(string returnUrl = null)
        {
            if (IsCustomerLoggedIn())
            {
                return RedirectToAction("Profile");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        /// <summary>
        /// Xử lý đăng nhập
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Find customer by email or phone using LINQ
                var customer = _db.Customers
                    .Where(c => (c.Email == model.EmailOrPhone || c.Phone == model.EmailOrPhone) && c.IsActive)
                    .Select(c => new CustomerLoginInfo
                    {
                        Id = c.Id,
                        FullName = c.FullName,
                        Email = c.Email,
                        Phone = c.Phone,
                        PasswordHash = c.PasswordHash,
                        IsActive = c.IsActive
                    })
                    .FirstOrDefault();

                if (customer == null)
                {
                    ModelState.AddModelError("", "Email/SĐT hoặc mật khẩu không đúng!");
                    return View(model);
                }

                // Verify password
                if (!VerifyPassword(model.Password, customer.PasswordHash))
                {
                    ModelState.AddModelError("", "Email/SĐT hoặc mật khẩu không đúng!");
                    return View(model);
                }

                // Set session - lưu đầy đủ thông tin khách hàng
                HttpContext.Session.SetString("CustomerId", customer.Id.ToString() ?? "");
                HttpContext.Session.SetString("CustomerName", customer.FullName?.ToString() ?? "");
                HttpContext.Session.SetString("CustomerEmail", customer.Email?.ToString() ?? "");
                HttpContext.Session.SetString("CustomerPhone", customer.Phone?.ToString() ?? "");

                // Update last login using EF Core
                var dbCustomer = _db.Customers.Find(customer.Id);
                if (dbCustomer != null)
                {
                    dbCustomer.LastLoginDate = DateTime.Now;
                    _db.SaveChanges();
                }

                // Set auth cookie if remember me
                if (model.RememberMe)
                {
                    // TODO ASP.NET membership should be replaced with ASP.NET Core identity. For more details see https://docs.microsoft.com/aspnet/core/migration/proper-to-2x/membership-to-core-identity.
                    }

                // Redirect
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Menu", "Public");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi: " + ex.Message);
                return View(model);
            }
        }

        #endregion

        #region Register

        /// <summary>
        /// Trang đăng ký
        /// </summary>
        public ActionResult Register()
        {
            if (IsCustomerLoggedIn())
            {
                return RedirectToAction("Profile");
            }

            return View(new RegisterViewModel());
        }

        /// <summary>
        /// Xử lý đăng ký
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                if (!model.AgreeTerms)
                {
                    ModelState.AddModelError("", "Bạn cần đồng ý với điều khoản dịch vụ!");
                    return View(model);
                }

                // Check existing email
                var existingEmail = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM Customer WHERE Email = @p0",
                    model.Email).FirstOrDefault();

                if (existingEmail > 0)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng!");
                    return View(model);
                }

                // Check existing phone
                var existingPhone = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM Customer WHERE Phone = @p0",
                    model.Phone).FirstOrDefault();

                if (existingPhone > 0)
                {
                    ModelState.AddModelError("Phone", "Số điện thoại này đã được sử dụng!");
                    return View(model);
                }

                // Hash password
                var passwordHash = HashPassword(model.Password);

                // Create customer
                var sql = @"
                    INSERT INTO Customer (FullName, Email, Phone, PasswordHash, MembershipLevel, LoyaltyPoints, IsActive, EmailVerified, CreatedDate)
                    VALUES (@p0, @p1, @p2, @p3, 'Bronze', 0, 1, 0, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                var customerId = _db.Database.SqlQuery<decimal>(sql,
                    model.FullName,
                    model.Email,
                    model.Phone,
                    passwordHash).FirstOrDefault();

                // Auto login - lưu đầy đủ thông tin khách hàng
                HttpContext.Session.SetString("CustomerId", ((int)customerId).ToString());
                HttpContext.Session.SetString("CustomerName", model.FullName ?? "");
                HttpContext.Session.SetString("CustomerEmail", model.Email ?? "");
                HttpContext.Session.SetString("CustomerPhone", model.Phone ?? "");

                TempData["Success"] = "Đăng ký thành công! Chào mừng bạn đến với Nhà Hàng LDP.";
                return RedirectToAction("Menu", "Public");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi: " + ex.Message);
                return View(model);
            }
        }

        #endregion

        #region Logout

        /// <summary>
        /// Đăng xuất
        /// </summary>
        public ActionResult Logout()
        {
            HttpContext.Session.Clear();
            // TODO ASP.NET membership should be replaced with ASP.NET Core identity. For more details see https://docs.microsoft.com/aspnet/core/migration/proper-to-2x/membership-to-core-identity.
            return RedirectToAction("Menu", "Public");
        }

        #endregion

        #region Forgot Password

        /// <summary>
        /// Trang quên mật khẩu
        /// </summary>
        public ActionResult ForgotPassword()
        {
            if (IsCustomerLoggedIn())
            {
                return RedirectToAction("Profile");
            }

            return View(new ForgotPasswordViewModel());
        }

        /// <summary>
        /// Xử lý quên mật khẩu
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Check if email exists
                var customer = _db.Set<CustomerLoginInfo>().FromSqlRaw(@"SELECT Id, FullName, Email, Phone, PasswordHash, IsActive FROM Customer WHERE Email = @p0 AND IsActive = 1",
                    model.Email).FirstOrDefault();

                if (customer == null)
                {
                    // For security, don't reveal if email exists or not
                    TempData["Success"] = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được hướng dẫn đặt lại mật khẩu.";
                    return View(model);
                }

                // Generate reset token (in production, you would send this via email)
                var resetToken = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                
                // For demo purposes, we'll just show the token
                // In production, send email with reset link
                TempData["Success"] = $"Mã đặt lại mật khẩu của bạn là: {resetToken} (Demo only - trong thực tế sẽ gửi qua email)";
                
                // You could store the token in database for verification later
                // _db.Database.ExecuteSqlRaw(
                //     "UPDATE Customer SET ResetToken = @p1, ResetTokenExpiry = DATEADD(HOUR, 1, GETDATE()) WHERE Id = @p0",
                //     customer.Id, HashPassword(resetToken));

                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi: " + ex.Message);
                return View(model);
            }
        }

        #endregion

        #region Profile

        /// <summary>
        /// Trang cá nhân
        /// </summary>
        public ActionResult Profile()
        {
            var customerId = GetCustomerId();
            if (customerId == null)
            {
                return RedirectToAction("Login");
            }

            var customer = GetCustomerProfile(customerId.Value);
            if (customer == null)
            {
                return RedirectToAction("Login");
            }

            // Load addresses
            customer.Addresses = GetCustomerAddresses(customerId.Value);

            return View(customer);
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string fullName, string phone, DateTime? dateOfBirth, string gender)
        {
            try
            {
                var customerId = GetCustomerId();
                if (customerId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                _db.Database.ExecuteSqlRaw(
                    @"UPDATE Customer SET FullName = @p1, Phone = @p2, DateOfBirth = @p3, Gender = @p4 WHERE Id = @p0",
                    customerId, fullName, phone, dateOfBirth, gender);

                HttpContext.Session.SetString("CustomerName", fullName?.ToString() ?? "");

                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lịch sử đơn hàng
        /// </summary>
        public ActionResult Orders(int page = 1)
        {
            var customerId = GetCustomerId();
            if (customerId == null)
            {
                return RedirectToAction("Login");
            }

            var pageSize = 10;
            var orders = _db.Set<OrderSummaryInfo>().FromSqlRaw(@"SELECT o.Id, o.OrderCode, o.OrderDate, o.Status, o.TotalAmount,
                         (SELECT COUNT(*) FROM OrderDetail WHERE OrderId = o.Id) as ItemCount,
                         (SELECT TOP 1 m.ImageUrl FROM OrderDetail od 
                          JOIN MenuItem m ON od.MenuItemId = m.Id WHERE od.OrderId = o.Id) as FirstItemImage
                  FROM CustomerOrder o
                  WHERE o.CustomerId = @p0
                  ORDER BY o.OrderDate DESC
                  OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY",
                customerId, (page - 1) * pageSize, pageSize).ToList();

            var totalOrders = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM CustomerOrder WHERE CustomerId = @p0",
                customerId).FirstOrDefault();

            var viewModel = new OrderListViewModel
            {
                Orders = orders.Select(o => new OrderSummaryItem
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    StatusClass = GetOrderStatusClass(o.Status),
                    StatusText = GetOrderStatusText(o.Status),
                    TotalAmount = o.TotalAmount,
                    ItemCount = o.ItemCount,
                    FirstItemImage = o.FirstItemImage,
                    CanCancel = o.Status == "Pending",
                    CanReview = o.Status == "Completed"
                }).ToList(),
                TotalOrders = totalOrders,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalOrders / pageSize)
            };

            return View(viewModel);
        }

        /// <summary>
        /// Chi tiết đơn hàng
        /// </summary>
        public ActionResult OrderDetail(string code)
        {
            var customerId = GetCustomerId();
            if (customerId == null)
            {
                return RedirectToAction("Login");
            }

            // Verify ownership
            var isOwner = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM CustomerOrder WHERE OrderCode = @p0 AND CustomerId = @p1",
                code, customerId).FirstOrDefault();

            if (isOwner == 0)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Orders");
            }

            return RedirectToAction("TrackOrder", "Checkout", new { code = code });
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            try
            {
                var customerId = GetCustomerId();
                if (customerId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
                }

                // Verify current password
                var currentHash = _db.Set<string>().FromSqlRaw(@"SELECT PasswordHash FROM Customer WHERE Id = @p0",
                    customerId).FirstOrDefault();

                if (!VerifyPassword(model.CurrentPassword, currentHash))
                {
                    return Json(new { success = false, message = "Mật khẩu hiện tại không đúng!" });
                }

                // Update password
                var newHash = HashPassword(model.NewPassword);
                _db.Database.ExecuteSqlRaw(
                    "UPDATE Customer SET PasswordHash = @p1 WHERE Id = @p0",
                    customerId, newHash);

                return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Wishlist

        /// <summary>
        /// Danh sách yêu thích
        /// </summary>
        public ActionResult Wishlist()
        {
            var customerId = GetCustomerId();
            if (customerId == null)
            {
                return RedirectToAction("Login");
            }

            var items = _db.Set<WishlistItemInfo>().FromSqlRaw(@"SELECT w.Id, w.MenuItemId, m.Name, m.Price, m.ImageUrl, m.Category, w.AddedDate
                  FROM Wishlist w
                  JOIN MenuItem m ON w.MenuItemId = m.Id
                  WHERE w.CustomerId = @p0
                  ORDER BY w.AddedDate DESC",
                customerId).ToList();

            var viewModel = items.Select(i => new WishlistItemViewModel
            {
                Id = i.Id,
                MenuItemId = i.MenuItemId,
                Name = i.Name,
                Price = i.Price,
                ImageUrl = i.ImageUrl,
                Category = i.Category,
                AddedDate = i.AddedDate
            }).ToList();

            return View(viewModel);
        }

        /// <summary>
        /// Toggle wishlist
        /// </summary>
        [HttpPost]
        public JsonResult ToggleWishlist(int menuItemId)
        {
            try
            {
                var customerId = GetCustomerId();
                if (customerId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!", needLogin = true });
                }

                var exists = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM Wishlist WHERE CustomerId = @p0 AND MenuItemId = @p1",
                    customerId, menuItemId).FirstOrDefault();

                if (exists > 0)
                {
                    _db.Database.ExecuteSqlRaw(
                        "DELETE FROM Wishlist WHERE CustomerId = @p0 AND MenuItemId = @p1",
                        customerId, menuItemId);
                    return Json(new { success = true, added = false, message = "Đã xóa khỏi yêu thích" });
                }
                else
                {
                    _db.Database.ExecuteSqlRaw(
                        "INSERT INTO Wishlist (CustomerId, MenuItemId, AddedDate) VALUES (@p0, @p1, GETDATE())",
                        customerId, menuItemId);
                    return Json(new { success = true, added = true, message = "Đã thêm vào yêu thích" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Address Management

        /// <summary>
        /// Lấy danh sách địa chỉ của khách hàng
        /// </summary>
        private List<CustomerAddressViewModel> GetCustomerAddresses(int customerId)
        {
            try
            {
                var addresses = _db.Set<CustomerAddressInfo>().FromSqlRaw(@"SELECT Id, ReceiverName, ReceiverPhone, AddressLine, Ward, District, City, AddressType, IsDefault
                      FROM CustomerAddress
                      WHERE CustomerId = @p0 AND IsDeleted = 0
                      ORDER BY IsDefault DESC, Id DESC",
                    customerId).ToList();

                return addresses.Select(a => new CustomerAddressViewModel
                {
                    Id = a.Id,
                    ReceiverName = a.ReceiverName,
                    ReceiverPhone = a.ReceiverPhone,
                    AddressLine = a.AddressLine,
                    Ward = a.Ward,
                    District = a.District,
                    City = a.City,
                    AddressType = a.AddressType,
                    IsDefault = a.IsDefault,
                    FullAddress = $"{a.AddressLine}, {a.Ward}, {a.District}, {a.City}"
                }).ToList();
            }
            catch
            {
                return new List<CustomerAddressViewModel>();
            }
        }

        /// <summary>
        /// Lấy thông tin một địa chỉ
        /// </summary>
        [HttpGet]
        public JsonResult GetAddress(int id)
        {
            try
            {
                var customerId = GetCustomerId();
                if (customerId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                var address = _db.Set<CustomerAddressInfo>().FromSqlRaw(@"SELECT Id, ReceiverName, ReceiverPhone, AddressLine, Ward, District, City, AddressType, IsDefault
                      FROM CustomerAddress
                      WHERE Id = @p0 AND CustomerId = @p1 AND IsDeleted = 0",
                    id, customerId).FirstOrDefault();

                if (address == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy địa chỉ!" });
                }

                return Json(new { success = true, data = address });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lưu địa chỉ (thêm mới hoặc cập nhật)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveAddress(int addressId, string receiverName, string receiverPhone, 
            string addressLine, string ward, string district, string city, string addressType, bool isDefault = false)
        {
            try
            {
                var customerId = GetCustomerId();
                if (customerId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                if (isDefault)
                {
                    // Reset all other addresses to non-default
                    _db.Database.ExecuteSqlRaw(
                        "UPDATE CustomerAddress SET IsDefault = 0 WHERE CustomerId = @p0",
                        customerId);
                }

                if (addressId == 0)
                {
                    // Insert new address
                    _db.Database.ExecuteSqlRaw(
                        @"INSERT INTO CustomerAddress (CustomerId, ReceiverName, ReceiverPhone, AddressLine, Ward, District, City, AddressType, IsDefault, IsDeleted, CreatedDate)
                          VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, 0, GETDATE())",
                        customerId, receiverName, receiverPhone, addressLine, ward, district, city, addressType, isDefault);
            
            return Json(new { success = true, message = "Thêm địa chỉ thành công!" });
        }
        else
        {
            // Update existing address
            _db.Database.ExecuteSqlRaw(
                @"UPDATE CustomerAddress 
                  SET ReceiverName = @p1, ReceiverPhone = @p2, AddressLine = @p3, Ward = @p4, District = @p5, City = @p6, AddressType = @p7, IsDefault = @p8
                  WHERE Id = @p0 AND CustomerId = @p9",
                addressId, receiverName, receiverPhone, addressLine, ward, district, city, addressType, isDefault, customerId);
            
            return Json(new { success = true, message = "Cập nhật địa chỉ thành công!" });
        }
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

/// <summary>
/// Đặt địa chỉ mặc định
/// </summary>
[HttpPost]
public JsonResult SetDefaultAddress(int id)
{
    try
    {
        var customerId = GetCustomerId();
        if (customerId == null)
        {
            return Json(new { success = false, message = "Vui lòng đăng nhập!" });
        }

        // Reset all addresses to non-default
        _db.Database.ExecuteSqlRaw(
            "UPDATE CustomerAddress SET IsDefault = 0 WHERE CustomerId = @p0",
            customerId);

        // Set the selected one as default
        _db.Database.ExecuteSqlRaw(
            "UPDATE CustomerAddress SET IsDefault = 1 WHERE Id = @p0 AND CustomerId = @p1",
            id, customerId);

        return Json(new { success = true, message = "Đã đặt làm địa chỉ mặc định!" });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

/// <summary>
/// Xóa địa chỉ
/// </summary>
[HttpPost]
public JsonResult DeleteAddress(int id)
{
    try
    {
        var customerId = GetCustomerId();
        if (customerId == null)
        {
            return Json(new { success = false, message = "Vui lòng đăng nhập!" });
        }

        // Check if it's default address
        var isDefault = _db.Set<bool>().FromSqlRaw(@"SELECT IsDefault FROM CustomerAddress WHERE Id = @p0 AND CustomerId = @p1",
            id, customerId).FirstOrDefault();

        if (isDefault)
        {
            return Json(new { success = false, message = "Không thể xóa địa chỉ mặc định!" });
        }

        // Soft delete
        _db.Database.ExecuteSqlRaw(
            "UPDATE CustomerAddress SET IsDeleted = 1 WHERE Id = @p0 AND CustomerId = @p1",
            id, customerId);

        return Json(new { success = true, message = "Đã xóa địa chỉ!" });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

/// <summary>
/// Xóa tài khoản
/// </summary>
[HttpPost]
public JsonResult DeleteAccount()
{
    try
    {
        var customerId = GetCustomerId();
        if (customerId == null)
        {
            return Json(new { success = false, message = "Vui lòng đăng nhập!" });
        }

        // Soft delete customer
        _db.Database.ExecuteSqlRaw(
            "UPDATE Customer SET IsActive = 0, Email = CONCAT(Email, '_deleted_', @p0) WHERE Id = @p0",
            customerId);

        // Clear session
        HttpContext.Session.Clear();
        // TODO ASP.NET membership should be replaced with ASP.NET Core identity. For more details see https://docs.microsoft.com/aspnet/core/migration/proper-to-2x/membership-to-core-identity.
        return Json(new { success = true, message = "Tài khoản đã bị xóa!" });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

#endregion

#region My Reservations

/// <summary>
/// Trang danh sách đặt bàn của tôi
/// </summary>
public ActionResult MyReservations()
{
    var customerId = GetCustomerId();
    if (customerId == null)
    {
        return RedirectToAction("Login", new { returnUrl = Url.Action("MyReservations") });
    }

    var reservations = _db.Set<MyReservationInfo>().FromSqlRaw(@"SELECT r.Id, r.ReservationCode, r.CustomerName, r.CustomerPhone, r.CustomerEmail,
                 r.ReservationDate, r.ReservationTime, r.NumberOfGuests, r.TablePreference,
                 r.SpecialRequests, r.Status, r.CreatedDate, t.TableNumber as TableName
          FROM Reservation r
          LEFT JOIN RestaurantTable t ON r.TableId = t.Id
          WHERE r.CustomerId = @p0
          ORDER BY r.ReservationDate DESC, r.ReservationTime DESC",
        customerId).ToList();

    var viewModel = reservations.Select(r => new MyReservationViewModel
    {
        Id = r.Id,
        ReservationCode = r.ReservationCode,
        CustomerName = r.CustomerName,
        CustomerPhone = r.CustomerPhone,
        ReservationDate = r.ReservationDate,
        ReservationTime = r.ReservationTime,
        NumberOfGuests = r.NumberOfGuests,
        TablePreference = r.TablePreference,
        TableName = r.TableName,
        SpecialRequests = r.SpecialRequests,
        Status = r.Status,
        StatusClass = GetReservationStatusClass(r.Status),
        StatusText = GetReservationStatusText(r.Status),
        CanCancel = r.Status == "Pending" || r.Status == "Confirmed",
        CanModify = r.Status == "Pending",
        CreatedDate = r.CreatedDate,
        IsUpcoming = r.ReservationDate >= DateTime.Today && (r.Status == "Pending" || r.Status == "Confirmed")
    }).ToList();

    return View(viewModel);
}

/// <summary>
/// Hủy đặt bàn
/// </summary>
[HttpPost]
public JsonResult CancelReservation(string code, string reason)
{
    try
    {
        var customerId = GetCustomerId();
        if (customerId == null)
        {
            return Json(new { success = false, message = "Vui lòng đăng nhập!" });
        }

        // Verify ownership
        var isOwner = _db.Set<int>().FromSqlRaw(@"SELECT COUNT(*) FROM Reservation WHERE ReservationCode = @p0 AND CustomerId = @p1",
            code, customerId).FirstOrDefault();

        if (isOwner == 0)
        {
            return Json(new { success = false, message = "Không tìm thấy đặt bàn!" });
        }

        var sql = @"
            UPDATE Reservation 
            SET Status = 'Cancelled', CancelReason = @p1, CancelledDate = GETDATE()
            WHERE ReservationCode = @p0 AND Status IN ('Pending', 'Confirmed')";

        var affected = _db.Database.ExecuteSqlRaw(sql, code, reason ?? "Khách hàng hủy");

        if (affected > 0)
        {
            return Json(new { success = true, message = "Đã hủy đặt bàn thành công!" });
        }
        else
        {
            return Json(new { success = false, message = "Không thể hủy đặt bàn này!" });
        }
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = ex.Message });
    }
}

private string GetReservationStatusClass(string status)
{
    switch (status)
    {
        case "Pending": return "warning";
        case "Confirmed": return "success";
        case "Completed": return "info";
        case "Cancelled": return "danger";
        case "NoShow": return "secondary";
        default: return "secondary";
    }
}

private string GetReservationStatusText(string status)
{
    switch (status)
    {
        case "Pending": return "Chờ xác nhận";
        case "Confirmed": return "Đã xác nhận";
        case "Completed": return "Hoàn thành";
        case "Cancelled": return "Đã hủy";
        case "NoShow": return "Không đến";
        default: return status;
    }
}

private class MyReservationInfo
{
    public int Id { get; set; }
    public string ReservationCode { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string CustomerEmail { get; set; }
    public DateTime ReservationDate { get; set; }
    public TimeSpan ReservationTime { get; set; }
    public int NumberOfGuests { get; set; }
    public string TablePreference { get; set; }
    public string SpecialRequests { get; set; }
    public string Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public string TableName { get; set; }
}

#endregion

#region Helper Methods

private bool IsCustomerLoggedIn()
{
    return HttpContext.Session.GetString("CustomerId") != null;
}

private int? GetCustomerId()
{
    return (int.TryParse(HttpContext.Session.GetString("CustomerId"), out int _pCustomerId) ? (int?)_pCustomerId : null);
}

private CustomerProfileViewModel GetCustomerProfile(int customerId)
{
    try
    {
        var customer = _db.Set<CustomerProfileInfo>().FromSqlRaw(@"SELECT c.Id, c.FullName, c.Email, c.Phone, c.DateOfBirth, c.Gender, c.AvatarUrl,
                     c.LoyaltyPoints, c.MembershipLevel, c.CreatedDate,
                     (SELECT COUNT(*) FROM CustomerOrder WHERE CustomerId = c.Id) as TotalOrders,
                     (SELECT ISNULL(SUM(TotalAmount), 0) FROM CustomerOrder WHERE CustomerId = c.Id AND Status = 'Completed') as TotalSpent
              FROM Customer c
              WHERE c.Id = @p0", customerId).FirstOrDefault();

        if (customer == null) return null;

        return new CustomerProfileViewModel
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email,
            Phone = customer.Phone,
            DateOfBirth = customer.DateOfBirth,
            Gender = customer.Gender,
            AvatarUrl = customer.AvatarUrl,
            LoyaltyPoints = customer.LoyaltyPoints,
            MembershipLevel = customer.MembershipLevel,
            CreatedDate = customer.CreatedDate,
            TotalOrders = customer.TotalOrders,
            TotalSpent = customer.TotalSpent
        };
    }
    catch
    {
        return null;
    }
}

private string HashPassword(string password)
{
    using (var sha256 = SHA256.Create())
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

private bool VerifyPassword(string password, string hash)
{
    var inputHash = HashPassword(password);
    return inputHash == hash;
}

private string GetOrderStatusClass(string status)
{
    switch (status)
    {
        case "Pending": return "warning";
        case "Confirmed": return "info";
        case "Preparing": return "info";
        case "Ready": return "primary";
        case "Delivering": return "primary";
        case "Completed": return "success";
        case "Cancelled": return "danger";
        default: return "secondary";
    }
}

private string GetOrderStatusText(string status)
{
    switch (status)
    {
        case "Pending": return "Chờ xác nhận";
        case "Confirmed": return "Đã xác nhận";
        case "Preparing": return "Đang chuẩn bị";
        case "Ready": return "Sẵn sàng";
        case "Delivering": return "Đang giao";
        case "Completed": return "Hoàn thành";
        case "Cancelled": return "Đã hủy";
        default: return status;
    }
}

#endregion

#region Helper Classes

private class CustomerLoginInfo
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; }
}

private class CustomerProfileInfo
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
}

private class OrderSummaryInfo
{
    public int Id { get; set; }
    public string OrderCode { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string FirstItemImage { get; set; }
}

private class WishlistItemInfo
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; }
    public string Category { get; set; }
    public DateTime AddedDate { get; set; }
}

private class CustomerAddressInfo
{
    public int Id { get; set; }
    public string ReceiverName { get; set; }
    public string ReceiverPhone { get; set; }
    public string AddressLine { get; set; }
    public string Ward { get; set; }
    public string District { get; set; }
    public string City { get; set; }
    public string AddressType { get; set; }
    public bool IsDefault { get; set; }
}

#endregion

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _db.Dispose();
    }
    base.Dispose(disposing);
}
    }
}
