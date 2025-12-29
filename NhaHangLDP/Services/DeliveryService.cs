using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý logic nghiệp vụ giao hàng
    /// </summary>
    public class DeliveryService
    {
        private readonly MyDbContext _db;

        public DeliveryService()
        {
            _db = new MyDbContext();
        }

        public DeliveryService(MyDbContext db)
        {
            _db = db;
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
                var zone = _db.DeliveryZones
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
            var zone = _db.DeliveryZones
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
            var query = _db.Shippers
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
            return _db.Shippers
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
                var shipper = _db.Shippers.Find(shipperId);
                if (shipper == null) return false;

                shipper.CurrentLatitude = latitude;
                shipper.CurrentLongitude = longitude;
                shipper.LastLocationUpdate = DateTime.Now;

                // Cập nhật vị trí trong assignment đang active
                var activeAssignment = _db.DeliveryAssignments
                    .FirstOrDefault(a => a.ShipperId == shipperId &&
                        (a.Status == "Accepted" || a.Status == "PickedUp" || a.Status == "Delivering"));

                if (activeAssignment != null)
                {
                    activeAssignment.CurrentLatitude = latitude;
                    activeAssignment.CurrentLongitude = longitude;
                }

                _db.SaveChanges();
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
                var shipper = _db.Shippers.Find(shipperId);
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
        /// Gán shipper cho đơn hàng
        /// </summary>
        public AssignmentResult AssignShipperToOrder(int orderId, int shipperId)
        {
            var result = new AssignmentResult();

            try
            {
                var order = _db.CustomerOrders.Find(orderId);
                if (order == null)
                {
                    result.Success = false;
                    result.Message = "Không tìm thấy đơn hàng";
                    return result;
                }

                var shipper = _db.Shippers.Find(shipperId);
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
                var existingAssignment = _db.DeliveryAssignments
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

                _db.DeliveryAssignments.Add(assignment);

                // Cập nhật trạng thái
                shipper.Status = "Busy";
                
                if (order.Status == "Confirmed" || order.Status == "Ready")
                {
                    order.Status = "Delivering";
                    order.DeliveringDate = DateTime.Now;
                    order.EstimatedDeliveryTime = assignment.EstimatedArrival;
                }

                _db.SaveChanges();

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
            var order = _db.CustomerOrders.Find(orderId);
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

        #endregion

        #region Delivery Status Updates

        /// <summary>
        /// Cập nhật trạng thái giao hàng
        /// </summary>
        public bool UpdateDeliveryStatus(int assignmentId, string newStatus, string notes = null, string proofImage = null, string failureReason = null)
        {
            try
            {
                var assignment = _db.DeliveryAssignments
                    .Include(a => a.Order)
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
                        assignment.Order.Status = "Delivering";
                        assignment.Order.DeliveringDate = DateTime.Now;
                        break;

                    case "Delivering":
                        // Đang trên đường giao
                        break;

                    case "Delivered":
                        assignment.DeliveryTime = DateTime.Now;
                        assignment.ProofImageUrl = proofImage;
                        assignment.Order.Status = "Completed";
                        assignment.Order.CompletedDate = DateTime.Now;
                        
                        // Cập nhật shipper
                        assignment.Shipper.Status = "Available";
                        assignment.Shipper.TotalDeliveries++;
                        assignment.Shipper.TotalEarnings += assignment.ShipperEarning;

                        // Cập nhật payment nếu COD
                        if (assignment.Order.PaymentMethod == "COD")
                        {
                            assignment.Order.PaymentStatus = "Paid";
                            assignment.Order.PaidDate = DateTime.Now;
                        }
                        break;

                    case "Failed":
                        assignment.FailureReason = failureReason;
                        assignment.Shipper.Status = "Available";
                        assignment.Order.Status = "Ready"; // Quay về trạng thái sẵn sàng để giao lại
                        break;

                    case "Cancelled":
                        assignment.Shipper.Status = "Available";
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

        /// <summary>
        /// Cập nhật trạng thái đơn hàng
        /// </summary>
        public bool UpdateOrderStatus(int orderId, string newStatus)
        {
            try
            {
                var order = _db.CustomerOrders.Find(orderId);
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
                var assignment = _db.DeliveryAssignments
                    .Include(a => a.Shipper)
                    .FirstOrDefault(a => a.Id == assignmentId);

                if (assignment == null || assignment.Status != "Delivered")
                    return false;

                assignment.CustomerRating = rating;
                assignment.CustomerFeedback = feedback;

                // Cập nhật rating trung bình của shipper
                var allRatings = _db.DeliveryAssignments
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
                PendingOrders = _db.CustomerOrders
                    .Count(o => o.OrderType == "Delivery" && 
                               (o.Status == "Confirmed" || o.Status == "Ready")),

                DeliveringOrders = _db.CustomerOrders
                    .Count(o => o.OrderType == "Delivery" && o.Status == "Delivering"),

                CompletedOrdersToday = _db.CustomerOrders
                    .Count(o => o.OrderType == "Delivery" && 
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow),

                FailedOrdersToday = _db.DeliveryAssignments
                    .Count(a => a.Status == "Failed" &&
                               a.AssignedTime >= today && a.AssignedTime < tomorrow),

                AvailableShippers = _db.Shippers
                    .Count(s => s.Status == "Available" && s.IsActive),

                BusyShippers = _db.Shippers
                    .Count(s => s.Status == "Busy" && s.IsActive),

                TotalShippers = _db.Shippers.Count(s => s.IsActive),

                TodayRevenue = _db.CustomerOrders
                    .Where(o => o.OrderType == "Delivery" &&
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0,

                TodayDeliveryFees = _db.CustomerOrders
                    .Where(o => o.OrderType == "Delivery" &&
                               o.Status == "Completed" &&
                               o.CompletedDate >= today && o.CompletedDate < tomorrow)
                    .Sum(o => (decimal?)o.DeliveryFee) ?? 0
            };

            // Recent deliveries
            viewModel.RecentDeliveries = _db.CustomerOrders
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
            viewModel.ShipperStatuses = _db.Shippers
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

            var orders = _db.CustomerOrders
                .Where(o => o.OrderType == "Delivery" &&
                           o.OrderDate >= fromDate && o.OrderDate < nextDay)
                .ToList();

            var viewModel = new DeliveryStatisticsViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalOrders = orders.Count,
                CompletedOrders = orders.Count(o => o.Status == "Completed"),
                FailedOrders = _db.DeliveryAssignments
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
