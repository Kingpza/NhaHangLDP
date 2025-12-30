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
    /// Context for order flow in chatbot
    /// </summary>
    public class ChatbotOrderContext
    {
        public string State { get; set; } = "IDLE"; // IDLE, COLLECTING_ITEMS, CONFIRM_ITEMS, COLLECTING_INFO, CONFIRM_ORDER
        public List<ChatbotOrderItem> Items { get; set; } = new List<ChatbotOrderItem>();
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public string OrderType { get; set; } = "Delivery"; // Delivery or TakeAway
        public string Note { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Item in chatbot order
    /// </summary>
    public class ChatbotOrderItem
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }

    /// <summary>
    /// Context for reservation flow in chatbot
    /// </summary>
    public class ChatbotReservationContext
    {
        public string State { get; set; } = "IDLE"; // IDLE, COLLECTING_INFO, CONFIRM_BOOKING
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime? ReservationDate { get; set; }
        public TimeSpan? ReservationTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequests { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Action button for chatbot
    /// </summary>
    public class ChatbotActionButton
    {
        public string Text { get; set; }
        public string Action { get; set; }
        public string Data { get; set; }
        public string Style { get; set; } = "primary"; // primary, secondary, success, danger
    }

    /// <summary>
    /// Enhanced response model with actions
    /// </summary>
    public class ChatbotEnhancedResponse : ChatBotResponseModel
    {
        public List<ChatbotActionButton> ActionButtons { get; set; } = new List<ChatbotActionButton>();
        public ChatbotOrderContext OrderContext { get; set; }
        public ChatbotReservationContext ReservationContext { get; set; }
        public string HtmlContent { get; set; } // For rendering rich content like order summary
    }
}
