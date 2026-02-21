using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý logic nghiệp vụ giao hàng
    /// </summary>
    public class DeliveryService
    {
        private readonly NhaHangLDPEntities _db;
        private readonly RealTimeNotificationService _notificationService;

        public DeliveryService()
        {
            _db = new NhaHangLDPEntities();
            _notificationService = new RealTimeNotificationService();
        }

        public DeliveryService(NhaHangLDPEntities db)
        {
            _db = db;
            _notificationService = new RealTimeNotificationService();
        }

        #region Delivery Fee Calculation

        /// <summary>
        /// Tính phí giao hàng dựa trên khu vực
        /// </summary>
        public DeliveryFeeResponse CalculateDeliveryFee(string district, string ward, decimal orderAmount)
        {
            var response = new DeliveryFeeResponse();

            try
            {
                // Tìm zone phù hợp nhất (ưu tiên ward cụ thể)
                var zone = _db.DeliveryZone
                    .Where(z => z.IsActive && z.District == district)
                    .OrderByDescending(z => z.Ward == ward ? 1 : 0)
                    .FirstOrDefault();

                if (zone != null)
                {
                    response.EstimatedTime = zone.EstimatedTime;
                    response.MinOrderForFree = zone.MinOrderForFreeDelivery;

                    // Miễn phí nếu đơn hàng >= MinOrderForFreeDelivery
                    if (zone.MinOrderForFreeDelivery > 0 && orderAmount >= zone.MinOrderForFreeDelivery)
                    {
                        response.DeliveryFee = 0;
                        response.IsFreeDelivery = true;
                        response.Message = "Miễn phí giao hàng cho đơn từ " + zone.MinOrderForFreeDelivery.ToString("N0") + "đ";
                    }
                    else
                    {
                        response.DeliveryFee = zone.DeliveryFee;
                        response.IsFreeDelivery = false;

                        if (zone.MinOrderForFreeDelivery > 0)
                        {
                            var remaining = zone.MinOrderForFreeDelivery - orderAmount;
                            response.Message = $"Thêm {remaining:N0}đ để được miễn phí giao hàng";
                        }
                    }
                }
                else
                {
                    // Phí mặc định nếu không tìm thấy zone
                    response.DeliveryFee = GetDefaultDeliveryFee();
                    response.EstimatedTime = 45;
                    response.IsFreeDelivery = false;
                    response.Message = "Khu vực của bạn áp dụng phí giao hàng tiêu chuẩn";
                }
            }
            catch (Exception)
            {
                response.DeliveryFee = GetDefaultDeliveryFee();
                response.EstimatedTime = 45;
            }

            return response;
        }

        /// <summary>
        /// Tính phí giao hàng dựa trên khoảng cách (KM)
        /// </summary>
        public DeliveryFeeResponse CalculateDeliveryFeeByDistance(decimal distanceKm, decimal orderAmount)
        {
            var response = new DeliveryFeeResponse
            {
                DistanceKm = distanceKm
            };

            try
            {
                // Kiểm tra có bật tính phí theo khoảng cách không
                var enableSetting = _db.DeliverySettings
                    .FirstOrDefault(s => s.SettingKey == "EnableDistanceBasedFee");
                
                bool enableDistanceFee = enableSetting != null && 
                    enableSetting.SettingValue?.ToLower() == "true";

                if (!enableDistanceFee)
                {
                    // Fallback về phí mặc định
                    response.DeliveryFee = GetDefaultDeliveryFee();
                    response.EstimatedTime = 45;
                    response.Message = "Phí giao hàng tiêu chuẩn";
                    return response;
                }

                // Tính phí theo khoảng cách
                decimal fee;
                string distanceRange;

                if (distanceKm < 10)
                {
                    fee = GetFeeFromSettings("FeeUnder10Km", 15000);
                    distanceRange = "Dưới 10km";
                    response.EstimatedTime = 30;
                }
                else if (distanceKm <= 20)
                {
                    fee = GetFeeFromSettings("Fee10To20Km", 25000);
                    distanceRange = "10km - 20km";
                    response.EstimatedTime = 45;
                }
                else
                {
                    fee = GetFeeFromSettings("FeeOver20Km", 40000);
                    distanceRange = "Trên 20km";
                    response.EstimatedTime = 60;
                }

                // Kiểm tra miễn phí giao hàng
                var freeDeliveryMin = GetFeeFromSettings("FreeDeliveryMinOrder", 0);
                if (freeDeliveryMin > 0 && orderAmount >= freeDeliveryMin)
                {
                    response.DeliveryFee = 0;
                    response.IsFreeDelivery = true;
                    response.MinOrderForFree = freeDeliveryMin;
                    response.Message = $"Miễn phí giao hàng cho đơn từ {freeDeliveryMin:N0}đ";
                }
                else
                {
                    response.DeliveryFee = fee;
                    response.IsFreeDelivery = false;
                    response.MinOrderForFree = freeDeliveryMin;
                    
                    if (freeDeliveryMin > 0)
                    {
                        var remaining = freeDeliveryMin - orderAmount;
                        response.Message = $"Thêm {remaining:N0}đ để được miễn phí giao hàng";
                    }
                }

                response.DistanceRange = distanceRange;
            }
            catch (Exception)
            {
                response.DeliveryFee = GetDefaultDeliveryFee();
                response.EstimatedTime = 45;
            }

            return response;
        }

        /// <summary>
        /// Lấy giá trị phí từ settings
        /// </summary>
        private decimal GetFeeFromSettings(string key, decimal defaultValue)
        {
            var setting = _db.DeliverySettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null && decimal.TryParse(setting.SettingValue, out decimal value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Lấy cấu hình phí giao hàng theo khoảng cách
        /// </summary>
        public List<DistanceBasedFeeConfig> GetDistanceFeeConfigs()
        {
            return new List<DistanceBasedFeeConfig>
            {
                new DistanceBasedFeeConfig 
                { 
                    MaxDistanceKm = 10, 
                    Fee = GetFeeFromSettings("FeeUnder10Km", 15000),
                    RangeName = "Dưới 10km"
                },
                new DistanceBasedFeeConfig 
                { 
                    MaxDistanceKm = 20, 
                    Fee = GetFeeFromSettings("Fee10To20Km", 25000),
                    RangeName = "10km - 20km"
                },
                new DistanceBasedFeeConfig 
                { 
                    MaxDistanceKm = 9999, 
                    Fee = GetFeeFromSettings("FeeOver20Km", 40000),
                    RangeName = "Trên 20km"
                }
            };
        }

        /// <summary>
        /// Cập nhật cấu hình phí giao hàng theo khoảng cách
        /// </summary>
        public bool UpdateDistanceFeeSettings(DistanceFeeSettingsViewModel settings)
        {
            try
            {
                UpdateOrCreateSetting("EnableDistanceBasedFee", settings.EnableDistanceBasedFee.ToString().ToLower());
                UpdateOrCreateSetting("FeeUnder10Km", settings.FeeUnder10Km.ToString());
                UpdateOrCreateSetting("Fee10To20Km", settings.Fee10To20Km.ToString());
                UpdateOrCreateSetting("FeeOver20Km", settings.FeeOver20Km.ToString());
                UpdateOrCreateSetting("FreeDeliveryMinOrder", settings.FreeDeliveryMinOrder.ToString());
                UpdateOrCreateSetting("RestaurantLat", settings.RestaurantLat.ToString(System.Globalization.CultureInfo.InvariantCulture));
                UpdateOrCreateSetting("RestaurantLng", settings.RestaurantLng.ToString(System.Globalization.CultureInfo.InvariantCulture));

                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateOrCreateSetting(string key, string value)
        {
            var setting = _db.DeliverySettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null)
            {
                setting.SettingValue = value;
                setting.UpdatedDate = DateTime.Now;
            }
            else
            {
                _db.DeliverySettings.Add(new DeliverySettings
                {
                    SettingKey = key,
                    SettingValue = value,
                    UpdatedDate = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Lấy phí giao hàng mặc định
        /// </summary>
        private decimal GetDefaultDeliveryFee()
        {
            var setting = _db.DeliverySettings
                .FirstOrDefault(s => s.SettingKey == "DefaultDeliveryFee");

            if (setting != null && decimal.TryParse(setting.SettingValue, out decimal fee))
            {
                return fee;
            }

            return 25000; // 25k VNĐ mặc định
        }

        #endregion

        #region Delivery Time Estimation

        /// <summary>
        /// Ước tính thời gian giao hàng
        /// </summary>
        public DateTime EstimateDeliveryTime(string district, DateTime orderTime)
        {
            var zone = _db.DeliveryZone
                .FirstOrDefault(z => z.District == district && z.IsActive);

            int estimatedMinutes = zone?.EstimatedTime ?? 45;

            // Thêm thời gian chuẩn bị món (lấy từ settings hoặc mặc định 20 phút)
            int prepTime = GetPreparationTime();

            return orderTime.AddMinutes(prepTime + estimatedMinutes);
        }

        private int GetPreparationTime()
        {
            var setting = _db.DeliverySettings
                .FirstOrDefault(s => s.SettingKey == "DefaultPreparationTime");

            if (setting != null && int.TryParse(setting.SettingValue, out int time))
            {
                return time;
            }

            return 20; // 20 phút mặc định
        }

        #endregion

        #region Shipper Management

        /// <summary>
        /// Tìm shipper tốt nhất cho đơn hàng
        /// </summary>
        public Shipper FindBestShipper(decimal? destLatitude = null, decimal? destLongitude = null)
        {
            var query = _db.Shipper
                .Where(s => s.Status == "Available" && s.IsActive);

            if (destLatitude.HasValue && destLongitude.HasValue)
            {
                // Ưu tiên shipper gần nhất nếu có vị trí
                var shippersWithLocation = query
                    .Where(s => s.CurrentLatitude.HasValue && s.CurrentLongitude.HasValue)
                    .ToList()
                    .Select(s => new
                    {
                        Shipper = s,
                        Distance = CalculateDistance(
                            s.CurrentLatitude.Value, s.CurrentLongitude.Value,
                            destLatitude.Value, destLongitude.Value)
                    })
                    .OrderBy(x => x.Distance)
                    .ThenByDescending(x => x.Shipper.Rating)
                    .Select(x => x.Shipper)
                    .FirstOrDefault();

                if (shippersWithLocation != null)
                    return shippersWithLocation;
            }

            // Fallback: ưu tiên rating cao, ít đơn hơn
            return query
                .OrderByDescending(s => s.Rating)
                .ThenBy(s => s.TotalDeliveries)
                .FirstOrDefault();
        }

        /// <summary>
        /// Lấy danh sách shipper khả dụng
        /// </summary>
        public List<Shipper> GetAvailableShippers()
        {
            return _db.Shipper
                .Where(s => s.Status == "Available" && s.IsActive)
                .OrderByDescending(s => s.Rating)
                .ToList();
        }

        /// <summary>
        /// Cập nhật vị trí shipper
        /// </summary>
        public bool UpdateShipperLocation(int shipperId, decimal latitude, decimal longitude)
        {
            try
            {
                var shipper = _db.Shipper.Find(shipperId);
                if (shipper == null) return false;

                shipper.CurrentLatitude = latitude;
                shipper.CurrentLongitude = longitude;
                shipper.LastLocationUpdate = DateTime.Now;

                // Cập nhật vị trí trong assignment đang active
                var activeAssignment = _db.DeliveryAssignment
                    .FirstOrDefault(a => a.ShipperId == shipperId &&
                        (a.Status == "Accepted" || a.Status == "PickedUp" || a.Status == "Delivering"));

                if (activeAssignment != null)
                {
                    activeAssignment.CurrentLatitude = latitude;
                    activeAssignment.CurrentLongitude = longitude;
                }

                _db.SaveChanges();

                // Gửi thông báo real-time về vị trí shipper
                _notificationService.UpdateShipperLocation(
                    shipperId,
                    shipper.FullName,
                    latitude,
                    longitude,
                    activeAssignment?.OrderId
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cập nhật trạng thái shipper
        /// </summary>
        public bool UpdateShipperStatus(int shipperId, string status)
        {
            try
            {
                var shipper = _db.Shipper.Find(shipperId);
                if (shipper == null) return false;

                shipper.Status = status;
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Order Assignment

        /// <summary>
        /// Gán shipper của shop cho đơn hàng
        /// </summary>
        public AssignmentResult AssignShipperToOrder(int orderId, int shipperId)
        {
            var result = new AssignmentResult();

            try
            {
                var order = _db.CustomerOrder.Find(orderId);
                if (order == null)
                {
                    result.Success = false;
                    result.Message = "Không tìm thấy đơn hàng";
                    return result;
                }

                var shipper = _db.Shipper.Find(shipperId);
                if (shipper == null)
                {
                    result.Success = false;
                    result.Message = "Không tìm thấy shipper";
                    return result;
                }

                if (shipper.Status != "Available")
                {
                    result.Success = false;
                    result.Message = "Shipper đang bận";
                    return result;
                }

                // Kiểm tra đã có assignment chưa
                var existingAssignment = _db.DeliveryAssignment
                    .FirstOrDefault(a => a.OrderId == orderId &&
                        a.Status != "Cancelled" && a.Status != "Failed");

                if (existingAssignment != null)
                {
                    result.Success = false;
                    result.Message = "Đơn hàng đã được gán cho shipper khác";
                    return result;
                }

                // Tính phí shipper nhận được (mặc định 80% phí giao hàng)
                decimal shipperEarningRate = 0.8m;
                var earningSetting = _db.DeliverySettings
                    .FirstOrDefault(s => s.SettingKey == "ShipperEarningRate");
                if (earningSetting != null && decimal.TryParse(earningSetting.SettingValue, out decimal rate))
                {
                    shipperEarningRate = rate;
                }

                // Tạo assignment mới
                var assignment = new DeliveryAssignment
                {
                    OrderId = orderId,
                    ShipperId = shipperId,
                    AssignedTime = DateTime.Now,
                    Status = "Assigned",
                    DeliveryFee = order.DeliveryFee,
                    ShipperEarning = order.DeliveryFee * shipperEarningRate,
                    EstimatedArrival = EstimateDeliveryTime(order.District, DateTime.Now)
                };

                _db.DeliveryAssignment.Add(assignment);

                // Cập nhật trạng thái
                shipper.Status = "Busy";
                
                if (order.Status == "Confirmed" || order.Status == "Ready")
                {
                    order.Status = "Delivering";
                    order.DeliveringDate = DateTime.Now;
                    order.EstimatedDeliveryTime = assignment.EstimatedArrival;
                }

                _db.SaveChanges();

                // Gửi thông báo real-time cho shipper về đơn hàng mới
                _notificationService.NotifyNewDeliveryToShipper(shipperId, new DeliveryOrderNotification
                {
                    OrderId = orderId,
                    OrderCode = order.OrderCode,
                    CustomerName = order.CustomerName,
                    CustomerPhone = order.CustomerPhone,
                    DeliveryAddress = order.DeliveryAddress,
                    District = order.District,
                    TotalAmount = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod,
                    DeliveryFee = order.DeliveryFee,
                    ShipperEarning = assignment.ShipperEarning,
                    EstimatedTime = assignment.EstimatedArrival?.ToString("HH:mm")
                });

                result.Success = true;
                result.Message = $"Đã gán shipper {shipper.FullName} cho đơn hàng";
                result.AssignmentId = assignment.Id;
                result.ShipperName = shipper.FullName;
                result.ShipperPhone = shipper.Phone;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Lỗi: " + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Auto-assign shipper cho đơn hàng
        /// </summary>
        public AssignmentResult AutoAssignShipper(int orderId)
        {
            var order = _db.CustomerOrder.Find(orderId);
            if (order == null)
            {
                return new AssignmentResult { Success = false, Message = "Không tìm thấy đơn hàng" };
            }

            var shipper = FindBestShipper();
            if (shipper == null)
            {
                return new AssignmentResult { Success = false, Message = "Không có shipper khả dụng" };
            }

            return AssignShipperToOrder(orderId, shipper.Id);
        }

        /// <summary>
        /// Gán đơn hàng cho bên thứ 3 (Grab, ShopeeFood, ...)
        /// </summary>
        public AssignmentResult AssignToThirdParty(AssignThirdPartyDto dto)
        {
            var result = new AssignmentResult();

            try
            {
                var order = _db.CustomerOrder.Find(dto.OrderId);
                if (order == null)
                {
                    result.Success = false;
                    result.Message = "Không tìm thấy đơn hàng";
                    return result;
                }

                // Kiểm tra đã có assignment chưa
                var existingAssignment = _db.DeliveryAssignment
                    .FirstOrDefault(a => a.OrderId == dto.OrderId &&
                        a.Status != "Cancelled" && a.Status != "Failed");

                if (existingAssignment != null)
                {
                    result.Success = false;
                    result.Message = "Đơn hàng đã được gán shipper";
                    return result;
                }

                // Tạo assignment cho bên thứ 3
                var notes = BuildThirdPartyNotes(
                    dto.ThirdPartyName, 
                    dto.ThirdPartyOrderCode, 
                    dto.ThirdPartyShipperName, 
                    dto.ThirdPartyShipperPhone, 
                    dto.Notes);

                var assignment = new DeliveryAssignment
                {
                    OrderId = dto.OrderId,
                    ShipperId = 0, // Không có shipper nội bộ
                    AssignedTime = DateTime.Now,
                    Status = "AssignedToThirdParty",
                    DeliveryFee = order.DeliveryFee,
                    ShipperEarning = 0, // Bên thứ 3 tự xử lý
                    Notes = notes,
                    EstimatedArrival = DateTime.Now.AddMinutes(45)
                };


                _db.DeliveryAssignment.Add(assignment);

                // Cập nhật trạng thái đơn hàng
                order.Status = "Delivering";
                order.DeliveringDate = DateTime.Now;

                _db.SaveChanges();

                // Gửi thông báo cập nhật trạng thái giao hàng
                _notificationService.UpdateDeliveryStatus(
                    order.Id,
                    order.OrderCode,
                    "Delivering",
                    0, // Không có shipper nội bộ
                    order.CustomerPhone
                );

                result.Success = true;
                result.Message = $"Đã chuyển đơn hàng cho {dto.ThirdPartyName}";
                result.AssignmentId = assignment.Id;
                result.ShipperName = dto.ThirdPartyShipperName ?? dto.ThirdPartyName;
                result.ShipperPhone = dto.ThirdPartyShipperPhone;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Lỗi: " + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Cập nhật thông tin shipper bên thứ 3
        /// </summary>
        public bool UpdateThirdPartyInfo(int assignmentId, string shipperName, string shipperPhone, string orderCode)
        {
            try
            {
                var assignment = _db.DeliveryAssignment.Find(assignmentId);
                if (assignment == null) return false;

                // Giữ lại tên hãng giao hàng từ Notes cũ
                var existingThirdPartyName = assignment.GetThirdPartyNameFromNotes();
                assignment.Notes = BuildThirdPartyNotes(
                    existingThirdPartyName, orderCode, shipperName, shipperPhone, null);
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách đơn hàng đang giao bởi nhân viên shop
        /// </summary>
        public List<ShopEmployeeDeliveryViewModel> GetShopEmployeeActiveDeliveries(int? shipperId = null)
        {
            var query = _db.DeliveryAssignment
                .Include(a => a.CustomerOrder)
                .Include(a => a.CustomerOrder.CustomerOrderDetail)
                .Include(a => a.Shipper)
                .Where(a => (a.Status == "Assigned" || a.Status == "Accepted" || 
                            a.Status == "PickedUp" || a.Status == "Delivering"));

            if (shipperId.HasValue)
            {
                query = query.Where(a => a.ShipperId == shipperId.Value);
            }

            return query
                .OrderByDescending(a => a.AssignedTime)
                .ToList()
                .Select(a => new ShopEmployeeDeliveryViewModel
                {
                    AssignmentId = a.Id,
                    OrderId = a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    CustomerPhone = a.CustomerOrder.CustomerPhone,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    District = a.CustomerOrder.District,
                    Ward = a.CustomerOrder.Ward,
                    TotalAmount = a.CustomerOrder.TotalAmount,
                    DeliveryFee = a.DeliveryFee,
                    PaymentMethod = a.CustomerOrder.PaymentMethod,
                    PaymentStatus = a.CustomerOrder.PaymentStatus,
                    Note = a.CustomerOrder.Note,
                    Status = a.Status,
                    OrderDate = a.CustomerOrder.OrderDate,
                    AssignedTime = a.AssignedTime,
                    PickupTime = a.PickupTime,
                    EstimatedArrival = a.EstimatedArrival,
                    DeliveryType = DeliveryType.ShopEmployee,
                    DistanceKm = a.ActualDistance,
                    EmployeeName = a.Shipper?.FullName,
                    Items = a.CustomerOrder.CustomerOrderDetail?.Select(d => new OrderItemSummary
                    {
                        ItemName = d.ItemName,
                        Quantity = d.Quantity,
                        Price = d.UnitPrice,
                        SpecialInstructions = d.SpecialInstructions
                    }).ToList() ?? new List<OrderItemSummary>()
                })
                .ToList();
        }

        /// <summary>
        /// Lấy danh sách đơn hàng đang giao bởi bên thứ 3
        /// </summary>
        public List<DeliveryOrderItemViewModel> GetThirdPartyActiveDeliveries()
        {
            return _db.DeliveryAssignment
                .Include(a => a.CustomerOrder)
                .Where(a => a.Status == "AssignedToThirdParty" || 
                           (a.Status == "Delivering" && a.ShipperId == 0))
                .OrderByDescending(a => a.AssignedTime)
                .ToList()
                .Select(a => new DeliveryOrderItemViewModel
                {
                    OrderId = a.OrderId,
                    OrderCode = a.CustomerOrder.OrderCode,
                    CustomerName = a.CustomerOrder.CustomerName,
                    CustomerPhone = a.CustomerOrder.CustomerPhone,
                    DeliveryAddress = a.CustomerOrder.DeliveryAddress,
                    District = a.CustomerOrder.District,
                    Ward = a.CustomerOrder.Ward,
                    TotalAmount = a.CustomerOrder.TotalAmount,
                    DeliveryFee = a.DeliveryFee,
                    PaymentMethod = a.CustomerOrder.PaymentMethod,
                    PaymentStatus = a.CustomerOrder.PaymentStatus,
                    Status = a.Status,
                    OrderDate = a.CustomerOrder.OrderDate,
                    EstimatedDeliveryTime = a.EstimatedArrival,
                    Note = a.CustomerOrder.Note,
                    DeliveryType = DeliveryType.ThirdParty,
                    DeliveryStatus = a.Status,
                    ThirdPartyName = a.GetThirdPartyNameFromNotes(),
                    ThirdPartyOrderCode = a.GetThirdPartyOrderCodeFromNotes(),
                    ThirdPartyShipperName = a.GetThirdPartyShipperNameFromNotes(),
                    ThirdPartyShipperPhone = a.GetThirdPartyShipperPhoneFromNotes()
                })
                .ToList();
        }

        /// <summary>
        /// Lấy đơn hàng chờ giao
        /// </summary>
        public List<DeliveryOrderItemViewModel> GetPendingDeliveryOrders()
        {
            return _db.CustomerOrder
                .Include(o => o.CustomerOrderDetail)
                .Where(o => o.OrderType == "Delivery" && 
                           (o.Status == "Confirmed" || o.Status == "Ready"))
                .OrderBy(o => o.OrderDate)
                .ToList()
                .Select(o => new DeliveryOrderItemViewModel
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
                    Items = o.CustomerOrderDetail?.Select(d => new OrderItemSummary
                    {
                        ItemName = d.ItemName,
                        Quantity = d.Quantity,
                        Price = d.UnitPrice,
                        SpecialInstructions = d.SpecialInstructions
                    }).ToList() ?? new List<OrderItemSummary>()
                })
                .ToList();
        }

        /// <summary>
        /// Lấy ViewModel quản lý giao hàng tổng hợp
        /// </summary>
        public DeliveryManagementViewModel GetDeliveryManagementData()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            return new DeliveryManagementViewModel
            {
                PendingOrders = GetPendingDeliveryOrders(),
                ShopDeliveryOrders = GetShopEmployeeActiveDeliveries()
                    .Select(s => new DeliveryOrderItemViewModel
                    {
                        OrderId = s.OrderId,
                        OrderCode = s.OrderCode,
                        CustomerName = s.CustomerName,
                        CustomerPhone = s.CustomerPhone,
                        DeliveryAddress = s.DeliveryAddress,
                        District = s.District,
                        Ward = s.Ward,
                        TotalAmount = s.TotalAmount,
                        DeliveryFee = s.DeliveryFee,
                        PaymentMethod = s.PaymentMethod,
                        Status = s.Status,
                        OrderDate = s.OrderDate,
                        EstimatedDeliveryTime = s.EstimatedArrival,
                        ShipperName = s.EmployeeName,
                        DeliveryType = DeliveryType.ShopEmployee,
                        DeliveryStatus = s.Status,
                        DistanceKm = s.DistanceKm
                    }).ToList(),
                ThirdPartyOrders = GetThirdPartyActiveDeliveries(),
                AvailableShopShippers = _db.Shipper
                    .Where(s => s.IsActive && s.Status == "Available")
                    .OrderByDescending(s => s.Rating)
                    .ToList(),
                FeeConfigs = GetDistanceFeeConfigs(),
                TotalPending = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && 
                               (o.Status == "Confirmed" || o.Status == "Ready")),
                TotalDelivering = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && o.Status == "Delivering"),
                TotalCompletedToday = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && 
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow)
            };
        }

        #endregion

        #region Delivery Status Updates

        /// <summary>
        /// Cập nhật trạng thái giao hàng
        /// </summary>
        public bool UpdateDeliveryStatus(int assignmentId, string newStatus, string notes = null, string proofImage = null, string failureReason = null)
        {
            try
            {
                var assignment = _db.DeliveryAssignment
                    .Include(a => a.CustomerOrder)
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.Id == assignmentId);

                if (assignment == null) return false;

                assignment.Status = newStatus;
                if (!string.IsNullOrEmpty(notes))
                    assignment.Notes = notes;

                switch (newStatus)
                {
                    case "Accepted":
                        // Shipper đã nhận đơn
                        break;

                    case "PickedUp":
                        assignment.PickupTime = DateTime.Now;
                        assignment.CustomerOrder.Status = "Delivering";
                        assignment.CustomerOrder.DeliveringDate = DateTime.Now;
                        break;

                    case "Delivering":
                        // Đang trên đường giao
                        break;

                    case "Delivered":
                        assignment.DeliveryTime = DateTime.Now;
                        assignment.ProofImageUrl = proofImage;
                        assignment.CustomerOrder.Status = "Completed";
                        assignment.CustomerOrder.CompletedDate = DateTime.Now;
                        
                        // Cập nhật shipper
                        assignment.Shipper.Status = "Available";
                        assignment.Shipper.TotalDeliveries++;
                        assignment.Shipper.TotalEarnings += assignment.ShipperEarning;

                        // Cập nhật payment nếu COD
                        if (assignment.CustomerOrder.PaymentMethod == "COD")
                        {
                            assignment.CustomerOrder.PaymentStatus = "Paid";
                            assignment.CustomerOrder.PaidDate = DateTime.Now;
                        }
                        break;

                    case "Failed":
                        assignment.FailureReason = failureReason;
                        assignment.Shipper.Status = "Available";
                        assignment.CustomerOrder.Status = "Ready"; // Quay về trạng thái sẵn sàng để giao lại
                        break;

                    case "Cancelled":
                        assignment.Shipper.Status = "Available";
                        break;
                }

                _db.SaveChanges();

                // Gửi thông báo real-time về trạng thái giao hàng
                _notificationService.UpdateDeliveryStatus(
                    assignment.OrderId,
                    assignment.CustomerOrder.OrderCode,
                    newStatus,
                    assignment.ShipperId,
                    assignment.CustomerOrder.CustomerPhone
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng
        /// </summary>
        public bool UpdateOrderStatus(int orderId, string newStatus)
        {
            try
            {
                var order = _db.CustomerOrder.Find(orderId);
                if (order == null) return false;

                order.Status = newStatus;

                switch (newStatus)
                {
                    case "Confirmed":
                        order.ConfirmedDate = DateTime.Now;
                        break;
                    case "Preparing":
                        order.PreparingDate = DateTime.Now;
                        break;
                    case "Ready":
                        order.ReadyDate = DateTime.Now;
                        break;
                    case "Delivering":
                        order.DeliveringDate = DateTime.Now;
                        break;
                    case "Completed":
                        order.CompletedDate = DateTime.Now;
                        if (order.PaymentMethod == "COD")
                        {
                            order.PaymentStatus = "Paid";
                            order.PaidDate = DateTime.Now;
                        }
                        break;
                    case "Cancelled":
                        order.CancelledDate = DateTime.Now;
                        break;
                }

                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Rating

        /// <summary>
        /// Đánh giá shipper sau khi giao hàng
        /// </summary>
        public bool RateDelivery(int assignmentId, int rating, string feedback)
        {
            try
            {
                var assignment = _db.DeliveryAssignment
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.Id == assignmentId);

                if (assignment == null || assignment.Status != "Delivered")
                    return false;

                assignment.CustomerRating = rating;
                assignment.CustomerFeedback = feedback;

                // Cập nhật rating trung bình của shipper
                var allRatings = _db.DeliveryAssignment
                    .Where(a => a.ShipperId == assignment.ShipperId && a.CustomerRating.HasValue)
                    .Select(a => a.CustomerRating.Value)
                    .ToList();

                allRatings.Add(rating);
                assignment.Shipper.Rating = (decimal)allRatings.Average();

                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Lấy thống kê dashboard
        /// </summary>
        public DeliveryDashboardViewModel GetDashboardStats()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var viewModel = new DeliveryDashboardViewModel
            {
                PendingOrders = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && 
                               (o.Status == "Confirmed" || o.Status == "Ready")),

                DeliveringOrders = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && o.Status == "Delivering"),

                CompletedOrdersToday = _db.CustomerOrder
                    .Count(o => o.OrderType == "Delivery" && 
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow),

                FailedOrdersToday = _db.DeliveryAssignment
                    .Count(a => a.Status == "Failed" &&
                               a.AssignedTime >= today && a.AssignedTime < tomorrow),

                AvailableShippers = _db.Shipper
                    .Count(s => s.Status == "Available" && s.IsActive),

                BusyShippers = _db.Shipper
                    .Count(s => s.Status == "Busy" && s.IsActive),

                TotalShippers = _db.Shipper.Count(s => s.IsActive),

                TodayRevenue = _db.CustomerOrder
                    .Where(o => o.OrderType == "Delivery" &&
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0,

                TodayDeliveryFees = _db.CustomerOrder
                    .Where(o => o.OrderType == "Delivery" &&
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow)
                    .Sum(o => (decimal?)o.DeliveryFee) ?? 0
            };

            // Recent deliveries
            viewModel.RecentDeliveries = _db.CustomerOrder
                .Where(o => o.OrderType == "Delivery")
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new RecentDeliveryViewModel
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    DeliveryAddress = o.DeliveryAddress,
                    Status = o.Status,
                    OrderDate = o.OrderDate,
                    EstimatedDelivery = o.EstimatedDeliveryTime,
                    TotalAmount = o.TotalAmount
                })
                .ToList();

            // Shipper statuses
            viewModel.ShipperStatuses = _db.Shipper
                .Where(s => s.IsActive)
                .Select(s => new ShipperStatusViewModel
                {
                    ShipperId = s.Id,
                    FullName = s.FullName,
                    Phone = s.Phone,
                    Status = s.Status,
                    VehicleType = s.VehicleType,
                    Rating = s.Rating,
                    Latitude = s.CurrentLatitude,
                    Longitude = s.CurrentLongitude
                })
                .ToList();

            return viewModel;
        }

        /// <summary>
        /// Lấy thống kê chi tiết
        /// </summary>
        public DeliveryStatisticsViewModel GetStatistics(DateTime fromDate, DateTime toDate)
        {
            var nextDay = toDate.AddDays(1);

            var orders = _db.CustomerOrder
                .Where(o => o.OrderType == "Delivery" &&
                           o.OrderDate >= fromDate && o.OrderDate < nextDay)
                .ToList();

            var viewModel = new DeliveryStatisticsViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalOrders = orders.Count,
                CompletedOrders = orders.Count(o => o.Status == "Completed"),
                FailedOrders = _db.DeliveryAssignment
                    .Count(a => a.Status == "Failed" &&
                               a.AssignedTime >= fromDate && a.AssignedTime < nextDay),
                CancelledOrders = orders.Count(o => o.Status == "Cancelled"),
                TotalRevenue = orders.Where(o => o.Status == "Completed").Sum(o => o.TotalAmount),
                TotalDeliveryFees = orders.Where(o => o.Status == "Completed").Sum(o => o.DeliveryFee)
            };

            if (viewModel.TotalOrders > 0)
            {
                viewModel.SuccessRate = (decimal)viewModel.CompletedOrders / viewModel.TotalOrders * 100;
                viewModel.AverageOrderValue = viewModel.TotalRevenue / viewModel.CompletedOrders;
            }

            return viewModel;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Tính khoảng cách giữa 2 điểm (Haversine formula)
        /// </summary>
        private double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            const double R = 6371; // Radius of Earth in km

            var dLat = ToRadians((double)(lat2 - lat1));
            var dLon = ToRadians((double)(lon2 - lon1));

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians((double)lat1)) * Math.Cos(ToRadians((double)lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        /// <summary>
        /// Xây dựng chuỗi Notes cấu trúc cho đơn giao bên thứ 3
        /// </summary>
        private string BuildThirdPartyNotes(string thirdPartyName, string orderCode, string shipperName, string shipperPhone, string additionalNotes)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(thirdPartyName))
                parts.Add($"[Hang:{thirdPartyName}]");
            if (!string.IsNullOrEmpty(orderCode))
                parts.Add($"[Ma don:{orderCode}]");
            if (!string.IsNullOrEmpty(shipperName))
                parts.Add($"[Shipper:{shipperName}]");
            if (!string.IsNullOrEmpty(shipperPhone))
                parts.Add($"[SDT:{shipperPhone}]");
            if (!string.IsNullOrEmpty(additionalNotes))
                parts.Add(additionalNotes);

            return string.Join(" ", parts);
        }

        #endregion

        public void Dispose()
        {
            _db?.Dispose();
        }
    }

    #region Result Classes

    public class AssignmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? AssignmentId { get; set; }
        public string ShipperName { get; set; }
        public string ShipperPhone { get; set; }
    }

    #endregion
}
