using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller cho ứng dụng Shipper
    /// </summary>
    public class ShipperAppController : Controller
    {
        private readonly MyDbContext _db = new MyDbContext();
        private readonly DeliveryService _deliveryService;

        public ShipperAppController()
        {
            _deliveryService = new DeliveryService(_db);
        }

        #region Authentication

        /// <summary>
        /// Trang giới thiệu Shipper App
        /// </summary>
        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("ShipperId") != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        /// <summary>
        /// Trang đăng nhập shipper
        /// </summary>
        public ActionResult Login()
        {
            if (HttpContext.Session.GetString("ShipperId") != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        /// <summary>
        /// Xử lý đăng nhập
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string phone, string password)
        {
            if (string.IsNullOrEmpty(phone))
            {
                ViewBag.Error = "Vui lòng nhập số điện thoại";
                return View();
            }

            var shipper = _db.Shippers.FirstOrDefault(s => s.Phone == phone && s.IsActive);
            
            if (shipper == null)
            {
                ViewBag.Error = "Số điện thoại không tồn tại hoặc tài khoản bị khóa";
                return View();
            }

            // Kiểm tra mật khẩu (nếu có) - đơn giản hóa: dùng 4 số cuối SĐT làm mật khẩu mặc định
            var defaultPassword = phone.Length >= 4 ? phone.Substring(phone.Length - 4) : phone;
            if (!string.IsNullOrEmpty(shipper.PasswordHash))
            {
                if (shipper.PasswordHash != password)
                {
                    ViewBag.Error = "Mật khẩu không đúng";
                    return View();
                }
            }
            else if (password != defaultPassword)
            {
                ViewBag.Error = "Mật khẩu không đúng (mặc định là 4 số cuối SĐT)";
                return View();
            }

            // Đăng nhập thành công
            HttpContext.Session.SetString("ShipperId", shipper.Id.ToString() ?? "");
            HttpContext.Session.SetString("ShipperName", shipper.FullName?.ToString() ?? "");
            HttpContext.Session.SetString("ShipperPhone", shipper.Phone?.ToString() ?? "");

            // Cập nhật trạng thái online
            shipper.Status = "Available";
            shipper.LastLocationUpdate = DateTime.Now;
            _db.SaveChanges();

            return RedirectToAction("Dashboard");
        }

        /// <summary>
        /// Đăng xuất
        /// </summary>
        public ActionResult Logout()
        {
            var shipperId = (int.TryParse(HttpContext.Session.GetString("ShipperId"), out int _pShipperId) ? (int?)_pShipperId : null);
            if (shipperId.HasValue)
            {
                var shipper = _db.Shippers.Find(shipperId.Value);
                if (shipper != null)
                {
                    shipper.Status = "Offline";
                    _db.SaveChanges();
                }
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        #endregion

        #region Dashboard & Orders

        /// <summary>
        /// Dashboard chính của shipper
        /// </summary>
        public ActionResult Dashboard()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var shipper = _db.Shippers.Find(shipperId);
            if (shipper == null) return RedirectToAction("Login");

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Lấy đơn hàng hiện tại (đang xử lý)
            var activeAssignment = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .Include(a => a.CustomerOrder.CustomerOrderDetails)
                .FirstOrDefault(a => a.ShipperId == shipperId &&
                    (a.Status == "Assigned" || a.Status == "Accepted" || a.Status == "PickedUp" || a.Status == "Delivering"));

            // Thống kê hôm nay
            var todayStats = _db.DeliveryAssignments
                .Where(a => a.ShipperId == shipperId &&
                           a.AssignedTime >= today && a.AssignedTime < tomorrow)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    TotalOrders = g.Count(),
                    Completed = g.Count(a => a.Status == "Delivered"),
                    Earnings = g.Where(a => a.Status == "Delivered").Sum(a => (decimal?)a.ShipperEarning) ?? 0
                })
                .FirstOrDefault();

            var viewModel = new ShipperDashboardViewModel
            {
                Shipper = shipper,
                ActiveOrder = activeAssignment != null ? new ShipperActiveOrderViewModel
                {
                    AssignmentId = activeAssignment.Id,
                    OrderId = activeAssignment.OrderId,
                    OrderCode = activeAssignment.CustomerOrder.OrderCode,
                    CustomerName = activeAssignment.CustomerOrder.CustomerName,
                    CustomerPhone = activeAssignment.CustomerOrder.CustomerPhone,
                    DeliveryAddress = activeAssignment.CustomerOrder.DeliveryAddress,
                    District = activeAssignment.CustomerOrder.District,
                    Ward = activeAssignment.CustomerOrder.Ward,
                    TotalAmount = activeAssignment.CustomerOrder.TotalAmount,
                    PaymentMethod = activeAssignment.CustomerOrder.PaymentMethod,
                    PaymentStatus = activeAssignment.CustomerOrder.PaymentStatus,
                    Note = activeAssignment.CustomerOrder.Note,
                    Status = activeAssignment.Status,
                    AssignedTime = activeAssignment.AssignedTime,
                    EstimatedArrival = activeAssignment.EstimatedArrival,
                    Items = activeAssignment.CustomerOrder.CustomerOrderDetails?.Select(d => new OrderItemSummary
                    {
                        ItemName = d.ItemName,
                        Quantity = d.Quantity,
                        Price = d.UnitPrice,
                        SpecialInstructions = d.SpecialInstructions
                    }).ToList() ?? new List<OrderItemSummary>()
                } : null,
                TodayOrderCount = todayStats?.TotalOrders ?? 0,
                TodayCompletedCount = todayStats?.Completed ?? 0,
                TodayEarnings = todayStats?.Earnings ?? 0
            };

            return View(viewModel);
        }

        /// <summary>
        /// Danh sách đơn hàng mới (chờ nhận)
        /// </summary>
        public ActionResult NewOrders()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var shipper = _db.Shippers.Find(shipperId);
            if (shipper == null || shipper.Status == "Busy")
            {
                // Nếu đang bận, không hiển thị đơn mới
                return View(new List<ShipperOrderViewModel>());
            }

            // Lấy đơn hàng chưa được gán hoặc đang chờ shipper chấp nhận
            var pendingAssignments = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .Where(a => a.ShipperId == shipperId && a.Status == "Assigned")
                .OrderByDescending(a => a.AssignedTime)
                .ToList()
                .Select(a => new ShipperOrderViewModel
                {
                    AssignmentId = a.Id,
                    OrderId = a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    CustomerPhone = a.CustomerOrder.CustomerPhone,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    District = a.CustomerOrder.District,
                    TotalAmount = a.CustomerOrder.TotalAmount,
                    DeliveryFee = a.DeliveryFee,
                    ShipperEarning = a.ShipperEarning,
                    PaymentMethod = a.CustomerOrder.PaymentMethod,
                    Status = a.Status,
                    AssignedTime = a.AssignedTime,
                    EstimatedArrival = a.EstimatedArrival
                })
                .ToList();

            return View(pendingAssignments);
        }

        /// <summary>
        /// Chi tiết đơn hàng
        /// </summary>
        public ActionResult OrderDetail(int id)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var assignment = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .Include(a => a.CustomerOrder.CustomerOrderDetails)
                .FirstOrDefault(a => a.Id == id && a.ShipperId == shipperId);

            if (assignment == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng";
                return RedirectToAction("Dashboard");
            }

            var viewModel = new ShipperOrderDetailViewModel
            {
                Assignment = assignment,
                Order = assignment.CustomerOrder,
                OrderItems = assignment.CustomerOrder.CustomerOrderDetails?.ToList() ?? new List<CustomerOrderDetail>()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Lịch sử đơn hàng
        /// </summary>
        public ActionResult History(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var from = fromDate ?? DateTime.Today.AddDays(-7);
            var to = (toDate ?? DateTime.Today).AddDays(1);

            var orders = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .Where(a => a.ShipperId == shipperId &&
                           a.AssignedTime >= from && a.AssignedTime < to)
                .OrderByDescending(a => a.AssignedTime)
                .ToList()
                .Select(a => new ShipperOrderViewModel
                {
                    AssignmentId = a.Id,
                    OrderId = a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    TotalAmount = a.CustomerOrder.TotalAmount,
                    ShipperEarning = a.ShipperEarning,
                    Status = a.Status,
                    AssignedTime = a.AssignedTime,
                    DeliveryTime = a.DeliveryTime,
                    CustomerRating = a.CustomerRating
                })
                .ToList();

            ViewBag.FromDate = from;
            ViewBag.ToDate = toDate ?? DateTime.Today;

            return View(orders);
        }

        /// <summary>
        /// Thống kê thu nhập
        /// </summary>
        public ActionResult Earnings(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var from = fromDate ?? DateTime.Today.AddDays(-30);
            var to = (toDate ?? DateTime.Today).AddDays(1);

            var assignments = _db.DeliveryAssignments
                .Where(a => a.ShipperId == shipperId &&
                           a.Status == "Delivered" &&
                           a.DeliveryTime >= from && a.DeliveryTime < to)
                .ToList();

            var viewModel = new ShipperEarningsViewModel
            {
                FromDate = from,
                ToDate = toDate ?? DateTime.Today,
                TotalOrders = assignments.Count,
                TotalEarnings = assignments.Sum(a => a.ShipperEarning),
                AverageEarningPerOrder = assignments.Count > 0 ? assignments.Average(a => a.ShipperEarning) : 0,
                DailyEarnings = assignments
                    .GroupBy(a => a.DeliveryTime.Value.Date)
                    .Select(g => new DailyEarning
                    {
                        Date = g.Key,
                        OrderCount = g.Count(),
                        Earnings = g.Sum(a => a.ShipperEarning)
                    })
                    .OrderByDescending(d => d.Date)
                    .ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Trang cá nhân
        /// </summary>
        public ActionResult Profile()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var shipper = _db.Shippers.Find(shipperId);
            if (shipper == null) return RedirectToAction("Login");

            return View(shipper);
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string fullName, string email, string vehicleType, string licensePlate)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            try
            {
                var shipper = _db.Shippers.Find(shipperId);
                if (shipper != null)
                {
                    shipper.FullName = fullName;
                    shipper.Email = email;
                    shipper.VehicleType = vehicleType;
                    shipper.LicensePlate = licensePlate;
                    _db.SaveChanges();

                    HttpContext.Session.SetString("ShipperName", fullName?.ToString() ?? "");
                    TempData["Success"] = "Đã cập nhật thông tin";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("Profile");
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost]
        public JsonResult ChangePassword(string currentPassword, string newPassword)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var shipper = _db.Shippers.Find(shipperId);
                if (shipper == null)
                    return Json(new { success = false, message = "Không tìm thấy tài khoản" });

                // Kiểm tra mật khẩu hiện tại
                var defaultPassword = shipper.Phone.Length >= 4 ? shipper.Phone.Substring(shipper.Phone.Length - 4) : shipper.Phone;
                var currentPwd = string.IsNullOrEmpty(shipper.PasswordHash) ? defaultPassword : shipper.PasswordHash;
                
                if (currentPassword != currentPwd)
                    return Json(new { success = false, message = "Mật khẩu hiện tại không đúng" });

                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 4)
                    return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 4 ký tự" });

                shipper.PasswordHash = newPassword;
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Order Actions (API)

        /// <summary>
        /// Chấp nhận đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult AcceptOrder(int assignmentId)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var assignment = _db.DeliveryAssignments
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.Id == assignmentId && a.ShipperId == shipperId);

                if (assignment == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                if (assignment.Status != "Assigned")
                    return Json(new { success = false, message = "Đơn hàng đã được xử lý" });

                assignment.Status = "Accepted";
                assignment.Shipper.Status = "Busy";
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã nhận đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Từ chối đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult RejectOrder(int assignmentId, string reason)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var assignment = _db.DeliveryAssignments
                    .Include(a => a.Shipper)
                    .Include(a => a.CustomerOrder)
                    .FirstOrDefault(a => a.Id == assignmentId && a.ShipperId == shipperId);

                if (assignment == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

                if (assignment.Status != "Assigned")
                    return Json(new { success = false, message = "Đơn hàng đã được xử lý" });

                assignment.Status = "Rejected";
                assignment.FailureReason = reason;
                assignment.Shipper.Status = "Available";
                
                // Đưa đơn về trạng thái Ready để gán shipper khác
                assignment.CustomerOrder.Status = "Ready";
                
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã từ chối đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Xác nhận đã lấy hàng
        /// </summary>
        [HttpPost]
        public JsonResult PickupOrder(int assignmentId)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var success = _deliveryService.UpdateDeliveryStatus(assignmentId, "PickedUp");
                if (success)
                    return Json(new { success = true, message = "Đã lấy hàng, bắt đầu giao" });
                
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành giao hàng
        /// </summary>
        [HttpPost]
        public JsonResult CompleteDelivery(int assignmentId, string proofImage = null, string notes = null)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var success = _deliveryService.UpdateDeliveryStatus(assignmentId, "Delivered", notes, proofImage);
                if (success)
                    return Json(new { success = true, message = "Giao hàng thành công!" });
                
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Báo cáo giao hàng thất bại
        /// </summary>
        [HttpPost]
        public JsonResult ReportFailure(int assignmentId, string reason)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var success = _deliveryService.UpdateDeliveryStatus(assignmentId, "Failed", null, null, reason);
                if (success)
                    return Json(new { success = true, message = "Đã báo cáo giao hàng thất bại" });
                
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật vị trí
        /// </summary>
        [HttpPost]
        public JsonResult UpdateLocation(decimal latitude, decimal longitude)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false });

            var success = _deliveryService.UpdateShipperLocation(shipperId, latitude, longitude);
            return Json(new { success = success });
        }

        /// <summary>
        /// Cập nhật trạng thái online/offline
        /// </summary>
        [HttpPost]
        public JsonResult ToggleStatus()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            try
            {
                var shipper = _db.Shippers.Find(shipperId);
                if (shipper == null)
                    return Json(new { success = false, message = "Không tìm thấy tài khoản" });

                // Không cho phép offline nếu đang có đơn
                if (shipper.Status == "Busy")
                    return Json(new { success = false, message = "Bạn đang có đơn hàng, không thể offline" });

                shipper.Status = shipper.Status == "Available" ? "Offline" : "Available";
                _db.SaveChanges();

                return Json(new { success = true, status = shipper.Status, message = shipper.Status == "Available" ? "Đã bật nhận đơn" : "Đã tắt nhận đơn" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gọi điện cho khách hàng
        /// </summary>
        public ActionResult CallCustomer(int assignmentId)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var assignment = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .FirstOrDefault(a => a.Id == assignmentId && a.ShipperId == shipperId);

            if (assignment != null)
            {
                return Redirect("tel:" + assignment.CustomerOrder.CustomerPhone);
            }

            return RedirectToAction("Dashboard");
        }

        /// <summary>
        /// Mở Google Maps chỉ đường
        /// </summary>
        public ActionResult Navigate(int assignmentId)
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0) return RedirectToAction("Login");

            var assignment = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .FirstOrDefault(a => a.Id == assignmentId && a.ShipperId == shipperId);

            if (assignment != null)
            {
                var address = Uri.EscapeDataString(assignment.CustomerOrder.DeliveryAddress);
                return Redirect($"https://www.google.com/maps/search/?api=1&query={address}");
            }

            return RedirectToAction("Dashboard");
        }

        #endregion

        #region API Endpoints (cho Mobile App)

        /// <summary>
        /// API đăng nhập
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult ApiLogin(string phone, string password)
        {
            var shipper = _db.Shippers.FirstOrDefault(s => s.Phone == phone && s.IsActive);
            
            if (shipper == null)
                return Json(new { success = false, message = "Số điện thoại không tồn tại" });

            var defaultPassword = phone.Length >= 4 ? phone.Substring(phone.Length - 4) : phone;
            var currentPwd = string.IsNullOrEmpty(shipper.PasswordHash) ? defaultPassword : shipper.PasswordHash;
            
            if (password != currentPwd)
                return Json(new { success = false, message = "Mật khẩu không đúng" });

            // Tạo token đơn giản (trong thực tế nên dùng JWT)
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            
            shipper.Status = "Available";
            shipper.LastLocationUpdate = DateTime.Now;
            _db.SaveChanges();

            return Json(new
            {
                success = true,
                token = token,
                shipper = new
                {
                    shipper.Id,
                    shipper.FullName,
                    shipper.Phone,
                    shipper.Email,
                    shipper.VehicleType,
                    shipper.Rating,
                    shipper.TotalDeliveries,
                    shipper.TotalEarnings,
                    shipper.Status
                }
            });
        }

        /// <summary>
        /// API lấy thông tin dashboard
        /// </summary>
        [HttpGet]
        public JsonResult ApiGetDashboard()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            var shipper = _db.Shippers.Find(shipperId);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var activeAssignment = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .FirstOrDefault(a => a.ShipperId == shipperId &&
                    (a.Status == "Assigned" || a.Status == "Accepted" || a.Status == "PickedUp"));

            var todayStats = _db.DeliveryAssignments
                .Where(a => a.ShipperId == shipperId && a.Status == "Delivered" &&
                           a.DeliveryTime >= today && a.DeliveryTime < tomorrow)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Earnings = g.Sum(a => a.ShipperEarning)
                })
                .FirstOrDefault();

            return Json(new
            {
                success = true,
                data = new
                {
                    shipper = new
                    {
                        shipper.Id,
                        shipper.FullName,
                        shipper.Status,
                        shipper.Rating,
                        shipper.TotalDeliveries,
                        shipper.TotalEarnings
                    },
                    activeOrder = activeAssignment != null ? new
                    {
                        activeAssignment.Id,
                        activeAssignment.OrderId,
                        OrderCode = activeAssignment.CustomerOrder.OrderCode,
                        CustomerName = activeAssignment.CustomerOrder.CustomerName,
                        CustomerPhone = activeAssignment.CustomerOrder.CustomerPhone,
                        DeliveryAddress = activeAssignment.CustomerOrder.DeliveryAddress,
                        TotalAmount = activeAssignment.CustomerOrder.TotalAmount,
                        PaymentMethod = activeAssignment.CustomerOrder.PaymentMethod,
                        activeAssignment.Status,
                        activeAssignment.ShipperEarning
                    } : null,
                    todayOrders = todayStats?.Count ?? 0,
                    todayEarnings = todayStats?.Earnings ?? 0
                }
            });
        }

        /// <summary>
        /// API lấy đơn hàng mới
        /// </summary>
        [HttpGet]
        public JsonResult ApiGetNewOrders()
        {
            var shipperId = GetCurrentShipperId();
            if (shipperId == 0)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            var orders = _db.DeliveryAssignments
                .Include(a => a.CustomerOrder)
                .Where(a => a.ShipperId == shipperId && a.Status == "Assigned")
                .Select(a => new
                {
                    a.Id,
                    a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    CustomerPhone = a.CustomerOrder.CustomerPhone,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    TotalAmount = a.CustomerOrder.TotalAmount,
                    PaymentMethod = a.CustomerOrder.PaymentMethod,
                    a.DeliveryFee,
                    a.ShipperEarning,
                    a.AssignedTime,
                    a.EstimatedArrival
                })
                .ToList();

            return Json(new { success = true, orders = orders });
        }

        #endregion

        #region Helper Methods

        private int GetCurrentShipperId()
        {
            var shipperIdStr = HttpContext.Session.GetString("ShipperId");
            return int.TryParse(shipperIdStr, out int shipperId) ? shipperId : 0;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
                _deliveryService.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
