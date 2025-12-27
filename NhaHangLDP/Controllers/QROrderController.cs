using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;
using NhaHangLDP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller xử lý QR Code ordering cho khách hàng
    /// </summary>
    public class QROrderController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        #region Public Pages - Khách hàng truy cập

        /// <summary>
        /// Scan QR code - Entry point
        /// </summary>
        public ActionResult Scan(int? tableId, string token = null)
        {
            if (!tableId.HasValue)
            {
                return View("Error", (object)"Mã QR không hợp lệ. Vui lòng quét lại.");
            }

            var table = db.RestaurantTable
                .Include(t => t.TableArea)
                .FirstOrDefault(t => t.Id == tableId.Value);

            if (table == null)
            {
                return View("Error", (object)"Bàn không tồn tại trong hệ thống.");
            }

            // Kiểm tra session hiện có
            var existingSession = db.TableSession
                .Where(s => s.TableId == tableId.Value && s.Status == "Active")
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefault();

            string sessionToken;
            if (existingSession != null && !string.IsNullOrEmpty(token) && existingSession.SessionToken == token)
            {
                // Sử dụng session cũ
                sessionToken = token;
            }
            else
            {
                // Tạo session mới
                sessionToken = GenerateSessionToken();
                var newSession = new TableSession
                {
                    TableId = tableId.Value,
                    SessionToken = sessionToken,
                    StartTime = DateTime.Now,
                    Status = "Active"
                };
                db.TableSession.Add(newSession);
                db.SaveChanges();
            }

            return RedirectToAction("Menu", new { tableId = tableId, token = sessionToken });
        }

        /// <summary>
        /// Trang menu cho khách
        /// </summary>
        public ActionResult Menu(int tableId, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Scan", new { tableId = tableId });
            }

            // Validate session
            var session = db.TableSession
                .FirstOrDefault(s => s.TableId == tableId && s.SessionToken == token && s.Status == "Active");

            if (session == null)
            {
                return RedirectToAction("Scan", new { tableId = tableId });
            }

            var table = db.RestaurantTable
                .Include(t => t.TableArea)
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return View("Error", (object)"Bàn không tồn tại.");
            }

            // Lấy menu items
            var menuItems = db.MenuItem
                .Where(m => m.IsAvailable)
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .ToList();

            // Group theo category
            var categories = menuItems
                .GroupBy(m => m.Category)
                .Select(g => new QRMenuCategoryViewModel
                {
                    CategoryName = g.Key,
                    CategoryIcon = GetCategoryIcon(g.Key),
                    Items = g.Select(m => new MenuItemViewModel
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Description = m.Description,
                        Price = m.Price,
                        OriginalPrice = m.OriginalPrice,
                        Category = m.Category,
                        ImageUrl = m.ImageUrl,
                        PreparationTime = m.PreparationTime,
                        IsAvailable = m.IsAvailable,
                        IsFeatured = m.IsFeatured,
                        IsNew = m.IsNew,
                        Rating = m.Rating,
                        ReviewCount = m.ReviewCount,
                        SoldCount = m.SoldCount
                    }).ToList()
                }).ToList();

            // Lấy order hiện tại nếu có
            var currentOrder = db.QROrder
                .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                .Where(o => o.SessionToken == token && o.TableId == tableId &&
                           (o.Status == "Draft" || o.Status == "Submitted"))
                .OrderByDescending(o => o.CreatedTime)
                .FirstOrDefault();

            QROrderViewModel orderViewModel = null;
            if (currentOrder != null)
            {
                orderViewModel = MapToOrderViewModel(currentOrder);
            }

            // Restaurant info
            var restaurantName = db.AppSetting.FirstOrDefault(s => s.SettingKey == "RestaurantName")?.SettingValue ?? "Nhà Hàng LDP";
            var restaurantPhone = db.AppSetting.FirstOrDefault(s => s.SettingKey == "PhoneNumber")?.SettingValue ?? "";

            var viewModel = new QRMenuViewModel
            {
                TableId = tableId,
                TableNumber = table.TableNumber,
                SessionToken = token,
                TableAreaName = table.TableArea?.Name ?? "",
                Categories = categories,
                FeaturedItems = menuItems.Where(m => m.IsFeatured).Take(6).Select(m => new MenuItemViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    OriginalPrice = m.OriginalPrice,
                    ImageUrl = m.ImageUrl,
                    IsFeatured = true,
                    Rating = m.Rating
                }).ToList(),
                NewItems = menuItems.Where(m => m.IsNew).Take(4).Select(m => new MenuItemViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Price = m.Price,
                    OriginalPrice = m.OriginalPrice,
                    ImageUrl = m.ImageUrl,
                    IsNew = true
                }).ToList(),
                CurrentOrder = orderViewModel,
                RestaurantName = restaurantName,
                RestaurantPhone = restaurantPhone
            };

            return View(viewModel);
        }

        /// <summary>
        /// Xem đơn hàng hiện tại
        /// </summary>
        public ActionResult Cart(int tableId, string token)
        {
            if (!ValidateSession(tableId, token))
            {
                return RedirectToAction("Scan", new { tableId = tableId });
            }

            var order = db.QROrder
                .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                .Include(o => o.RestaurantTable)
                .Where(o => o.SessionToken == token && o.TableId == tableId &&
                           (o.Status == "Draft" || o.Status == "Submitted"))
                .OrderByDescending(o => o.CreatedTime)
                .FirstOrDefault();

            if (order == null)
            {
                return RedirectToAction("Menu", new { tableId = tableId, token = token });
            }

            var viewModel = MapToOrderViewModel(order);
            ViewBag.TableId = tableId;
            ViewBag.Token = token;

            return View(viewModel);
        }

        /// <summary>
        /// Theo dõi trạng thái đơn hàng
        /// </summary>
        public ActionResult TrackOrder(int tableId, string token)
        {
            if (!ValidateSession(tableId, token))
            {
                return RedirectToAction("Scan", new { tableId = tableId });
            }

            var orders = db.QROrder
                .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                .Include(o => o.RestaurantTable)
                .Where(o => o.SessionToken == token && o.TableId == tableId)
                .OrderByDescending(o => o.CreatedTime)
                .ToList();

            ViewBag.TableId = tableId;
            ViewBag.Token = token;

            return View(orders.Select(o => MapToOrderViewModel(o)).ToList());
        }

        #endregion

        #region API Endpoints - AJAX calls

        /// <summary>
        /// Thêm món vào đơn
        /// </summary>
        [HttpPost]
        public JsonResult AddToOrder(AddToQROrderDto dto)
        {
            try
            {
                if (!ValidateSession(dto.TableId, dto.SessionToken))
                {
                    return Json(new { success = false, message = "Phiên làm việc không hợp lệ" });
                }

                var menuItem = db.MenuItem.Find(dto.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    return Json(new { success = false, message = "Món ăn không còn khả dụng" });
                }

                // Tìm hoặc tạo order draft
                var order = db.QROrder
                    .Include(o => o.QROrderDetail)
                    .FirstOrDefault(o => o.SessionToken == dto.SessionToken &&
                                        o.TableId == dto.TableId &&
                                        o.Status == "Draft");

                if (order == null)
                {
                    order = new QROrder
                    {
                        QROrderCode = "QR" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),
                        SessionToken = dto.SessionToken,
                        TableId = dto.TableId,
                        Status = "Draft",
                        CreatedTime = DateTime.Now
                    };
                    db.QROrder.Add(order);
                    db.SaveChanges();
                }

                // Kiểm tra món đã có trong order chưa
                var existingItem = order.QROrderDetail.FirstOrDefault(d => d.MenuItemId == dto.MenuItemId);
                if (existingItem != null)
                {
                    existingItem.Quantity += dto.Quantity;
                    if (!string.IsNullOrEmpty(dto.Notes))
                    {
                        existingItem.ItemNotes = dto.Notes;
                    }
                }
                else
                {
                    var detail = new QROrderDetail
                    {
                        QROrderId = order.Id,
                        MenuItemId = dto.MenuItemId,
                        Quantity = dto.Quantity,
                        UnitPrice = menuItem.Price,
                        ItemNotes = dto.Notes,
                        ItemStatus = "Draft",
                        AddedTime = DateTime.Now
                    };
                    db.QROrderDetail.Add(detail);
                }

                db.SaveChanges();

                // Tính tổng
                var total = db.QROrderDetail
                    .Where(d => d.QROrderId == order.Id)
                    .Sum(d => d.Quantity * d.UnitPrice);

                var itemCount = db.QROrderDetail
                    .Where(d => d.QROrderId == order.Id)
                    .Sum(d => d.Quantity);

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {menuItem.Name} vào đơn",
                    orderId = order.Id,
                    itemCount = itemCount,
                    total = total
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật số lượng món
        /// </summary>
        [HttpPost]
        public JsonResult UpdateItemQuantity(UpdateQROrderItemDto dto)
        {
            try
            {
                var detail = db.QROrderDetail
                    .Include(d => d.QROrder)
                    .FirstOrDefault(d => d.Id == dto.OrderDetailId);

                if (detail == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy món" });
                }

                if (detail.QROrder.SessionToken != dto.SessionToken)
                {
                    return Json(new { success = false, message = "Phiên không hợp lệ" });
                }

                if (detail.QROrder.Status != "Draft")
                {
                    return Json(new { success = false, message = "Không thể sửa đơn đã gửi" });
                }

                if (dto.Quantity <= 0)
                {
                    // Xóa item
                    db.QROrderDetail.Remove(detail);
                }
                else
                {
                    detail.Quantity = dto.Quantity;
                }

                db.SaveChanges();

                // Tính lại tổng
                var orderId = detail.QROrderId;
                var total = db.QROrderDetail
                    .Where(d => d.QROrderId == orderId)
                    .Sum(d => (decimal?)(d.Quantity * d.UnitPrice)) ?? 0;

                var itemCount = db.QROrderDetail
                    .Where(d => d.QROrderId == orderId)
                    .Sum(d => (int?)d.Quantity) ?? 0;

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật",
                    itemCount = itemCount,
                    total = total
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult SubmitOrder(SubmitQROrderDto dto)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.QROrder
                        .Include(o => o.QROrderDetail)
                        .FirstOrDefault(o => o.SessionToken == dto.SessionToken &&
                                            o.TableId == dto.TableId &&
                                            o.Status == "Draft");

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }

                    if (!order.QROrderDetail.Any())
                    {
                        return Json(new { success = false, message = "Đơn hàng chưa có món nào" });
                    }

                    // Validate promotion nếu có
                    decimal discount = 0;
                    if (!string.IsNullOrEmpty(dto.PromotionCode))
                    {
                        var promoResult = ValidatePromotionForOrder(dto.PromotionCode,
                            order.QROrderDetail.Sum(d => d.Quantity * d.UnitPrice),
                            dto.CustomerPhone);

                        if (promoResult.IsValid)
                        {
                            discount = promoResult.CalculatedDiscount;
                            order.AppliedPromotionCode = dto.PromotionCode;
                        }
                    }

                    // Cập nhật order
                    order.CustomerName = dto.CustomerName;
                    order.CustomerPhone = dto.CustomerPhone;
                    order.Notes = dto.Notes;
                    order.Status = "Submitted";
                    order.SubmittedTime = DateTime.Now;

                    // Cập nhật status các items
                    foreach (var item in order.QROrderDetail)
                    {
                        item.ItemStatus = "Pending";
                    }

                    // Cập nhật trạng thái bàn
                    var table = db.RestaurantTable.Find(dto.TableId);
                    if (table != null && table.Status == "Available")
                    {
                        table.Status = "Occupied";
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    // Gửi email xác nhận nếu có email
                    // TODO: Implement email sending

                    return Json(new
                    {
                        success = true,
                        message = "Đơn hàng đã được gửi! Nhân viên sẽ xác nhận trong giây lát.",
                        orderId = order.Id,
                        orderCode = order.QROrderCode
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        /// <summary>
        /// Lấy thông tin đơn hàng hiện tại
        /// </summary>
        [HttpGet]
        public JsonResult GetCurrentOrder(int tableId, string token)
        {
            try
            {
                var order = db.QROrder
                    .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                    .Where(o => o.SessionToken == token && o.TableId == tableId &&
                               (o.Status == "Draft" || o.Status == "Submitted"))
                    .OrderByDescending(o => o.CreatedTime)
                    .FirstOrDefault();

                if (order == null)
                {
                    return Json(new { success = true, hasOrder = false });
                }

                var viewModel = MapToOrderViewModel(order);
                return Json(new { success = true, hasOrder = true, order = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy trạng thái đơn hàng
        /// </summary>
        [HttpGet]
        public JsonResult GetOrderStatus(int orderId, string token)
        {
            try
            {
                var order = db.QROrder
                    .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                    .FirstOrDefault(o => o.Id == orderId && o.SessionToken == token);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                var viewModel = MapToOrderViewModel(order);
                return Json(new { success = true, order = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gọi nhân viên
        /// </summary>
        [HttpPost]
        public JsonResult CallStaff(int tableId, string token, string requestType)
        {
            try
            {
                if (!ValidateSession(tableId, token))
                {
                    return Json(new { success = false, message = "Phiên không hợp lệ" });
                }

                var table = db.RestaurantTable.Find(tableId);
                if (table == null)
                {
                    return Json(new { success = false, message = "Bàn không tồn tại" });
                }

                // Tạo notification
                var notification = new Notification
                {
                    Type = "CallStaff",
                    Title = $"Bàn {table.TableNumber} gọi nhân viên",
                    Message = $"Yêu cầu: {GetRequestTypeText(requestType)}",
                    Data = tableId.ToString(),
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    Level = requestType == "bill" ? "High" : "Normal"
                };
                db.Notification.Add(notification);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã gọi nhân viên. Vui lòng chờ trong giây lát!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Management - Quản lý đơn QR

        /// <summary>
        /// Danh sách đơn QR chờ xử lý (cho thu ngân/nhân viên)
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult PendingOrders()
        {
            var orders = db.QROrder
                .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                .Include(o => o.RestaurantTable)
                .Where(o => o.Status == "Submitted" || o.Status == "Confirmed")
                .OrderBy(o => o.SubmittedTime)
                .ToList();

            var viewModel = new QROrderListViewModel
            {
                Orders = orders.Select(o => MapToOrderViewModel(o)).ToList(),
                TotalOrders = orders.Count,
                PendingOrders = orders.Count(o => o.Status == "Submitted"),
                ConfirmedOrders = orders.Count(o => o.Status == "Confirmed")
            };

            return View(viewModel);
        }

        /// <summary>
        /// Xác nhận đơn QR
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult ConfirmOrder(int orderId)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var qrOrder = db.QROrder
                        .Include(o => o.QROrderDetail.Select(d => d.MenuItem))
                        .Include(o => o.RestaurantTable)
                        .FirstOrDefault(o => o.Id == orderId);

                    if (qrOrder == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }

                    if (qrOrder.Status != "Submitted")
                    {
                        return Json(new { success = false, message = "Đơn hàng không ở trạng thái chờ xác nhận" });
                    }

                    // Tạo Order chính thức
                    var activeShift = db.CashierShift.FirstOrDefault(s => s.Status == "Active");

                    var order = new Order
                    {
                        TableId = qrOrder.TableId,
                        WaiterId = GetCurrentEmployeeId() ?? 0,
                        ShiftId = activeShift?.Id ?? 0,
                        OrderTime = DateTime.Now,
                        Status = "Pending"
                    };
                    db.Order.Add(order);
                    db.SaveChanges();

                    // Tạo OrderDetails
                    foreach (var qrItem in qrOrder.QROrderDetail)
                    {
                        var detail = new OrderDetail
                        {
                            OrderId = order.Id,
                            MenuItemId = qrItem.MenuItemId,
                            Quantity = qrItem.Quantity,
                            PriceAtTime = qrItem.UnitPrice,
                            Notes = qrItem.ItemNotes
                        };
                        db.OrderDetail.Add(detail);

                        qrItem.ItemStatus = "Confirmed";
                    }

                    // Cập nhật QROrder
                    qrOrder.Status = "Confirmed";
                    qrOrder.ConfirmedTime = DateTime.Now;
                    qrOrder.LinkedOrderId = order.Id;
                    qrOrder.ConfirmedByEmployeeId = GetCurrentEmployeeId();

                    // Cập nhật bàn
                    var table = qrOrder.RestaurantTable;
                    if (table != null)
                    {
                        table.Status = "Occupied";
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = "Đã xác nhận đơn hàng thành công!",
                        orderId = order.Id
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        /// <summary>
        /// Từ chối đơn QR
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult RejectOrder(int orderId, string reason)
        {
            try
            {
                var order = db.QROrder.Find(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                order.Status = "Cancelled";
                order.Notes = (order.Notes ?? "") + "\n[Từ chối]: " + reason;

                // Cập nhật items
                foreach (var item in db.QROrderDetail.Where(d => d.QROrderId == orderId))
                {
                    item.ItemStatus = "Cancelled";
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Đã từ chối đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy QR Code image cho bàn
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult GenerateQR(int tableId)
        {
            var table = db.RestaurantTable.Include(t => t.TableArea).FirstOrDefault(t => t.Id == tableId);
            if (table == null)
            {
                return NotFound();
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrUrl = $"{baseUrl}{Url.Action("Scan", "QROrder", new { tableId = tableId })}";

            ViewBag.QRUrl = qrUrl;
            ViewBag.Table = table;

            return View();
        }

        #endregion

        #region Helpers

        private bool ValidateSession(int tableId, string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            return db.TableSession.Any(s => s.TableId == tableId &&
                                           s.SessionToken == token &&
                                           s.Status == "Active");
        }

        private string GenerateSessionToken()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
        }

        private string GetCategoryIcon(string category)
        {
            switch (category?.ToLower())
            {
                case "món khai vị": return "🥗";
                case "món chính": return "🍜";
                case "tráng miệng": return "🍰";
                case "đồ uống": return "🥤";
                case "combo": return "🎁";
                default: return "🍽️";
            }
        }

        private string GetRequestTypeText(string type)
        {
            switch (type?.ToLower())
            {
                case "bill": return "Thanh toán";
                case "water": return "Thêm nước";
                case "menu": return "Xem menu";
                case "other": return "Hỗ trợ khác";
                default: return type;
            }
        }

        private QROrderViewModel MapToOrderViewModel(QROrder order)
        {
            return new QROrderViewModel
            {
                Id = order.Id,
                QROrderCode = order.QROrderCode,
                SessionToken = order.SessionToken,
                TableId = order.TableId,
                TableNumber = order.RestaurantTable?.TableNumber ?? "",
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                Status = order.Status,
                Notes = order.Notes,
                CreatedTime = order.CreatedTime,
                SubmittedTime = order.SubmittedTime,
                ConfirmedTime = order.ConfirmedTime,
                CompletedTime = order.CompletedTime,
                LinkedOrderId = order.LinkedOrderId,
                AppliedPromotionCode = order.AppliedPromotionCode,
                Items = order.QROrderDetail?.Select(d => new QROrderItemViewModel
                {
                    Id = d.Id,
                    QROrderId = d.QROrderId,
                    MenuItemId = d.MenuItemId,
                    MenuItemName = d.MenuItem?.Name ?? "",
                    MenuItemImage = d.MenuItem?.ImageUrl ?? "",
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    ItemNotes = d.ItemNotes,
                    ItemStatus = d.ItemStatus,
                    AddedTime = d.AddedTime
                }).ToList() ?? new List<QROrderItemViewModel>()
            };
        }

        private PromotionValidationResult ValidatePromotionForOrder(string code, decimal orderTotal, string customerPhone)
        {
            // Sử dụng logic từ PromotionController
            var promotion = db.Promotion.FirstOrDefault(p => p.Code == code && p.IsActive);
            if (promotion == null)
            {
                return new PromotionValidationResult { IsValid = false, Message = "Mã không hợp lệ" };
            }

            var now = DateTime.Now;
            if (now < promotion.StartDate || now > promotion.EndDate)
            {
                return new PromotionValidationResult { IsValid = false, Message = "Mã đã hết hạn" };
            }

            if (orderTotal < promotion.MinOrderValue)
            {
                return new PromotionValidationResult { IsValid = false, Message = "Chưa đủ điều kiện" };
            }

            decimal discount = 0;
            if (promotion.DiscountType == "Percentage")
            {
                discount = orderTotal * promotion.DiscountValue / 100;
                if (promotion.MaxDiscountAmount.HasValue && discount > promotion.MaxDiscountAmount.Value)
                {
                    discount = promotion.MaxDiscountAmount.Value;
                }
            }
            else
            {
                discount = promotion.DiscountValue;
            }

            return new PromotionValidationResult
            {
                IsValid = true,
                PromotionId = promotion.Id,
                CalculatedDiscount = discount
            };
        }

        private int? GetCurrentEmployeeId()
        {
            return HttpContext.Session.GetInt32("EmployeeId");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
