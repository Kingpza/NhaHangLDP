using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;
using NhaHangLDP.Services;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý hệ thống giao hàng
    /// </summary>
    [CustomAuthorize("Admin", "Manager", "Delivery")]
    public class DeliveryController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();
        private readonly DeliveryService _deliveryService;

        public DeliveryController()
        {
            _deliveryService = new DeliveryService(_db);
        }


        #region Dashboard

        /// <summary>
        /// Dashboard quản lý giao hàng
        /// </summary>
        public ActionResult Index()
        {
            var viewModel = _deliveryService.GetDashboardStats();
            return View(viewModel);
        }

        /// <summary>
        /// API lấy dữ liệu dashboard (cho auto-refresh)
        /// </summary>
        [HttpGet]
        public JsonResult GetDashboardData()
        {
            try
            {
                var data = _deliveryService.GetDashboardStats();
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Trang quản lý giao hàng tổng hợp (Shop + Bên thứ 3)
        /// </summary>
        public ActionResult Management()
        {
            var viewModel = _deliveryService.GetDeliveryManagementData();
            ViewBag.ThirdPartyShippers = ThirdPartyShippers.GetAll();
            return View(viewModel);
        }

        /// <summary>
        /// API lấy dữ liệu quản lý giao hàng
        /// </summary>
        [HttpGet]
        public JsonResult GetManagementData()
        {
            try
            {
                var data = _deliveryService.GetDeliveryManagementData();
                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Orders Management

        /// <summary>
        /// Danh sách đơn hàng giao hàng
        /// </summary>
        public ActionResult Orders(string status = "all", int page = 1, int pageSize = 20)
        {
            var query = _db.CustomerOrder
                .Include(o => o.CustomerOrderDetail)
                .Where(o => o.OrderType == "Delivery");

            // Filter by status
            if (status != "all")
            {
                query = query.Where(o => o.Status == status);
            }

            var totalCount = query.Count();
            var orders = query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Map to ViewModel
            var orderViewModels = orders.Select(o =>
            {
                var assignment = _db.DeliveryAssignment
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.OrderId == o.Id && a.Status != "Cancelled");

                var vm = new DeliveryOrderItemViewModel
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    DeliveryAddress = o.DeliveryAddress,
                    District = o.District,
                    Ward = o.Ward,
                    SubTotal = o.SubTotal,
                    DeliveryFee = o.DeliveryFee,
                    TotalAmount = o.TotalAmount,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    Status = o.Status,
                    OrderDate = o.OrderDate,
                    EstimatedDeliveryTime = o.EstimatedDeliveryTime,
                    Note = o.Note,
                    ShipperId = assignment?.ShipperId,
                    DeliveryStatus = assignment?.Status,
                    Items = o.CustomerOrderDetail?.Select(d => new OrderItemSummary
                    {
                        ItemName = d.ItemName,
                        Quantity = d.Quantity,
                        Price = d.UnitPrice,
                        SpecialInstructions = d.SpecialInstructions
                    }).ToList() ?? new List<OrderItemSummary>()
                };

                // Phan biet shipper noi bo vs ben thu 3
                if (assignment != null && assignment.IsThirdPartyDelivery)
                {
                    vm.DeliveryType = DeliveryType.ThirdParty;
                    vm.ThirdPartyName = assignment.GetThirdPartyNameFromNotes();
                    vm.ThirdPartyOrderCode = assignment.GetThirdPartyOrderCodeFromNotes();
                    vm.ThirdPartyShipperName = assignment.GetThirdPartyShipperNameFromNotes();
                    vm.ThirdPartyShipperPhone = assignment.GetThirdPartyShipperPhoneFromNotes();
                    vm.ShipperName = vm.ThirdPartyName ?? "Ben thu 3";
                }
                else
                {
                    vm.DeliveryType = DeliveryType.ShopEmployee;
                    vm.ShipperName = assignment?.Shipper?.FullName;
                    vm.ShipperPhone = assignment?.Shipper?.Phone;
                }

                return vm;
            }).ToList();

            var viewModel = new DeliveryOrderListViewModel
            {
                Orders = orderViewModels,
                SelectedStatus = status,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            ViewBag.StatusCounts = new Dictionary<string, int>
            {
                { "all", _db.CustomerOrder.Count(o => o.OrderType == "Delivery") },
                { "Pending", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Pending") },
                { "Confirmed", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Confirmed") },
                { "Preparing", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Preparing") },
                { "Ready", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Ready") },
                { "Delivering", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Delivering") },
                { "Completed", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Completed") },
                { "Cancelled", _db.CustomerOrder.Count(o => o.OrderType == "Delivery" && o.Status == "Cancelled") }
            };

            return View(viewModel);
        }

        /// <summary>
        /// Chi tiết đơn hàng
        /// </summary>
        public ActionResult OrderDetail(int id)
        {
            var order = _db.CustomerOrder
                .Include(o => o.CustomerOrderDetail.Select(d => d.MenuItem))
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng";
                return RedirectToAction("Orders");
            }

            var assignment = _db.DeliveryAssignment
                .Include(a => a.Shipper)
                .FirstOrDefault(a => a.OrderId == id && a.Status != "Cancelled");

            var viewModel = new DeliveryOrderDetailViewModel
            {
                Order = order,
                Assignment = assignment,
                Shipper = assignment?.Shipper,
                OrderItems = order.CustomerOrderDetail?.ToList() ?? new List<CustomerOrderDetail>(),
                TrackingSteps = GetOrderTrackingSteps(order, assignment)
            };

            ViewBag.AvailableShippers = _db.Shipper
                .Where(s => s.Status == "Available" && s.IsActive)
                .OrderByDescending(s => s.Rating)
                .ToList();

            return View(viewModel);
        }

        /// <summary>
        /// Lấy các bước tracking đơn hàng
        /// </summary>
        private List<OrderTrackingStep> GetOrderTrackingSteps(CustomerOrder order, DeliveryAssignment assignment)
        {
            var steps = new List<OrderTrackingStep>
            {
                new OrderTrackingStep
                {
                    Status = "Pending",
                    Title = "Đơn hàng mới",
                    Description = "Đơn hàng đã được tạo",
                    Time = order.OrderDate,
                    IsCompleted = true,
                    Icon = "fas fa-shopping-cart"
                },
                new OrderTrackingStep
                {
                    Status = "Confirmed",
                    Title = "Đã xác nhận",
                    Description = "Nhà hàng đã xác nhận đơn hàng",
                    Time = order.ConfirmedDate,
                    IsCompleted = order.ConfirmedDate.HasValue,
                    Icon = "fas fa-check-circle"
                },
                new OrderTrackingStep
                {
                    Status = "Preparing",
                    Title = "Đang chuẩn bị",
                    Description = "Đang chuẩn bị món ăn",
                    Time = order.PreparingDate,
                    IsCompleted = order.PreparingDate.HasValue,
                    Icon = "fas fa-utensils"
                },
                new OrderTrackingStep
                {
                    Status = "Ready",
                    Title = "Sẵn sàng giao",
                    Description = "Món ăn đã sẵn sàng, đang chờ shipper",
                    Time = order.ReadyDate,
                    IsCompleted = order.ReadyDate.HasValue,
                    Icon = "fas fa-box"
                },
                new OrderTrackingStep
                {
                    Status = "Delivering",
                    Title = "Đang giao hàng",
                    Description = assignment != null ? $"Shipper {assignment.Shipper?.FullName} đang giao hàng" : "Đang giao hàng",
                    Time = order.DeliveringDate ?? assignment?.PickupTime,
                    IsCompleted = order.DeliveringDate.HasValue,
                    Icon = "fas fa-motorcycle"
                },
                new OrderTrackingStep
                {
                    Status = "Completed",
                    Title = "Hoàn thành",
                    Description = "Đã giao hàng thành công",
                    Time = order.CompletedDate ?? assignment?.DeliveryTime,
                    IsCompleted = order.Status == "Completed",
                    Icon = "fas fa-check-double"
                }
            };

            // Mark current step
            var currentIndex = steps.FindIndex(s => s.Status == order.Status);
            if (currentIndex >= 0)
            {
                steps[currentIndex].IsCurrent = true;
            }

            return steps;
        }

        #endregion

        #region Order Actions

        /// <summary>
        /// Xác nhận đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult ConfirmOrder(int orderId)
        {
            try
            {
                var success = _deliveryService.UpdateOrderStatus(orderId, "Confirmed");
                if (success)
                {
                    return Json(new { success = true, message = "Đã xác nhận đơn hàng" });
                }
                return Json(new { success = false, message = "Không thể xác nhận đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu đang chuẩn bị
        /// </summary>
        [HttpPost]
        public JsonResult StartPreparing(int orderId)
        {
            try
            {
                var success = _deliveryService.UpdateOrderStatus(orderId, "Preparing");
                if (success)
                {
                    return Json(new { success = true, message = "Đã bắt đầu chuẩn bị" });
                }
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu sẵn sàng giao
        /// </summary>
        [HttpPost]
        public JsonResult MarkReady(int orderId)
        {
            try
            {
                var success = _deliveryService.UpdateOrderStatus(orderId, "Ready");
                if (success)
                {
                    return Json(new { success = true, message = "Đơn hàng đã sẵn sàng giao" });
                }
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Hủy đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult CancelOrder(int orderId, string reason)
        {
            try
            {
                var order = _db.CustomerOrder.Find(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                order.Status = "Cancelled";
                order.CancelledDate = DateTime.Now;
                order.CancelReason = reason;

                // Hủy assignment nếu có
                var assignment = _db.DeliveryAssignment
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.OrderId == orderId && a.Status != "Cancelled" && a.Status != "Delivered");

                if (assignment != null)
                {
                    assignment.Status = "Cancelled";
                    assignment.Shipper.Status = "Available";
                }

                _db.SaveChanges();

                return Json(new { success = true, message = "Đã hủy đơn hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Shipper Assignment

        /// <summary>
        /// Gán shipper cho đơn hàng
        /// </summary>
        [HttpPost]
        public JsonResult AssignShipper(int orderId, int shipperId)
        {
            var result = _deliveryService.AssignShipperToOrder(orderId, shipperId);
            return Json(result);
        }

        /// <summary>
        /// Auto-assign shipper
        /// </summary>
        [HttpPost]
        public JsonResult AutoAssign(int orderId)
        {
            var result = _deliveryService.AutoAssignShipper(orderId);
            return Json(result);
        }

        /// <summary>
        /// Gán đơn hàng cho bên thứ 3
        /// </summary>
        [HttpPost]
        public JsonResult AssignToThirdParty(AssignThirdPartyDto dto)
        {
            try
            {
                var result = _deliveryService.AssignToThirdParty(dto);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật thông tin shipper bên thứ 3
        /// </summary>
        [HttpPost]
        public JsonResult UpdateThirdPartyInfo(int assignmentId, string shipperName, string shipperPhone, string orderCode)
        {
            try
            {
                var success = _deliveryService.UpdateThirdPartyInfo(assignmentId, shipperName, shipperPhone, orderCode);
                return Json(new { success = success, message = success ? "Đã cập nhật" : "Không thể cập nhật" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách shipper khả dụng
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableShippers()
        {
            var shippers = _db.Shipper
                .Where(s => s.Status == "Available" && s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.FullName,
                    s.Phone,
                    s.VehicleType,
                    s.Rating,
                    s.TotalDeliveries
                })
                .OrderByDescending(s => s.Rating)
                .ToList();

            return Json(new { success = true, shippers = shippers }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Shipper Management

        /// <summary>
        /// Danh sách shipper
        /// </summary>
        public ActionResult Shippers()
        {
            var shippers = _db.Shipper.OrderByDescending(s => s.IsActive).ThenBy(s => s.FullName).ToList();

            var viewModel = new ShipperManagementViewModel
            {
                Shippers = shippers,
                TotalActive = shippers.Count(s => s.IsActive),
                TotalAvailable = shippers.Count(s => s.Status == "Available" && s.IsActive),
                TotalBusy = shippers.Count(s => s.Status == "Busy" && s.IsActive),
                TotalOffline = shippers.Count(s => s.Status == "Offline" && s.IsActive)
            };

            return View(viewModel);
        }

        /// <summary>
        /// Form tạo shipper
        /// </summary>
        public ActionResult CreateShipper()
        {
            return View(new ShipperFormViewModel());
        }

        /// <summary>
        /// Tạo shipper mới
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateShipper(ShipperFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Kiểm tra số điện thoại đã tồn tại
                if (_db.Shipper.Any(s => s.Phone == model.Phone))
                {
                    ModelState.AddModelError("Phone", "Số điện thoại đã được sử dụng");
                    return View(model);
                }

                var shipper = new Shipper
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    VehicleType = model.VehicleType,
                    LicensePlate = model.LicensePlate,
                    Status = "Offline",
                    Rating = 5.0m,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.Now
                };

                _db.Shipper.Add(shipper);
                _db.SaveChanges();

                TempData["Success"] = "Đã thêm shipper thành công";
                return RedirectToAction("Shippers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                return View(model);
            }
        }

        /// <summary>
        /// Form sửa shipper
        /// </summary>
        public ActionResult EditShipper(int id)
        {
            var shipper = _db.Shipper.Find(id);
            if (shipper == null)
            {
                TempData["Error"] = "Không tìm thấy shipper";
                return RedirectToAction("Shippers");
            }

            var model = new ShipperFormViewModel
            {
                Id = shipper.Id,
                FullName = shipper.FullName,
                Phone = shipper.Phone,
                Email = shipper.Email,
                VehicleType = shipper.VehicleType,
                LicensePlate = shipper.LicensePlate,
                IsActive = shipper.IsActive
            };

            return View(model);
        }

        /// <summary>
        /// Cập nhật shipper
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditShipper(ShipperFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var shipper = _db.Shipper.Find(model.Id);
                if (shipper == null)
                {
                    TempData["Error"] = "Không tìm thấy shipper";
                    return RedirectToAction("Shippers");
                }

                // Kiểm tra số điện thoại trùng
                if (_db.Shipper.Any(s => s.Phone == model.Phone && s.Id != model.Id))
                {
                    ModelState.AddModelError("Phone", "Số điện thoại đã được sử dụng");
                    return View(model);
                }

                shipper.FullName = model.FullName;
                shipper.Phone = model.Phone;
                shipper.Email = model.Email;
                shipper.VehicleType = model.VehicleType;
                shipper.LicensePlate = model.LicensePlate;
                shipper.IsActive = model.IsActive;

                _db.SaveChanges();

                TempData["Success"] = "Đã cập nhật shipper thành công";
                return RedirectToAction("Shippers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                return View(model);
            }
        }

        /// <summary>
        /// Xóa/Vô hiệu hóa shipper
        /// </summary>
        [HttpPost]
        public JsonResult DeleteShipper(int id)
        {
            try
            {
                var shipper = _db.Shipper.Find(id);
                if (shipper == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy shipper" });
                }

                // Kiểm tra shipper có đang giao hàng không
                if (shipper.Status == "Busy")
                {
                    return Json(new { success = false, message = "Shipper đang giao hàng, không thể xóa" });
                }

                shipper.IsActive = false;
                shipper.Status = "Offline";
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã vô hiệu hóa shipper" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái shipper
        /// </summary>
        [HttpPost]
        public JsonResult UpdateShipperStatus(int id, string status)
        {
            try
            {
                var success = _deliveryService.UpdateShipperStatus(id, status);
                if (success)
                {
                    return Json(new { success = true, message = "Đã cập nhật trạng thái" });
                }
                return Json(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Real-time Tracking

        /// <summary>
        /// Trang theo dõi real-time
        /// </summary>
        public ActionResult Tracking()
        {
            var activeDeliveries = _db.DeliveryAssignment
                .Include(a => a.CustomerOrder)
                .Include(a => a.Shipper)
                .Where(a => a.Status == "Accepted" || a.Status == "PickedUp" || a.Status == "Delivering")
                .ToList()
                .Select(a => new ActiveDeliveryViewModel
                {
                    AssignmentId = a.Id,
                    OrderId = a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    CustomerPhone = a.CustomerOrder.CustomerPhone,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    ShipperId = a.ShipperId,
                    ShipperName = a.Shipper.FullName,
                    ShipperPhone = a.Shipper.Phone,
                    Status = a.Status,
                    EstimatedArrival = a.EstimatedArrival,
                    Latitude = a.CurrentLatitude ?? a.Shipper.CurrentLatitude,
                    Longitude = a.CurrentLongitude ?? a.Shipper.CurrentLongitude
                })
                .ToList();

            var viewModel = new DeliveryTrackingViewModel
            {
                ActiveDeliveries = activeDeliveries,
                ShipperLocations = _db.Shipper
                    .Where(s => s.IsActive && s.CurrentLatitude.HasValue)
                    .Select(s => new ShipperLocationViewModel
                    {
                        ShipperId = s.Id,
                        ShipperName = s.FullName,
                        Status = s.Status,
                        Latitude = s.CurrentLatitude,
                        Longitude = s.CurrentLongitude,
                        LastUpdate = s.LastLocationUpdate
                    })
                    .ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// API lấy vị trí shipper đang giao
        /// </summary>
        [HttpGet]
        public JsonResult GetActiveDeliveries()
        {
            try
            {
                var deliveries = _db.DeliveryAssignment
                    .Include(a => a.CustomerOrder)
                    .Include(a => a.Shipper)
                    .Where(a => a.Status == "Accepted" || a.Status == "PickedUp" || a.Status == "Delivering")
                    .Select(a => new
                    {
                        a.Id,
                        a.OrderId,
                        OrderCode = a.CustomerOrder.OrderCode,
                        CustomerName = a.CustomerOrder.CustomerName,
                        DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                        ShipperName = a.Shipper.FullName,
                        ShipperPhone = a.Shipper.Phone,
                        a.Status,
                        a.EstimatedArrival,
                        Latitude = a.CurrentLatitude ?? a.Shipper.CurrentLatitude,
                        Longitude = a.CurrentLongitude ?? a.Shipper.CurrentLongitude
                    })
                    .ToList();

                return Json(new { success = true, deliveries = deliveries }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Delivery Zones

        /// <summary>
        /// Quản lý khu vực giao hàng
        /// </summary>
        public ActionResult Zones()
        {
            var zones = _db.DeliveryZone.OrderBy(z => z.DisplayOrder).ThenBy(z => z.District).ToList();
            return View(zones);
        }

        /// <summary>
        /// Tạo khu vực mới
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CreateZone(DeliveryZone zone)
        {
            try
            {
                if (string.IsNullOrEmpty(zone.ZoneName) || string.IsNullOrEmpty(zone.District))
                {
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin" });
                }

                _db.DeliveryZone.Add(zone);
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã thêm khu vực", zone = zone });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật khu vực
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateZone(DeliveryZone zone)
        {
            try
            {
                var existing = _db.DeliveryZone.Find(zone.Id);
                if (existing == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khu vực" });
                }

                existing.ZoneName = zone.ZoneName;
                existing.District = zone.District;
                existing.Ward = zone.Ward;
                existing.DeliveryFee = zone.DeliveryFee;
                existing.MinOrderForFreeDelivery = zone.MinOrderForFreeDelivery;
                existing.EstimatedTime = zone.EstimatedTime;
                existing.MaxDistance = zone.MaxDistance;
                existing.IsActive = zone.IsActive;
                existing.DisplayOrder = zone.DisplayOrder;

                _db.SaveChanges();

                return Json(new { success = true, message = "Đã cập nhật khu vực" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa khu vực
        /// </summary>
        [HttpPost]
        public JsonResult DeleteZone(int id)
        {
            try
            {
                var zone = _db.DeliveryZone.Find(id);
                if (zone == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khu vực" });
                }

                _db.DeliveryZone.Remove(zone);
                _db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa khu vực" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Distance-based Fee Settings

        /// <summary>
        /// Trang cài đặt phí giao hàng theo khoảng cách
        /// </summary>
        public ActionResult FeeSettings()
        {
            var viewModel = new DistanceFeeSettingsViewModel
            {
                FeeUnder10Km = GetSettingValue("FeeUnder10Km", 15000),
                Fee10To20Km = GetSettingValue("Fee10To20Km", 25000),
                FeeOver20Km = GetSettingValue("FeeOver20Km", 40000),
                FreeDeliveryMinOrder = GetSettingValue("FreeDeliveryMinOrder", 0),
                EnableDistanceBasedFee = GetSettingBool("EnableDistanceBasedFee", true),
                RestaurantLat = GetSettingDouble("RestaurantLat", 10.77690),
                RestaurantLng = GetSettingDouble("RestaurantLng", 106.70090)
            };
            return View(viewModel);
        }

        /// <summary>
        /// Cập nhật cài đặt phí giao hàng (Form POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateFeeSettings(DistanceFeeSettingsViewModel model)
        {
            try
            {
                var success = _deliveryService.UpdateDistanceFeeSettings(model);
                if (success)
                {
                    TempData["Success"] = "Đã cập nhật cài đặt phí giao hàng";
                }
                else
                {
                    TempData["Error"] = "Không thể cập nhật cài đặt";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("FeeSettings");
        }

        /// <summary>
        /// Lưu cài đặt phí giao hàng (AJAX JSON)
        /// </summary>
        [HttpPost]
        public JsonResult SaveFeeSettings()
        {
            try
            {
                Request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
                var bodyText = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
                var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<DistanceFeeSettingsViewModel>(bodyText);

                if (settings == null)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

                var success = _deliveryService.UpdateDistanceFeeSettings(settings);
                return Json(new
                {
                    success = success,
                    message = success ? "Đã lưu cài đặt phí giao hàng!" : "Không thể lưu cài đặt"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// API tính phí giao hàng theo khoảng cách
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult CalculateFeeByDistance(CalculateFeeByDistanceDto dto)
        {
            try
            {
                var result = _deliveryService.CalculateDeliveryFeeByDistance(dto.DistanceKm, dto.OrderAmount);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API lấy cấu hình phí theo khoảng cách
        /// </summary>
        [HttpGet]
        public JsonResult GetDistanceFeeConfigs()
        {
            try
            {
                var configs = _deliveryService.GetDistanceFeeConfigs();
                return Json(new { success = true, data = configs }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private decimal GetSettingValue(string key, decimal defaultValue)
        {
            var setting = _db.DeliverySettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null && decimal.TryParse(setting.SettingValue, out decimal value))
            {
                return value;
            }
            return defaultValue;
        }

        private bool GetSettingBool(string key, bool defaultValue)
        {
            var setting = _db.DeliverySettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null)
            {
                return setting.SettingValue?.ToLower() == "true";
            }
            return defaultValue;
        }

        private double GetSettingDouble(string key, double defaultValue)
        {
            var setting = _db.DeliverySettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null && double.TryParse(setting.SettingValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double value))
                return value;
            return defaultValue;
        }

        #endregion

        #region Shop Employee Delivery

        /// <summary>
        /// Trang đơn hàng đang giao cho nhân viên shop
        /// </summary>
        public ActionResult ShopEmployeeOrders(int? shipperId = null)
        {
            // Nếu không có shipperId, lấy từ session hoặc employee hiện tại
            int? currentShipperId = shipperId;
            
            if (!currentShipperId.HasValue && Session["ShipperId"] != null)
            {
                currentShipperId = (int)Session["ShipperId"];
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var viewModel = new ShopEmployeeDeliveryDashboardViewModel
            {
                ActiveOrders = _deliveryService.GetShopEmployeeActiveDeliveries(currentShipperId),
                CompletedTodayOrders = _db.DeliveryAssignment
                    .Include(a => a.CustomerOrder)
                    .Where(a => a.Status == "Delivered" &&
                               a.DeliveryTime >= today && a.DeliveryTime < tomorrow)
                    .Where(a => !currentShipperId.HasValue || a.ShipperId == currentShipperId.Value)
                    .OrderByDescending(a => a.DeliveryTime)
                    .ToList()
                    .Select(a => new ShopEmployeeDeliveryViewModel
                    {
                        AssignmentId = a.Id,
                        OrderId = a.OrderId,
                        OrderCode = a.CustomerOrder.OrderCode,
                        CustomerName = a.CustomerOrder.CustomerName,
                        TotalAmount = a.CustomerOrder.TotalAmount,
                        DeliveryFee = a.DeliveryFee,
                        Status = a.Status
                    })
                    .ToList()
            };

            // Get shipper/employee info
            if (currentShipperId.HasValue)
            {
                var shipper = _db.Shipper.Find(currentShipperId.Value);
                if (shipper != null)
                {
                    viewModel.Employee = new Employee { FullName = shipper.FullName };
                }
            }

            viewModel.TodayOrderCount = viewModel.ActiveOrders.Count + viewModel.CompletedTodayOrders.Count;
            viewModel.TodayCompletedCount = viewModel.CompletedTodayOrders.Count;
            viewModel.TodayTotalDeliveryFees = viewModel.CompletedTodayOrders.Sum(o => o.DeliveryFee);

            return View(viewModel);
        }

        /// <summary>
        /// API lấy đơn hàng đang giao của nhân viên shop
        /// </summary>
        [HttpGet]
        public JsonResult GetShopEmployeeActiveOrders(int? shipperId = null)
        {
            try
            {
                var orders = _deliveryService.GetShopEmployeeActiveDeliveries(shipperId);
                return Json(new { success = true, data = orders }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Thống kê giao hàng
        /// </summary>
        public ActionResult Statistics(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var from = fromDate ?? DateTime.Today.AddDays(-30);
            var to = toDate ?? DateTime.Today;


            var viewModel = _deliveryService.GetStatistics(from, to);
            return View(viewModel);
        }

        #endregion

        #region API for Shipper App

        /// <summary>
        /// API tính phí giao hàng
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult CalculateDeliveryFee(CalculateDeliveryFeeDto dto)
        {
            try
            {
                var result = _deliveryService.CalculateDeliveryFee(dto.District, dto.Ward, dto.OrderAmount);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API cập nhật vị trí shipper
        /// </summary>
        [HttpPost]
        public JsonResult UpdateShipperLocation(UpdateLocationDto dto)
        {
            try
            {
                var success = _deliveryService.UpdateShipperLocation(dto.ShipperId, dto.Latitude, dto.Longitude);
                return Json(new { success = success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API cập nhật trạng thái giao hàng
        /// </summary>
        [HttpPost]
        public JsonResult UpdateDeliveryStatus(UpdateDeliveryStatusDto dto)
        {
            try
            {
                var success = _deliveryService.UpdateDeliveryStatus(
                    dto.AssignmentId, 
                    dto.Status, 
                    dto.Notes, 
                    dto.ProofImageUrl, 
                    dto.FailureReason);

                return Json(new { success = success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API đánh giá shipper
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult RateDelivery(RateDeliveryDto dto)
        {
            try
            {
                var success = _deliveryService.RateDelivery(dto.AssignmentId, dto.Rating, dto.Feedback);
                return Json(new { success = success, message = success ? "Cảm ơn bạn đã đánh giá" : "Không thể đánh giá" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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
