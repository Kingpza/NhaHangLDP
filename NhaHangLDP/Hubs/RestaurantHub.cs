using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace NhaHangLDP.Hubs
{
    /// <summary>
    /// SignalR Hub chính cho nhà hàng - xử lý real-time communication
    /// </summary>
    public class RestaurantHub : Hub
    {
        #region Connection Management

        /// <summary>
        /// Khi client kết nối
        /// </summary>
        public override Task OnConnected()
        {
            var role = Context.QueryString["role"];
            var userId = Context.QueryString["userId"];

            // Thêm connection vào group theo role
            if (!string.IsNullOrEmpty(role))
            {
                Groups.Add(Context.ConnectionId, role);
            }

            // Thêm vào group cá nhân nếu có userId
            if (!string.IsNullOrEmpty(userId))
            {
                Groups.Add(Context.ConnectionId, $"user_{userId}");
            }

            return base.OnConnected();
        }

        /// <summary>
        /// Khi client ngắt kết nối
        /// </summary>
        public override Task OnDisconnected(bool stopCalled)
        {
            return base.OnDisconnected(stopCalled);
        }

        /// <summary>
        /// Khi client kết nối lại
        /// </summary>
        public override Task OnReconnected()
        {
            var role = Context.QueryString["role"];
            var userId = Context.QueryString["userId"];

            if (!string.IsNullOrEmpty(role))
            {
                Groups.Add(Context.ConnectionId, role);
            }

            if (!string.IsNullOrEmpty(userId))
            {
                Groups.Add(Context.ConnectionId, $"user_{userId}");
            }

            return base.OnReconnected();
        }

        #endregion

        #region Group Management

        /// <summary>
        /// Tham gia vào một group cụ thể
        /// </summary>
        public Task JoinGroup(string groupName)
        {
            return Groups.Add(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Rời khỏi group
        /// </summary>
        public Task LeaveGroup(string groupName)
        {
            return Groups.Remove(Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Tham gia vào group của bếp (theo station)
        /// </summary>
        public Task JoinKitchenStation(string stationCode)
        {
            return Groups.Add(Context.ConnectionId, $"kitchen_{stationCode}");
        }

        /// <summary>
        /// Tham gia vào group của shipper
        /// </summary>
        public Task JoinShipperGroup(int shipperId)
        {
            return Groups.Add(Context.ConnectionId, $"shipper_{shipperId}");
        }

        #endregion

        #region Order Notifications

        /// <summary>
        /// Thông báo đơn hàng mới cho bếp
        /// </summary>
        public void NotifyNewOrderToKitchen(object orderData)
        {
            Clients.Group("Kitchen").receiveNewOrder(orderData);
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

            // Thông báo cho thu ngân
            Clients.Group("Cashier").onOrderStatusChanged(data);

            // Thông báo cho quản lý
            Clients.Group("Manager").onOrderStatusChanged(data);
            Clients.Group("Admin").onOrderStatusChanged(data);
        }

        /// <summary>
        /// Đơn hàng sẵn sàng để phục vụ
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

            Clients.Group("Cashier").onOrderReady(data);
            Clients.Group("Waiter").onOrderReady(data);
        }

        #endregion

        #region Kitchen Notifications

        /// <summary>
        /// Gửi ticket mới đến bếp
        /// </summary>
        public void SendTicketToKitchen(object ticketData, string stationCode = null)
        {
            if (!string.IsNullOrEmpty(stationCode))
            {
                Clients.Group($"kitchen_{stationCode}").receiveNewTicket(ticketData);
            }
            else
            {
                Clients.Group("Kitchen").receiveNewTicket(ticketData);
            }
        }

        /// <summary>
        /// Cập nhật trạng thái ticket
        /// </summary>
        public void UpdateTicketStatus(int ticketId, string status, string ticketCode)
        {
            var data = new
            {
                ticketId = ticketId,
                ticketCode = ticketCode,
                status = status,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.Group("Kitchen").onTicketStatusChanged(data);
            Clients.Group("Cashier").onTicketStatusChanged(data);
        }

        /// <summary>
        /// Cập nhật món trong ticket đã hoàn thành
        /// </summary>
        public void UpdateItemCompleted(int ticketId, int itemId, int completedQty)
        {
            var data = new
            {
                ticketId = ticketId,
                itemId = itemId,
                completedQuantity = completedQty
            };

            Clients.Group("Kitchen").onItemCompleted(data);
        }

        #endregion

        #region Reservation Notifications

        /// <summary>
        /// Thông báo đặt bàn mới
        /// </summary>
        public void NotifyNewReservation(object reservationData)
        {
            Clients.Group("Manager").onNewReservation(reservationData);
            Clients.Group("Admin").onNewReservation(reservationData);
            Clients.Group("Cashier").onNewReservation(reservationData);
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

            Clients.Group("Manager").onReservationStatusChanged(data);
            Clients.Group("Admin").onReservationStatusChanged(data);
        }

        #endregion

        #region Delivery Notifications

        /// <summary>
        /// Thông báo đơn giao hàng mới cho shipper
        /// </summary>
        public void NotifyNewDeliveryOrder(int shipperId, object orderData)
        {
            Clients.Group($"shipper_{shipperId}").receiveNewDeliveryOrder(orderData);
        }

        /// <summary>
        /// Cập nhật trạng thái giao hàng
        /// </summary>
        public void UpdateDeliveryStatus(int orderId, string status, int? shipperId = null)
        {
            var data = new
            {
                orderId = orderId,
                status = status,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            // Thông báo cho quản lý
            Clients.Group("Manager").onDeliveryStatusChanged(data);
            Clients.Group("Admin").onDeliveryStatusChanged(data);

            // Thông báo cho shipper cụ thể
            if (shipperId.HasValue)
            {
                Clients.Group($"shipper_{shipperId}").onDeliveryStatusChanged(data);
            }
        }

        /// <summary>
        /// Cập nhật vị trí shipper
        /// </summary>
        public void UpdateShipperLocation(int shipperId, decimal latitude, decimal longitude)
        {
            var data = new
            {
                shipperId = shipperId,
                latitude = latitude,
                longitude = longitude,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.Group("Manager").onShipperLocationUpdated(data);
            Clients.Group("Delivery").onShipperLocationUpdated(data);
        }

        #endregion

        #region Dashboard Updates

        /// <summary>
        /// Cập nhật dashboard real-time
        /// </summary>
        public void BroadcastDashboardUpdate(object dashboardData)
        {
            Clients.Group("Manager").onDashboardUpdate(dashboardData);
            Clients.Group("Admin").onDashboardUpdate(dashboardData);
        }

        /// <summary>
        /// Cập nhật doanh thu real-time
        /// </summary>
        public void UpdateRevenue(decimal newTotal, decimal todayRevenue)
        {
            var data = new
            {
                newPaymentAmount = newTotal,
                todayRevenue = todayRevenue,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.Group("Manager").onRevenueUpdate(data);
            Clients.Group("Admin").onRevenueUpdate(data);
            Clients.Group("Cashier").onRevenueUpdate(data);
        }

        /// <summary>
        /// Cập nhật trạng thái bàn
        /// </summary>
        public void UpdateTableStatus(int tableId, string status, string tableNumber)
        {
            var data = new
            {
                tableId = tableId,
                status = status,
                tableNumber = tableNumber,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.All.onTableStatusChanged(data);
        }

        #endregion

        #region Chat Support

        /// <summary>
        /// Gửi tin nhắn chat
        /// </summary>
        public void SendChatMessage(string fromUserId, string toUserId, string message, string senderName)
        {
            var chatMessage = new
            {
                fromUserId = fromUserId,
                toUserId = toUserId,
                message = message,
                senderName = senderName,
                timestamp = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };

            // Gửi đến người nhận
            Clients.Group($"user_{toUserId}").receiveChatMessage(chatMessage);

            // Gửi lại cho người gửi để xác nhận
            Clients.Caller.messageSent(chatMessage);
        }

        /// <summary>
        /// Gửi tin nhắn đến support (staff)
        /// </summary>
        public void SendSupportMessage(string customerId, string customerName, string message)
        {
            var supportMessage = new
            {
                customerId = customerId,
                customerName = customerName,
                message = message,
                timestamp = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };

            // Gửi đến nhóm support
            Clients.Group("Support").receiveCustomerMessage(supportMessage);
            Clients.Group("Manager").receiveCustomerMessage(supportMessage);
        }

        /// <summary>
        /// Staff phản hồi khách hàng
        /// </summary>
        public void SendSupportReply(string customerId, string staffName, string message)
        {
            var replyMessage = new
            {
                staffName = staffName,
                message = message,
                timestamp = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            };

            Clients.Group($"user_{customerId}").receiveSupportReply(replyMessage);
        }

        /// <summary>
        /// Đánh dấu đang gõ
        /// </summary>
        public void SendTypingIndicator(string fromUserId, string toUserId, bool isTyping)
        {
            Clients.Group($"user_{toUserId}").onUserTyping(new { userId = fromUserId, isTyping = isTyping });
        }

        #endregion

        #region General Notifications

        /// <summary>
        /// Gửi thông báo chung
        /// </summary>
        public void SendNotification(string targetGroup, string title, string message, string level = "info")
        {
            var notification = new
            {
                title = title,
                message = message,
                level = level, // info, success, warning, error
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.Group(targetGroup).receiveNotification(notification);
        }

        /// <summary>
        /// Gửi thông báo đến user cụ thể
        /// </summary>
        public void SendNotificationToUser(string userId, string title, string message, string level = "info")
        {
            var notification = new
            {
                title = title,
                message = message,
                level = level,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Clients.Group($"user_{userId}").receiveNotification(notification);
        }

        /// <summary>
        /// Gửi thông báo đến tất cả
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

            Clients.All.receiveNotification(notification);
        }

        #endregion

        #region Low Stock Alerts

        /// <summary>
        /// Cảnh báo hết hàng
        /// </summary>
        public void NotifyLowStock(string ingredientName, decimal currentStock, decimal threshold)
        {
            var alert = new
            {
                ingredientName = ingredientName,
                currentStock = currentStock,
                threshold = threshold,
                message = $"Cảnh báo: {ingredientName} còn {currentStock} (ngưỡng: {threshold})",
                level = currentStock <= 0 ? "error" : "warning"
            };

            Clients.Group("Manager").onLowStockAlert(alert);
            Clients.Group("Admin").onLowStockAlert(alert);
            Clients.Group("Kitchen").onLowStockAlert(alert);
        }

        #endregion
    }
}
