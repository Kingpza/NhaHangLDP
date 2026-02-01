using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Filters;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller cho Chat Support - Hỗ trợ khách hàng real-time
    /// </summary>
    public class ChatSupportController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();
        private readonly RealTimeNotificationService _notificationService;

        public ChatSupportController()
        {
            _notificationService = new RealTimeNotificationService();
        }

        #region Staff Views

        /// <summary>
        /// Trang quản lý chat cho nhân viên
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier", "Support")]
        public ActionResult Index()
        {
            var conversations = GetActiveConversationsList();
            return View(conversations);
        }

        /// <summary>
        /// Chi tiết conversation
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier", "Support")]
        public ActionResult Conversation(string customerId)
        {
            var messages = GetConversationMessages(customerId);
            var customer = GetCustomerInfo(customerId);

            var viewModel = new ChatConversationViewModel
            {
                CustomerId = customerId,
                CustomerName = customer?.FullName ?? "Khách",
                CustomerPhone = customer?.Phone,
                Messages = messages
            };

            return View(viewModel);
        }

        #endregion

        #region Customer Views

        /// <summary>
        /// Widget chat cho khách hàng
        /// </summary>
        public ActionResult Widget()
        {
            var customerId = GetCurrentCustomerId();
            var customerName = GetCurrentCustomerName();

            ViewBag.CustomerId = customerId;
            ViewBag.CustomerName = customerName;

            return PartialView("_ChatWidget");
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Gửi tin nhắn từ khách hàng
        /// </summary>
        [HttpPost]
        public JsonResult SendCustomerMessage(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Json(new { success = false, message = "Tin nhắn không được để trống" });
                }

                var customerId = GetCurrentCustomerId();
                var customerName = GetCurrentCustomerName();

                // Lưu tin nhắn vào database
                var chatMessage = new SupportMessage
                {
                    SenderId = customerId,
                    SenderType = "Customer",
                    SenderName = customerName,
                    RecipientId = null, // Gửi đến support chung
                    RecipientType = "Support",
                    Message = message,
                    SentTime = DateTime.Now,
                    IsRead = false
                };

                _db.SupportMessage.Add(chatMessage);
                _db.SaveChanges();

                // Gửi thông báo real-time đến support
                _notificationService.SendCustomerMessageToSupport(customerId, customerName, message);

                return Json(new
                {
                    success = true,
                    messageId = chatMessage.Id,
                    timestamp = chatMessage.SentTime.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi tin nhắn từ staff
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier", "Support")]
        public JsonResult SendStaffMessage(string customerId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Json(new { success = false, message = "Tin nhắn không được để trống" });
                }

                var staffId = Session["EmployeeId"]?.ToString() ?? "0";
                var staffName = Session["UserName"]?.ToString() ?? "Nhân viên";

                // Lưu tin nhắn vào database
                var chatMessage = new SupportMessage
                {
                    SenderId = staffId,
                    SenderType = "Staff",
                    SenderName = staffName,
                    RecipientId = customerId,
                    RecipientType = "Customer",
                    Message = message,
                    SentTime = DateTime.Now,
                    IsRead = false
                };

                _db.SupportMessage.Add(chatMessage);
                _db.SaveChanges();

                // Gửi thông báo real-time đến khách hàng
                _notificationService.SendSupportReplyToCustomer(customerId, staffId, staffName, message);

                return Json(new
                {
                    success = true,
                    messageId = chatMessage.Id,
                    timestamp = chatMessage.SentTime.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy lịch sử chat
        /// </summary>
        [HttpGet]
        public JsonResult GetChatHistory(string customerId, int page = 1, int pageSize = 50)
        {
            try
            {
                var currentCustomerId = GetCurrentCustomerId();
                var isStaff = Session["EmployeeId"] != null;

                // Khách hàng chỉ được xem chat của mình
                if (!isStaff && customerId != currentCustomerId)
                {
                    customerId = currentCustomerId;
                }

                var messages = _db.SupportMessage
                    .Where(m => (m.SenderId == customerId && m.SenderType == "Customer") ||
                               (m.RecipientId == customerId && m.RecipientType == "Customer"))
                    .OrderByDescending(m => m.SentTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(m => new ChatMessageViewModel
                    {
                        Id = m.Id,
                        SenderId = m.SenderId,
                        SenderName = m.SenderName,
                        SenderType = m.SenderType,
                        Message = m.Message,
                        SentTime = m.SentTime.ToString("HH:mm dd/MM"),
                        IsRead = m.IsRead,
                        IsFromCustomer = m.SenderType == "Customer"
                    })
                    .OrderBy(m => m.Id)
                    .ToList();

                return Json(new { success = true, messages }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Đánh dấu tin nhắn đã đọc
        /// </summary>
        [HttpPost]
        public JsonResult MarkAsRead(string customerId)
        {
            try
            {
                var isStaff = Session["EmployeeId"] != null;

                if (isStaff)
                {
                    // Staff đánh dấu đã đọc tin nhắn từ customer
                    var unreadMessages = _db.SupportMessage
                        .Where(m => m.SenderId == customerId && m.SenderType == "Customer" && !m.IsRead)
                        .ToList();

                    foreach (var msg in unreadMessages)
                    {
                        msg.IsRead = true;
                        msg.ReadTime = DateTime.Now;
                    }
                }
                else
                {
                    // Customer đánh dấu đã đọc tin nhắn từ staff
                    var currentCustomerId = GetCurrentCustomerId();
                    var unreadMessages = _db.SupportMessage
                        .Where(m => m.RecipientId == currentCustomerId && m.SenderType == "Staff" && !m.IsRead)
                        .ToList();

                    foreach (var msg in unreadMessages)
                    {
                        msg.IsRead = true;
                        msg.ReadTime = DateTime.Now;
                    }
                }

                _db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy số tin nhắn chưa đọc
        /// </summary>
        [HttpGet]
        public JsonResult GetUnreadCount()
        {
            try
            {
                int unreadCount = 0;
                var isStaff = Session["EmployeeId"] != null;

                if (isStaff)
                {
                    // Đếm tất cả tin nhắn từ customer chưa đọc
                    unreadCount = _db.SupportMessage
                        .Count(m => m.SenderType == "Customer" && !m.IsRead);
                }
                else
                {
                    // Đếm tin nhắn từ staff gửi cho customer này chưa đọc
                    var currentCustomerId = GetCurrentCustomerId();
                    unreadCount = _db.SupportMessage
                        .Count(m => m.RecipientId == currentCustomerId && m.SenderType == "Staff" && !m.IsRead);
                }

                return Json(new { success = true, count = unreadCount }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, count = 0, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Lấy danh sách conversations đang active (cho staff)
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier", "Support")]
        public JsonResult GetActiveConversations()
        {
            try
            {
                var conversations = GetActiveConversationsList();
                return Json(new { success = true, conversations }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Quick Responses

        /// <summary>
        /// Lấy danh sách câu trả lời nhanh
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier", "Support")]
        public JsonResult GetQuickResponses()
        {
            var responses = new List<QuickResponse>
            {
                new QuickResponse { Id = 1, Category = "Chào hỏi", Text = "Xin chào! Nhà hàng có thể giúp gì cho bạn?" },
                new QuickResponse { Id = 2, Category = "Chào hỏi", Text = "Cảm ơn bạn đã liên hệ. Chúng tôi sẽ hỗ trợ bạn ngay!" },
                new QuickResponse { Id = 3, Category = "Đặt bàn", Text = "Bạn có thể đặt bàn qua website hoặc gọi hotline 1900.xxx" },
                new QuickResponse { Id = 4, Category = "Đặt bàn", Text = "Xin bạn cho biết thời gian và số lượng khách để chúng tôi kiểm tra bàn trống." },
                new QuickResponse { Id = 5, Category = "Giao hàng", Text = "Thời gian giao hàng dự kiến từ 30-45 phút tùy khu vực." },
                new QuickResponse { Id = 6, Category = "Giao hàng", Text = "Bạn có thể theo dõi đơn hàng trong phần 'Đơn hàng của tôi'." },
                new QuickResponse { Id = 7, Category = "Menu", Text = "Menu của chúng tôi có nhiều món đặc sắc. Bạn muốn tôi giới thiệu món nào?" },
                new QuickResponse { Id = 8, Category = "Khuyến mãi", Text = "Hiện tại chúng tôi có nhiều ưu đãi hấp dẫn. Bạn check mục Khuyến mãi nhé!" },
                new QuickResponse { Id = 9, Category = "Kết thúc", Text = "Cảm ơn bạn đã sử dụng dịch vụ. Chúc bạn ngon miệng!" },
                new QuickResponse { Id = 10, Category = "Kết thúc", Text = "Rất vui được hỗ trợ bạn. Hẹn gặp lại!" }
            };

            return Json(new { success = true, responses }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Helper Methods

        private string GetCurrentCustomerId()
        {
            // Lấy từ session nếu đã đăng nhập
            if (Session["CustomerId"] != null)
            {
                return Session["CustomerId"].ToString();
            }

            // Lấy từ cookie nếu là guest
            if (Request.Cookies["GuestId"] != null)
            {
                return Request.Cookies["GuestId"].Value;
            }

            // Tạo guest ID mới
            var guestId = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Response.Cookies.Add(new System.Web.HttpCookie("GuestId", guestId)
            {
                Expires = DateTime.Now.AddDays(30)
            });

            return guestId;
        }

        private string GetCurrentCustomerName()
        {
            if (Session["CustomerName"] != null)
            {
                return Session["CustomerName"].ToString();
            }

            return "Khách";
        }

        private List<ChatConversationSummary> GetActiveConversationsList()
        {
            // Lấy danh sách các customer có tin nhắn trong 24h gần đây
            var since = DateTime.Now.AddDays(-1);

            var conversations = _db.SupportMessage
                .Where(m => m.SenderType == "Customer" && m.SentTime >= since)
                .GroupBy(m => m.SenderId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    LastMessageTime = g.Max(m => m.SentTime),
                    UnreadCount = g.Count(m => !m.IsRead),
                    LastMessage = g.OrderByDescending(m => m.SentTime).FirstOrDefault()
                })
                .OrderByDescending(c => c.LastMessageTime)
                .ToList()
                .Select(c => new ChatConversationSummary
                {
                    CustomerId = c.CustomerId,
                    CustomerName = c.LastMessage?.SenderName ?? "Khách",
                    LastMessage = c.LastMessage?.Message?.Length > 50
                        ? c.LastMessage.Message.Substring(0, 50) + "..."
                        : c.LastMessage?.Message ?? "",
                    LastMessageTime = c.LastMessageTime.ToString("HH:mm dd/MM"),
                    UnreadCount = c.UnreadCount,
                    IsOnline = false // TODO: Implement online status
                })
                .ToList();

            return conversations;
        }

        private List<ChatMessageViewModel> GetConversationMessages(string customerId)
        {
            return _db.SupportMessage
                .Where(m => (m.SenderId == customerId && m.SenderType == "Customer") ||
                           (m.RecipientId == customerId && m.RecipientType == "Customer"))
                .OrderBy(m => m.SentTime)
                .ToList()
                .Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName,
                    SenderType = m.SenderType,
                    Message = m.Message,
                    SentTime = m.SentTime.ToString("HH:mm dd/MM"),
                    IsRead = m.IsRead,
                    IsFromCustomer = m.SenderType == "Customer"
                })
                .ToList();
        }

        private Customer GetCustomerInfo(string customerId)
        {
            if (int.TryParse(customerId, out int id))
            {
                return _db.Customer.Find(id);
            }
            return null;
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

    #region ViewModels

    public class ChatConversationViewModel
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; }
    }

    public class ChatMessageViewModel
    {
        public int Id { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderType { get; set; }
        public string Message { get; set; }
        public string SentTime { get; set; }
        public bool IsRead { get; set; }
        public bool IsFromCustomer { get; set; }
    }

    public class ChatConversationSummary
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string LastMessage { get; set; }
        public string LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
    }

    public class QuickResponse
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public string Text { get; set; }
    }

    #endregion
}
