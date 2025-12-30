# Real-time Communication với SignalR & Chat Support

## Tổng quan

Hệ thống đã được tích hợp Microsoft ASP.NET SignalR để cung cấp các tính năng real-time communication cho nhà hàng, bao gồm **AI Chatbot** và **Live Chat Support**.

## Các tính năng đã triển khai

### 1. Cập nhật trạng thái đơn hàng real-time cho bếp/shipper
- Khi có đơn hàng mới, bếp nhận được thông báo ngay lập tức
- Cập nhật trạng thái ticket (Pending → Preparing → Ready → Completed)
- Thông báo khi món ăn đã hoàn thành

### 2. Thông báo đặt bàn mới cho quản lý
- Khi khách hàng đặt bàn, quản lý/admin nhận thông báo real-time
- Cập nhật trạng thái đặt bàn

### 3. Cập nhật dashboard real-time
- Doanh thu cập nhật ngay khi có thanh toán mới
- Số đơn hàng, trạng thái bàn cập nhật tức thì
- Cảnh báo tồn kho thấp, nguyên liệu sắp hết hạn

### 4. Chat Support tích hợp AI Chatbot + Live Support
- **AI Chatbot**: Tư vấn món ăn, gợi ý theo ngân sách, hỗ trợ đặt bàn tự động
- **Live Chat**: Khi cần, khách hàng có thể chat trực tiếp với nhân viên
- **Chuyển đổi linh hoạt**: Khách hàng có thể chuyển giữa Bot AI và Nhân viên
- **Real-time messaging**: Tin nhắn được gửi/nhận ngay lập tức qua SignalR

## Cấu trúc files

```
NhaHangLDP/
├── Startup.cs                              # OWIN Startup cấu hình SignalR
├── Hubs/
│   └── RestaurantHub.cs                    # SignalR Hub chính
├── Services/
│   ├── RealTimeNotificationService.cs      # Service gửi thông báo
│   └── AIChatbotService.cs                 # AI Chatbot service
├── Controllers/
│   └── ChatSupportController.cs            # Controller cho chat support
├── Models/
│   ├── ChatModels.cs                       # Models cho chat (SupportMessage, SupportConversation)
│   └── NhaHangLDP.Context.Extended.cs      # Mở rộng DbContext
├── Scripts/
│   ├── chatbot.js                          # JavaScript client (Bot AI + Live Chat)
│   └── SQL/
│       └── CreateChatTables.sql            # Script tạo bảng database
├── Content/
│   └── chatbot.css                         # Styles cho chatbot widget
├── Views/
│   └── ChatSupport/
│       └── Index.cshtml                    # Trang quản lý chat cho nhân viên
└── App_Start/
    └── BundleConfig.cs                     # Đã thêm SignalR bundle
```

## Hướng dẫn sử dụng Chat Support

### Bước 1: Chạy SQL Script
```sql
-- Chạy file: NhaHangLDP\Scripts\SQL\CreateChatTables.sql
-- Tạo 2 bảng: SupportMessage và SupportConversation
```

### Bước 2: Sử dụng Chat Widget (Khách hàng)

Chat widget tự động hiển thị ở góc phải màn hình trên trang Home và Menu.

**Tính năng:**
- 🤖 **Bot AI**: Trả lời tự động về menu, giá cả, đặt bàn
- 👨‍💼 **Nhân viên**: Chat trực tiếp với support team
- 🎤 **Voice Input**: Nhập bằng giọng nói
- 📱 **Responsive**: Hoạt động tốt trên mobile

**Cách sử dụng:**
1. Click vào icon chat ở góc phải
2. Mặc định là chế độ **Bot AI** - bot tự động trả lời
3. Click nút **"Nhân viên"** để chuyển sang chat với nhân viên thật
4. Sử dụng các **Quick Replies** để hỏi nhanh

### Bước 3: Quản lý Chat (Nhân viên)

Truy cập: `/ChatSupport/Index`

**Tính năng:**
- Xem danh sách conversations đang active
- Trả lời tin nhắn real-time
- Quick Responses (câu trả lời nhanh)
- Đánh dấu tin nhắn đã đọc
- Thông báo khi có tin nhắn mới

### Bước 4: Include SignalR trong View (nếu cần custom)

```html
<!-- Thêm vào layout hoặc view cần sử dụng SignalR -->
@Scripts.Render("~/bundles/jquery")
<script src="~/Scripts/jquery.signalR-2.4.3.min.js"></script>
<script src="~/signalr/hubs"></script>
<script src="~/Scripts/chatbot.js"></script>
<link href="~/Content/chatbot.css" rel="stylesheet" />
```

## API Reference

### Chat Support Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ChatSupport/SendCustomerMessage` | POST | Gửi tin nhắn từ khách hàng |
| `/ChatSupport/SendStaffMessage` | POST | Nhân viên gửi tin nhắn |
| `/ChatSupport/GetChatHistory` | GET | Lấy lịch sử chat |
| `/ChatSupport/GetUnreadCount` | GET | Đếm tin nhắn chưa đọc |
| `/ChatSupport/MarkAsRead` | POST | Đánh dấu đã đọc |
| `/ChatSupport/GetQuickResponses` | GET | Lấy câu trả lời nhanh |
| `/ChatSupport/GetActiveConversations` | GET | Lấy danh sách conversations |

### AI Chatbot Endpoint

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/Public/ChatBotAI` | POST | Gửi tin nhắn đến AI Chatbot |

## SignalR Events

### Chat Events
| Event | Direction | Description |
|-------|-----------|-------------|
| `receiveCustomerMessage` | Server → Staff | Tin nhắn từ khách hàng |
| `receiveSupportReply` | Server → Customer | Phản hồi từ support |
| `onUserTyping` | Bi-directional | Người dùng đang gõ |
| `onUnreadMessagesCountChanged` | Server → Client | Số tin nhắn chưa đọc |

### Khởi tạo cho Staff:
```javascript
$.connection.hub.qs = { 
    role: 'Support',
    userId: '@Session["EmployeeId"]'
};

$.connection.hub.start().done(function() {
    // Join Support group
    $.connection.restaurantHub.server.joinGroup('Support');
});
```

### Khởi tạo cho Customer:
```javascript
$.connection.hub.qs = { 
    role: 'Customer',
    userId: customerId
};

$.connection.hub.start().done(function() {
    $.connection.restaurantHub.server.joinGroup('user_' + customerId);
});
```

## JavaScript API

Chatbot widget expose public API:

```javascript
// Mở chatbot
LDPChatbot.open();

// Đóng chatbot
LDPChatbot.close();

// Toggle
LDPChatbot.toggle();

// Chuyển sang Live Chat
LDPChatbot.switchToLive();

// Chuyển về Bot AI
LDPChatbot.switchToBot();

// Lấy state
LDPChatbot.getState();
```

## Troubleshooting

### Không kết nối được SignalR
1. Kiểm tra đã include đúng thứ tự: jQuery → SignalR → /signalr/hubs
2. Kiểm tra Console để xem lỗi
3. Đảm bảo Startup.cs đã được cấu hình đúng

### Không nhận được tin nhắn
1. Kiểm tra đã join đúng group
2. Kiểm tra role trong query string khi init
3. Sử dụng Chrome DevTools để debug SignalR connections

### Chatbot không hoạt động
1. Kiểm tra file `chatbot.js` và `chatbot.css` đã được include
2. Kiểm tra endpoint `/Public/ChatBotAI` hoạt động
3. Xem Console log để debug

### Performance
- SignalR tự động chọn transport tốt nhất (WebSocket > Server-Sent Events > Long Polling)
- Trên IIS cần enable WebSocket
- Có thể cấu hình timeout trong Startup.cs nếu cần
