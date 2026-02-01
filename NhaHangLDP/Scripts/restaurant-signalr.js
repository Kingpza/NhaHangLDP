/**
 * Restaurant SignalR Client
 * Quản lý kết nối real-time cho nhà hàng
 */
var RestaurantSignalR = (function () {
    var connection = null;
    var hubProxy = null;
    var isConnected = false;
    var reconnectAttempts = 0;
    var maxReconnectAttempts = 10;

    // Callbacks cho các sự kiện
    var callbacks = {
        onConnected: null,
        onDisconnected: null,
        onReconnecting: null,
        onReconnected: null,
        onError: null
    };

    /**
     * Khởi tạo kết nối SignalR
     * @param {Object} options - Tùy chọn kết nối
     */
    function init(options) {
        options = options || {};
        
        var role = options.role || '';
        var userId = options.userId || '';

        // Tạo connection với query string
        var queryString = '';
        if (role) queryString += 'role=' + role;
        if (userId) queryString += (queryString ? '&' : '') + 'userId=' + userId;

        connection = $.hubConnection();
        if (queryString) {
            connection.qs = { role: role, userId: userId };
        }

        hubProxy = connection.createHubProxy('restaurantHub');

        // Đăng ký các handlers mặc định
        registerDefaultHandlers();

        // Bắt đầu kết nối
        connection.start()
            .done(function () {
                isConnected = true;
                reconnectAttempts = 0;
                console.log('[SignalR] Đã kết nối thành công');
                if (callbacks.onConnected) callbacks.onConnected();
            })
            .fail(function (error) {
                console.error('[SignalR] Lỗi kết nối:', error);
                if (callbacks.onError) callbacks.onError(error);
            });

        // Xử lý sự kiện ngắt kết nối
        connection.disconnected(function () {
            isConnected = false;
            console.log('[SignalR] Đã ngắt kết nối');
            if (callbacks.onDisconnected) callbacks.onDisconnected();

            // Thử kết nối lại
            if (reconnectAttempts < maxReconnectAttempts) {
                setTimeout(function () {
                    reconnectAttempts++;
                    console.log('[SignalR] Đang thử kết nối lại... (lần ' + reconnectAttempts + ')');
                    connection.start();
                }, 5000);
            }
        });

        connection.reconnecting(function () {
            console.log('[SignalR] Đang kết nối lại...');
            if (callbacks.onReconnecting) callbacks.onReconnecting();
        });

        connection.reconnected(function () {
            isConnected = true;
            reconnectAttempts = 0;
            console.log('[SignalR] Đã kết nối lại thành công');
            if (callbacks.onReconnected) callbacks.onReconnected();
        });

        return this;
    }

    /**
     * Đăng ký các handlers mặc định
     */
    function registerDefaultHandlers() {
        // Handler cho thông báo chung
        hubProxy.on('receiveNotification', function (notification) {
            showNotification(notification);
        });
    }

    /**
     * Hiển thị thông báo
     */
    function showNotification(notification) {
        var level = notification.level || 'info';
        var title = notification.title || 'Thông báo';
        var message = notification.message || '';

        // Sử dụng toastr nếu có
        if (typeof toastr !== 'undefined') {
            switch (level) {
                case 'success':
                    toastr.success(message, title);
                    break;
                case 'warning':
                    toastr.warning(message, title);
                    break;
                case 'error':
                    toastr.error(message, title);
                    break;
                default:
                    toastr.info(message, title);
            }
        } else {
            console.log('[Notification] ' + level.toUpperCase() + ': ' + title + ' - ' + message);
        }

        // Phát âm thanh nếu cần
        if (notification.level === 'warning' || notification.level === 'error') {
            playNotificationSound();
        }
    }

    /**
     * Phát âm thanh thông báo
     */
    function playNotificationSound() {
        try {
            var audio = new Audio('/Content/sounds/notification.mp3');
            audio.volume = 0.5;
            audio.play().catch(function() {});
        } catch (e) {}
    }

    /**
     * Đăng ký callback cho sự kiện
     */
    function on(eventName, callback) {
        if (hubProxy) {
            hubProxy.on(eventName, callback);
        }
        return this;
    }

    /**
     * Gọi phương thức trên server
     */
    function invoke(methodName) {
        if (!hubProxy || !isConnected) {
            console.warn('[SignalR] Chưa kết nối');
            return $.Deferred().reject('Not connected').promise();
        }
        var args = Array.prototype.slice.call(arguments);
        return hubProxy.invoke.apply(hubProxy, args);
    }

    /**
     * Tham gia vào group
     */
    function joinGroup(groupName) {
        return invoke('JoinGroup', groupName);
    }

    /**
     * Rời khỏi group
     */
    function leaveGroup(groupName) {
        return invoke('LeaveGroup', groupName);
    }

    /**
     * Đặt callback
     */
    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    /**
     * Kiểm tra trạng thái kết nối
     */
    function getIsConnected() {
        return isConnected;
    }

    /**
     * Ngắt kết nối
     */
    function stop() {
        if (connection) {
            connection.stop();
        }
    }

    // Public API
    return {
        init: init,
        on: on,
        invoke: invoke,
        joinGroup: joinGroup,
        leaveGroup: leaveGroup,
        setCallback: setCallback,
        isConnected: getIsConnected,
        stop: stop,
        showNotification: showNotification
    };
})();

/**
 * Kitchen Display SignalR Client
 * Module cho màn hình bếp
 */
var KitchenSignalR = (function () {
    var callbacks = {
        onNewTicket: null,
        onTicketStatusChanged: null,
        onItemCompleted: null,
        onTicketReady: null,
        onRefreshRequired: null
    };

    function init(stationCode) {
        // Đăng ký handlers cho bếp
        RestaurantSignalR
            .on('receiveNewTicket', function (data) {
                console.log('[Kitchen] Ticket mới:', data);
                playKitchenAlert();
                if (callbacks.onNewTicket) callbacks.onNewTicket(data);
            })
            .on('receiveNewOrder', function (data) {
                console.log('[Kitchen] Đơn hàng mới:', data);
                playKitchenAlert();
                if (callbacks.onNewTicket) callbacks.onNewTicket(data);
            })
            .on('onTicketStatusChanged', function (data) {
                console.log('[Kitchen] Trạng thái ticket thay đổi:', data);
                if (callbacks.onTicketStatusChanged) callbacks.onTicketStatusChanged(data);
            })
            .on('onItemCompleted', function (data) {
                console.log('[Kitchen] Món hoàn thành:', data);
                if (callbacks.onItemCompleted) callbacks.onItemCompleted(data);
            })
            .on('onLowStockAlert', function (data) {
                console.log('[Kitchen] Cảnh báo tồn kho:', data);
                RestaurantSignalR.showNotification({
                    title: 'Cảnh báo tồn kho',
                    message: data.message,
                    level: data.level
                });
            });

        // Tham gia station nếu có
        if (stationCode) {
            RestaurantSignalR.invoke('JoinKitchenStation', stationCode);
        }

        return this;
    }

    function playKitchenAlert() {
        try {
            var audio = new Audio('/Content/sounds/kitchen-alert.mp3');
            audio.volume = 0.7;
            audio.play().catch(function() {});
        } catch (e) {}
    }

    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    function startTicket(ticketId) {
        return RestaurantSignalR.invoke('StartTicket', ticketId);
    }

    function completeItem(ticketId, itemId, quantity) {
        return RestaurantSignalR.invoke('CompleteItem', { ticketId: ticketId, itemId: itemId, completedQuantity: quantity });
    }

    function completeTicket(ticketId) {
        return RestaurantSignalR.invoke('CompleteTicket', ticketId);
    }

    return {
        init: init,
        setCallback: setCallback,
        startTicket: startTicket,
        completeItem: completeItem,
        completeTicket: completeTicket
    };
})();

/**
 * Cashier SignalR Client
 * Module cho thu ngân
 */
var CashierSignalR = (function () {
    var callbacks = {
        onOrderReady: null,
        onOrderStatusChanged: null,
        onRevenueUpdate: null,
        onNewReservation: null,
        onTableStatusChanged: null
    };

    function init() {
        RestaurantSignalR
            .on('onOrderReady', function (data) {
                console.log('[Cashier] Đơn sẵn sàng:', data);
                playOrderReadySound();
                RestaurantSignalR.showNotification({
                    title: 'Đơn hàng sẵn sàng',
                    message: data.message,
                    level: 'success'
                });
                if (callbacks.onOrderReady) callbacks.onOrderReady(data);
            })
            .on('onOrderStatusChanged', function (data) {
                console.log('[Cashier] Trạng thái đơn thay đổi:', data);
                if (callbacks.onOrderStatusChanged) callbacks.onOrderStatusChanged(data);
            })
            .on('onRevenueUpdate', function (data) {
                console.log('[Cashier] Doanh thu cập nhật:', data);
                if (callbacks.onRevenueUpdate) callbacks.onRevenueUpdate(data);
            })
            .on('onNewReservation', function (data) {
                console.log('[Cashier] Đặt bàn mới:', data);
                RestaurantSignalR.showNotification({
                    title: 'Đặt bàn mới',
                    message: data.message,
                    level: 'info'
                });
                if (callbacks.onNewReservation) callbacks.onNewReservation(data);
            })
            .on('onTableStatusChanged', function (data) {
                console.log('[Cashier] Trạng thái bàn thay đổi:', data);
                if (callbacks.onTableStatusChanged) callbacks.onTableStatusChanged(data);
            })
            .on('onTicketStatusChanged', function (data) {
                if (data.status === 'Ready') {
                    playOrderReadySound();
                }
            });

        return this;
    }

    function playOrderReadySound() {
        try {
            var audio = new Audio('/Content/sounds/order-ready.mp3');
            audio.volume = 0.6;
            audio.play().catch(function() {});
        } catch (e) {}
    }

    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    return {
        init: init,
        setCallback: setCallback
    };
})();

/**
 * Shipper SignalR Client
 * Module cho shipper
 */
var ShipperSignalR = (function () {
    var callbacks = {
        onNewDeliveryOrder: null,
        onDeliveryStatusChanged: null
    };

    function init(shipperId) {
        RestaurantSignalR
            .on('receiveNewDeliveryOrder', function (data) {
                console.log('[Shipper] Đơn hàng mới:', data);
                playDeliveryAlert();
                RestaurantSignalR.showNotification({
                    title: 'Đơn hàng mới!',
                    message: data.message || 'Bạn có đơn hàng mới',
                    level: 'info'
                });
                if (callbacks.onNewDeliveryOrder) callbacks.onNewDeliveryOrder(data);
            })
            .on('onDeliveryStatusChanged', function (data) {
                console.log('[Shipper] Trạng thái giao hàng:', data);
                if (callbacks.onDeliveryStatusChanged) callbacks.onDeliveryStatusChanged(data);
            });

        // Tham gia group của shipper
        if (shipperId) {
            RestaurantSignalR.invoke('JoinShipperGroup', shipperId);
        }

        return this;
    }

    function playDeliveryAlert() {
        try {
            var audio = new Audio('/Content/sounds/delivery-alert.mp3');
            audio.volume = 0.8;
            audio.play().catch(function() {});
        } catch (e) {}
    }

    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    function updateLocation(latitude, longitude) {
        return RestaurantSignalR.invoke('UpdateShipperLocation', latitude, longitude);
    }

    return {
        init: init,
        setCallback: setCallback,
        updateLocation: updateLocation
    };
})();

/**
 * Dashboard SignalR Client
 * Module cho quản lý dashboard
 */
var DashboardSignalR = (function () {
    var callbacks = {
        onDashboardUpdate: null,
        onRevenueUpdate: null,
        onOrderStatusChanged: null,
        onDeliveryStatusChanged: null,
        onTableStatusChanged: null,
        onLowStockAlert: null,
        onExpiringItemAlert: null,
        onNewReservation: null,
        onShipperLocationUpdated: null
    };

    function init() {
        RestaurantSignalR
            .on('onDashboardUpdate', function (data) {
                console.log('[Dashboard] Cập nhật:', data);
                if (callbacks.onDashboardUpdate) callbacks.onDashboardUpdate(data);
            })
            .on('onRevenueUpdate', function (data) {
                console.log('[Dashboard] Doanh thu:', data);
                RestaurantSignalR.showNotification({
                    title: 'Thanh toán mới',
                    message: data.message,
                    level: 'success'
                });
                if (callbacks.onRevenueUpdate) callbacks.onRevenueUpdate(data);
            })
            .on('onOrderStatusChanged', function (data) {
                if (callbacks.onOrderStatusChanged) callbacks.onOrderStatusChanged(data);
            })
            .on('onDeliveryStatusChanged', function (data) {
                if (callbacks.onDeliveryStatusChanged) callbacks.onDeliveryStatusChanged(data);
            })
            .on('onTableStatusChanged', function (data) {
                if (callbacks.onTableStatusChanged) callbacks.onTableStatusChanged(data);
            })
            .on('onLowStockAlert', function (data) {
                console.log('[Dashboard] Cảnh báo tồn kho:', data);
                RestaurantSignalR.showNotification({
                    title: 'Cảnh báo tồn kho',
                    message: data.message,
                    level: data.level
                });
                if (callbacks.onLowStockAlert) callbacks.onLowStockAlert(data);
            })
            .on('onExpiringItemAlert', function (data) {
                console.log('[Dashboard] Cảnh báo hết hạn:', data);
                RestaurantSignalR.showNotification({
                    title: 'Cảnh báo hết hạn',
                    message: data.message,
                    level: data.level
                });
                if (callbacks.onExpiringItemAlert) callbacks.onExpiringItemAlert(data);
            })
            .on('onNewReservation', function (data) {
                console.log('[Dashboard] Đặt bàn mới:', data);
                RestaurantSignalR.showNotification({
                    title: 'Đặt bàn mới',
                    message: data.message,
                    level: 'info'
                });
                if (callbacks.onNewReservation) callbacks.onNewReservation(data);
            })
            .on('onShipperLocationUpdated', function (data) {
                if (callbacks.onShipperLocationUpdated) callbacks.onShipperLocationUpdated(data);
            });

        return this;
    }

    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    return {
        init: init,
        setCallback: setCallback
    };
})();

/**
 * Chat Support SignalR Client
 * Module cho chat hỗ trợ khách hàng
 */
var ChatSignalR = (function () {
    var callbacks = {
        onReceiveMessage: null,
        onReceiveCustomerMessage: null,
        onReceiveSupportReply: null,
        onUserTyping: null,
        onUnreadCountChanged: null
    };

    function init() {
        RestaurantSignalR
            .on('receiveChatMessage', function (data) {
                console.log('[Chat] Tin nhắn:', data);
                if (callbacks.onReceiveMessage) callbacks.onReceiveMessage(data);
            })
            .on('receiveCustomerMessage', function (data) {
                console.log('[Chat] Tin nhắn từ khách:', data);
                playMessageSound();
                RestaurantSignalR.showNotification({
                    title: 'Tin nhắn mới từ ' + data.customerName,
                    message: data.message.substring(0, 50) + '...',
                    level: 'info'
                });
                if (callbacks.onReceiveCustomerMessage) callbacks.onReceiveCustomerMessage(data);
            })
            .on('receiveSupportReply', function (data) {
                console.log('[Chat] Phản hồi từ support:', data);
                playMessageSound();
                if (callbacks.onReceiveSupportReply) callbacks.onReceiveSupportReply(data);
            })
            .on('onUserTyping', function (data) {
                if (callbacks.onUserTyping) callbacks.onUserTyping(data);
            })
            .on('onUnreadMessagesCountChanged', function (data) {
                if (callbacks.onUnreadCountChanged) callbacks.onUnreadCountChanged(data);
            })
            .on('messageSent', function (data) {
                console.log('[Chat] Tin nhắn đã gửi:', data);
            });

        return this;
    }

    function playMessageSound() {
        try {
            var audio = new Audio('/Content/sounds/message.mp3');
            audio.volume = 0.4;
            audio.play().catch(function() {});
        } catch (e) {}
    }

    function setCallback(name, callback) {
        if (callbacks.hasOwnProperty(name)) {
            callbacks[name] = callback;
        }
        return this;
    }

    function sendMessage(toUserId, message) {
        return RestaurantSignalR.invoke('SendChatMessage', toUserId, message);
    }

    function sendSupportMessage(message) {
        var customerId = window.currentUserId || '';
        var customerName = window.currentUserName || 'Khách';
        return RestaurantSignalR.invoke('SendSupportMessage', customerId, customerName, message);
    }

    function sendSupportReply(customerId, message) {
        var staffName = window.currentUserName || 'Nhân viên';
        return RestaurantSignalR.invoke('SendSupportReply', customerId, staffName, message);
    }

    function sendTypingIndicator(toUserId, isTyping) {
        var fromUserId = window.currentUserId || '';
        return RestaurantSignalR.invoke('SendTypingIndicator', fromUserId, toUserId, isTyping);
    }

    return {
        init: init,
        setCallback: setCallback,
        sendMessage: sendMessage,
        sendSupportMessage: sendSupportMessage,
        sendSupportReply: sendSupportReply,
        sendTypingIndicator: sendTypingIndicator
    };
})();
