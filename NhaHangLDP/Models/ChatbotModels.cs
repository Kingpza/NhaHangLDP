using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// Model cho tin nhắn chat
    /// </summary>
    public class ChatMessage
    {
        public int Id { get; set; }
        public string SessionId { get; set; }
        public string Role { get; set; } // "user" hoặc "assistant"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public string Intent { get; set; } // Phân loại intent của tin nhắn
    }

    /// <summary>
    /// Model cho phiên chat
    /// </summary>
    public class ChatSession
    {
        public string SessionId { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public Dictionary<string, object> Context { get; set; }

        public ChatSession()
        {
            SessionId = Guid.NewGuid().ToString();
            Messages = new List<ChatMessage>();
            StartTime = DateTime.Now;
            LastActivity = DateTime.Now;
            Context = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Request từ client gửi lên
    /// </summary>
    public class ChatBotRequest
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
        public string History { get; set; }
    }

    /// <summary>
    /// Response trả về cho client - Enhanced version
    /// </summary>
    public class ChatBotResponseModel
    {
        public bool Success { get; set; }
        public string Response { get; set; }
        public List<MenuSuggestionModel> Suggestions { get; set; }
        public List<string> QuickReplies { get; set; }
        public string Intent { get; set; }
        public double Confidence { get; set; }
        public string SessionId { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public ChatBotResponseModel()
        {
            Success = true;
            Suggestions = new List<MenuSuggestionModel>();
            QuickReplies = new List<string>();
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Gợi ý món ăn từ chatbot
    /// </summary>
    public class MenuSuggestionModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public double? Rating { get; set; }
        public int SoldCount { get; set; }
        public bool IsNew { get; set; }
        public bool IsFeatured { get; set; }
    }

    /// <summary>
    /// Entity được trích xuất từ tin nhắn
    /// </summary>
    public class ExtractedEntities
    {
        public decimal? MaxPrice { get; set; }
        public decimal? MinPrice { get; set; }
        public string Category { get; set; }
        public int? PartySize { get; set; }
        public DateTime? BookingDate { get; set; }
        public string DishName { get; set; }
        public List<string> Ingredients { get; set; }
        public string Preference { get; set; } // spicy, vegetarian, etc.

        public ExtractedEntities()
        {
            Ingredients = new List<string>();
        }
    }

    /// <summary>
    /// Intent và độ tin cậy
    /// </summary>
    public class IntentResult
    {
        public string Intent { get; set; }
        public double Confidence { get; set; }
        public ExtractedEntities Entities { get; set; }

        public IntentResult()
        {
            Entities = new ExtractedEntities();
        }
    }

    /// <summary>
    /// Combo suggestion
    /// </summary>
    public class ComboSuggestion
    {
        public string ComboName { get; set; }
        public List<MenuSuggestionModel> Items { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal SaveAmount { get; set; }
        public int ForPeople { get; set; }

        public ComboSuggestion()
        {
            Items = new List<MenuSuggestionModel>();
        }
    }

    /// <summary>
    /// Thông tin đặt bàn từ chatbot
    /// </summary>
    public class BookingRequest
    {
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public int NumberOfGuests { get; set; }
        public DateTime BookingDateTime { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// FAQ item
    /// </summary>
    public class FAQItem
    {
        public int Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public string Category { get; set; }
        public int Priority { get; set; }
        public List<string> Keywords { get; set; }

        public FAQItem()
        {
            Keywords = new List<string>();
        }
    }

    /// <summary>
    /// Chatbot Analytics
    /// </summary>
    public class ChatbotAnalytics
    {
        public int TotalConversations { get; set; }
        public int TotalMessages { get; set; }
        public Dictionary<string, int> IntentDistribution { get; set; }
        public List<TopQuestion> TopQuestions { get; set; }
        public double AverageMessagesPerSession { get; set; }
        public double ResponseSuccessRate { get; set; }
        public DateTime ReportDate { get; set; }

        public ChatbotAnalytics()
        {
            IntentDistribution = new Dictionary<string, int>();
            TopQuestions = new List<TopQuestion>();
        }
    }

    public class TopQuestion
    {
        public string Question { get; set; }
        public int Count { get; set; }
        public string Intent { get; set; }
    }

    /// <summary>
    /// Một item trong giỏ hàng chatbot
    /// </summary>
    public class ChatOrderItem
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; }
        public string ImageUrl { get; set; }
    }

    /// <summary>
    /// Giỏ hàng chatbot (lưu trong session context)
    /// </summary>
    public class ChatOrderCart
    {
        public List<ChatOrderItem> Items { get; set; }
        public string OrderType { get; set; } // "DineIn", "TakeAway", "Delivery"
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public string DeliveryAddress { get; set; }
        public string Note { get; set; }

        public ChatOrderCart()
        {
            Items = new List<ChatOrderItem>();
            OrderType = "DineIn";
        }

        public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);
        public int TotalItems => Items.Sum(i => i.Quantity);
    }

    /// <summary>
    /// Thông tin đặt bàn qua chatbot
    /// </summary>
    public class ChatBookingInfo
    {
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int NumberOfGuests { get; set; }
        public DateTime? BookingDateTime { get; set; }
        public string SpecialRequests { get; set; }
        public string Step { get; set; } // "init", "guests", "time", "name", "phone", "confirm"

        public ChatBookingInfo()
        {
            Step = "init";
        }

        public bool IsComplete =>
            !string.IsNullOrEmpty(CustomerName) &&
            !string.IsNullOrEmpty(CustomerPhone) &&
            NumberOfGuests > 0 &&
            BookingDateTime.HasValue;
    }

    /// <summary>
    /// Response metadata cho actions (đặt hàng, đặt bàn)
    /// </summary>
    public class ChatActionResult
    {
        public string Action { get; set; } // "order_created", "booking_created", "item_added", "cart_updated"
        public string OrderCode { get; set; }
        public string ReservationCode { get; set; }
        public bool Success { get; set; }
    }
}
