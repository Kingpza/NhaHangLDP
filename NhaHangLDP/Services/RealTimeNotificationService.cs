using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using NhaHangLDP.Hubs;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý gửi thông báo real-time qua SignalR
    /// </summary>
    public class RealTimeNotificationService
    {
        private readonly IHubContext _hubContext;

        public RealTimeNotificationService()
        {
            _hubContext = GlobalHost.ConnectionManager.GetHubContext<RestaurantHub>();
        }

        #region Order Notifications

        /// <summary>
        /// Thông báo có đơn hàng mới cho bếp
        /// </summary>
        public void NotifyNewOrderToKitchen(int orderId, string tableNumber, int itemCount, string specialNotes = null)
        {
            var data = new
            {
                orderId = orderId,
                tableNumber = tableNumber,
                itemCount = itemCount,
                specialNotes = specialNotes,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                message = $"Đơn hàng mới từ bàn {tableNumber}"
            };

            _hubContext.Clients.Group("Kitchen").receiveNewOrder(data);
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng
        /// </summary>
        public void UpdateOrderStatus(int orderId, string status, string tableNumber)
        {
            var data = new
            {
                orderId = orderId,
                status = status,
                tableNumber = tableNumber,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Cashier").onOrderStatusChanged(data);
            _hubContext.Clients.Group("Manager").onOrderStatusChanged(data);
            _hubContext.Clients.Group("Admin").onOrderStatusChanged(data);
        }

        /// <summary>
        /// Thông báo đơn hàng đã sẵn sàng
        /// </summary>
        public void NotifyOrderReady(int orderId, string tableNumber, string ticketCode)
        {
            var data = new
            {
                orderId = orderId,
                tableNumber = tableNumber,
                ticketCode = ticketCode,
                message = $"Bàn {tableNumber} - Đơn #{ticketCode} đã sẵn sàng!",
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Cashier").onOrderReady(data);
            _hubContext.Clients.Group("Waiter").onOrderReady(data);
        }

        #endregion

        #region Kitchen Ticket Notifications

        /// <summary>
        /// Gửi ticket mới đến bếp
        /// </summary>
        public void SendNewTicketToKitchen(KitchenTicketNotification ticket)
        {
            var data = new
            {
                ticketId = ticket.TicketId,
                ticketCode = ticket.TicketCode,
                orderId = ticket.OrderId,
                tableNumber = ticket.TableNumber,
                itemCount = ticket.ItemCount,
                priority = ticket.Priority,
                specialNotes = ticket.SpecialNotes,
                estimatedMinutes = ticket.EstimatedMinutes,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                items = ticket.Items
            };

            // Gửi đến station cụ thể nếu có
            if (!string.IsNullOrEmpty(ticket.StationCode))
            {
                _hubContext.Clients.Group($"kitchen_{ticket.StationCode}").receiveNewTicket(data);
            }
            else
            {
                _hubContext.Clients.Group("Kitchen").receiveNewTicket(data);
            }
        }

        /// <summary>
        /// Cập nhật trạng thái ticket
        /// </summary>
        public void UpdateTicketStatus(int ticketId, string ticketCode, string status, string tableNumber = null)
        {
            var data = new
            {
                ticketId = ticketId,
                ticketCode = ticketCode,
                status = status,
                tableNumber = tableNumber,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Kitchen").onTicketStatusChanged(data);
            _hubContext.Clients.Group("Cashier").onTicketStatusChanged(data);

            // Nếu ticket đã hoàn thành, thông báo thêm
            if (status == "Ready" || status == "Completed")
            {
                _hubContext.Clients.Group("Waiter").onTicketReady(data);
            }
        }

        /// <summary>
        /// Cập nhật món đã hoàn thành trong ticket
        /// </summary>
        public void UpdateItemCompleted(int ticketId, int itemId, string itemName, int completedQty, int totalQty)
        {
            var data = new
            {
                ticketId = ticketId,
                itemId = itemId,
                itemName = itemName,
                completedQuantity = completedQty,
                totalQuantity = totalQty,
                isCompleted = completedQty >= totalQty,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Kitchen").onItemCompleted(data);
        }

        #endregion

        #region Reservation Notifications

        /// <summary>
        /// Thông báo có đặt bàn mới
        /// </summary>
        public void NotifyNewReservation(ReservationNotification reservation)
        {
            var data = new
            {
                reservationId = reservation.ReservationId,
                reservationCode = reservation.ReservationCode,
                customerName = reservation.CustomerName,
                customerPhone = reservation.CustomerPhone,
                guestCount = reservation.GuestCount,
                reservationDate = reservation.ReservationDate,
                reservationTime = reservation.ReservationTime,
                tablePreference = reservation.TablePreference,
                specialRequests = reservation.SpecialRequests,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                message = $"Đặt bàn mới: {reservation.CustomerName} - {reservation.GuestCount} khách"
            };

            _hubContext.Clients.Group("Manager").onNewReservation(data);
            _hubContext.Clients.Group("Admin").onNewReservation(data);
            _hubContext.Clients.Group("Cashier").onNewReservation(data);
        }

        /// <summary>
        /// Cập nhật trạng thái đặt bàn
        /// </summary>
        public void UpdateReservationStatus(int reservationId, string status, string customerName)
        {
            var data = new
            {
                reservationId = reservationId,
                status = status,
                customerName = customerName,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Manager").onReservationStatusChanged(data);
            _hubContext.Clients.Group("Admin").onReservationStatusChanged(data);
        }

        #endregion

        #region Delivery Notifications

        /// <summary>
        /// Thông báo đơn giao hàng mới cho shipper
        /// </summary>
        public void NotifyNewDeliveryToShipper(int shipperId, DeliveryOrderNotification order)
        {
            var data = new
            {
                orderId = order.OrderId,
                orderCode = order.OrderCode,
                customerName = order.CustomerName,
                customerPhone = order.CustomerPhone,
                deliveryAddress = order.DeliveryAddress,
                district = order.District,
                totalAmount = order.TotalAmount,
                paymentMethod = order.PaymentMethod,
                deliveryFee = order.DeliveryFee,
                shipperEarning = order.ShipperEarning,
                estimatedTime = order.EstimatedTime,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                message = $"Đơn hàng mới: {order.DeliveryAddress}"
            };

            _hubContext.Clients.Group($"shipper_{shipperId}").receiveNewDeliveryOrder(data);
        }

        /// <summary>
        /// Cập nhật trạng thái giao hàng
        /// </summary>
        public void UpdateDeliveryStatus(int orderId, string orderCode, string status, int? shipperId = null, string customerPhone = null)
        {
            var data = new
            {
                orderId = orderId,
                orderCode = orderCode,
                status = status,
                statusText = GetDeliveryStatusText(status),
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            // Thông báo cho quản lý
            _hubContext.Clients.Group("Manager").onDeliveryStatusChanged(data);
            _hubContext.Clients.Group("Admin").onDeliveryStatusChanged(data);
            _hubContext.Clients.Group("Delivery").onDeliveryStatusChanged(data);

            // Thông báo cho shipper cụ thể
            if (shipperId.HasValue)
            {
                _hubContext.Clients.Group($"shipper_{shipperId}").onDeliveryStatusChanged(data);
            }

            // Thông báo cho khách hàng nếu có
            if (!string.IsNullOrEmpty(customerPhone))
            {
                _hubContext.Clients.Group($"customer_{customerPhone}").onDeliveryStatusChanged(data);
            }
        }

        /// <summary>
        /// Cập nhật vị trí shipper
        /// </summary>
        public void UpdateShipperLocation(int shipperId, string shipperName, decimal latitude, decimal longitude, int? currentOrderId = null)
        {
            var data = new
            {
                shipperId = shipperId,
                shipperName = shipperName,
                latitude = latitude,
                longitude = longitude,
                currentOrderId = currentOrderId,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Manager").onShipperLocationUpdated(data);
            _hubContext.Clients.Group("Delivery").onShipperLocationUpdated(data);

            // Gửi đến khách hàng đang chờ đơn nếu có
            if (currentOrderId.HasValue)
            {
                _hubContext.Clients.Group($"order_{currentOrderId}").onShipperLocationUpdated(data);
            }
        }

        #endregion

        #region Dashboard & Revenue Updates

        /// <summary>
        /// Cập nhật dashboard real-time
        /// </summary>
        public void UpdateDashboard(DashboardUpdateNotification dashboard)
        {
            var data = new
            {
                totalOrdersToday = dashboard.TotalOrdersToday,
                todayRevenue = dashboard.TodayRevenue,
                activeOrders = dashboard.ActiveOrders,
                pendingOrders = dashboard.PendingOrders,
                occupiedTables = dashboard.OccupiedTables,
                totalTables = dashboard.TotalTables,
                pendingDeliveries = dashboard.PendingDeliveries,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Manager").onDashboardUpdate(data);
            _hubContext.Clients.Group("Admin").onDashboardUpdate(data);
        }

        /// <summary>
        /// Cập nhật doanh thu khi có thanh toán mới
        /// </summary>
        public void UpdateRevenue(decimal paymentAmount, decimal todayRevenue, string paymentMethod, string tableNumber = null)
        {
            var data = new
            {
                paymentAmount = paymentAmount,
                todayRevenue = todayRevenue,
                paymentMethod = paymentMethod,
                tableNumber = tableNumber,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                message = tableNumber != null 
                    ? $"Bàn {tableNumber}: +{paymentAmount:N0}₫" 
                    : $"Thanh toán mới: +{paymentAmount:N0}₫"
            };

            _hubContext.Clients.Group("Manager").onRevenueUpdate(data);
            _hubContext.Clients.Group("Admin").onRevenueUpdate(data);
            _hubContext.Clients.Group("Cashier").onRevenueUpdate(data);
        }

        /// <summary>
        /// Cập nhật trạng thái bàn
        /// </summary>
        public void UpdateTableStatus(int tableId, string tableNumber, string status, int? orderId = null)
        {
            var data = new
            {
                tableId = tableId,
                tableNumber = tableNumber,
                status = status,
                statusText = GetTableStatusText(status),
                orderId = orderId,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.All.onTableStatusChanged(data);
        }

        #endregion

        #region Chat Support

        /// <summary>
        /// Gửi tin nhắn từ khách hàng đến support
        /// </summary>
        public void SendCustomerMessageToSupport(string customerId, string customerName, string message)
        {
            var chatMessage = new
            {
                customerId = customerId,
                customerName = customerName,
                message = message,
                isFromCustomer = true,
                timestamp = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };

            _hubContext.Clients.Group("Support").receiveCustomerMessage(chatMessage);
            _hubContext.Clients.Group("Manager").receiveCustomerMessage(chatMessage);
        }

        /// <summary>
        /// Gửi phản hồi từ support đến khách hàng
        /// </summary>
        public void SendSupportReplyToCustomer(string customerId, string staffId, string staffName, string message)
        {
            var replyMessage = new
            {
                staffId = staffId,
                staffName = staffName,
                message = message,
                isFromCustomer = false,
                timestamp = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };

            _hubContext.Clients.Group($"user_{customerId}").receiveSupportReply(replyMessage);
        }

        /// <summary>
        /// Thông báo có tin nhắn mới chưa đọc
        /// </summary>
        public void NotifyUnreadMessages(string userId, int unreadCount)
        {
            var data = new
            {
                unreadCount = unreadCount
            };

            _hubContext.Clients.Group($"user_{userId}").onUnreadMessagesCountChanged(data);
        }

        #endregion

        #region General Notifications

        /// <summary>
        /// Gửi thông báo đến một nhóm
        /// </summary>
        public void SendNotificationToGroup(string groupName, string title, string message, string level = "info", string actionUrl = null)
        {
            var notification = new
            {
                title = title,
                message = message,
                level = level,
                actionUrl = actionUrl,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group(groupName).receiveNotification(notification);
        }

        /// <summary>
        /// Gửi thông báo đến một user cụ thể
        /// </summary>
        public void SendNotificationToUser(string userId, string title, string message, string level = "info", string actionUrl = null)
        {
            var notification = new
            {
                title = title,
                message = message,
                level = level,
                actionUrl = actionUrl,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group($"user_{userId}").receiveNotification(notification);
        }

        /// <summary>
        /// Broadcast thông báo cho tất cả
        /// </summary>
        public void BroadcastNotification(string title, string message, string level = "info")
        {
            var notification = new
            {
                title = title,
                message = message,
                level = level,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.All.receiveNotification(notification);
        }

        #endregion

        #region Inventory Alerts

        /// <summary>
        /// Cảnh báo tồn kho thấp
        /// </summary>
        public void NotifyLowStock(string ingredientName, string unit, decimal currentStock, decimal threshold)
        {
            var alert = new
            {
                ingredientName = ingredientName,
                unit = unit,
                currentStock = currentStock,
                threshold = threshold,
                level = currentStock <= 0 ? "error" : "warning",
                message = currentStock <= 0 
                    ? $"HẾT HÀNG: {ingredientName}" 
                    : $"Cảnh báo: {ingredientName} còn {currentStock} {unit}",
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Manager").onLowStockAlert(alert);
            _hubContext.Clients.Group("Admin").onLowStockAlert(alert);
            _hubContext.Clients.Group("Kitchen").onLowStockAlert(alert);
        }

        /// <summary>
        /// Cảnh báo nguyên liệu sắp hết hạn
        /// </summary>
        public void NotifyExpiringItem(string ingredientName, DateTime expiryDate, int daysLeft)
        {
            var alert = new
            {
                ingredientName = ingredientName,
                expiryDate = expiryDate.ToString("dd/MM/yyyy"),
                daysLeft = daysLeft,
                level = daysLeft <= 3 ? "error" : "warning",
                message = $"{ingredientName} hết hạn sau {daysLeft} ngày",
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _hubContext.Clients.Group("Manager").onExpiringItemAlert(alert);
            _hubContext.Clients.Group("Admin").onExpiringItemAlert(alert);
        }

        #endregion

        #region Helper Methods

        private string GetDeliveryStatusText(string status)
        {
            switch (status)
            {
                case "Pending": return "Chờ xác nhận";
                case "Confirmed": return "Đã xác nhận";
                case "Preparing": return "Đang chuẩn bị";
                case "Ready": return "Sẵn sàng giao";
                case "Assigned": return "Đã gán shipper";
                case "Accepted": return "Shipper đã nhận";
                case "PickedUp": return "Đã lấy hàng";
                case "Delivering": return "Đang giao";
                case "Delivered": return "Đã giao";
                case "Failed": return "Giao thất bại";
                case "Cancelled": return "Đã hủy";
                default: return status;
            }
        }

        private string GetTableStatusText(string status)
        {
            switch (status)
            {
                case "Available": return "Trống";
                case "Occupied": return "Có khách";
                case "Reserved": return "Đã đặt";
                case "Cleaning": return "Đang dọn";
                default: return status;
            }
        }

        #endregion
    }

    #region Notification DTOs

    /// <summary>
    /// DTO cho thông báo ticket bếp
    /// </summary>
    public class KitchenTicketNotification
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public int OrderId { get; set; }
        public string TableNumber { get; set; }
        public int ItemCount { get; set; }
        public int Priority { get; set; }
        public string SpecialNotes { get; set; }
        public int EstimatedMinutes { get; set; }
        public string StationCode { get; set; }
        public object Items { get; set; }
    }

    /// <summary>
    /// DTO cho thông báo đặt bàn
    /// </summary>
    public class ReservationNotification
    {
        public int ReservationId { get; set; }
        public string ReservationCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int GuestCount { get; set; }
        public string ReservationDate { get; set; }
        public string ReservationTime { get; set; }
        public string TablePreference { get; set; }
        public string SpecialRequests { get; set; }
    }

    /// <summary>
    /// DTO cho thông báo đơn giao hàng
    /// </summary>
    public class DeliveryOrderNotification
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string District { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal ShipperEarning { get; set; }
        public string EstimatedTime { get; set; }
    }

    /// <summary>
    /// DTO cho cập nhật dashboard
    /// </summary>
    public class DashboardUpdateNotification
    {
        public int TotalOrdersToday { get; set; }
        public decimal TodayRevenue { get; set; }
        public int ActiveOrders { get; set; }
        public int PendingOrders { get; set; }
        public int OccupiedTables { get; set; }
        public int TotalTables { get; set; }
        public int PendingDeliveries { get; set; }
    }

    #endregion
}
