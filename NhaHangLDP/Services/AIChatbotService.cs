using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý AI Chatbot nâng cao
    /// Hỗ trợ: Gemini API (miễn phí), Rule-based fallback
    /// </summary>
    public class AIChatbotService
    {
        private readonly NhaHangLDPEntities _db;
        private readonly string _geminiApiKey;
        private readonly bool _useGeminiAI;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly RedisCacheService _cache = RedisCacheService.Instance;
        
        // Cache sessions trong memory (production nên dùng Redis)
        private static Dictionary<string, ChatSession> _sessions = new Dictionary<string, ChatSession>();
        
        // Intent patterns - Enhanced version
        private static readonly Dictionary<string, List<string>> IntentPatterns = new Dictionary<string, List<string>>
        {
            { "GREETING", new List<string> { "hi", "hello", "chào", "xin chào", "hey", "alo", "chao", "xin hỏi", "cho hỏi" } },
            { "FIND_DISH", new List<string> { "tìm", "có", "món", "gì", "menu", "danh sách", "xem", "cho xem" } },
            { "RECOMMEND", new List<string> { "gợi ý", "đề xuất", "nên ăn", "ngon", "recommend", "best", "thích hợp" } },
            { "BEST_SELLERS", new List<string> { "bán chạy", "top", "phổ biến", "nổi tiếng", "hot", "best seller", "nhiều người" } },
            { "BUDGET", new List<string> { "giá", "bao nhiêu", "dưới", "rẻ", "tiết kiệm", "ngân sách", "tầm", "khoảng" } },
            { "CATEGORY", new List<string> { "khai vị", "món chính", "tráng miệng", "đồ uống", "lẩu", "nướng", "hải sản", "thịt", "gà", "bò", "heo" } },
            { "NEW_DISHES", new List<string> { "mới", "new", "ra mắt", "vừa có", "mới nhất" } },
            { "FEATURED", new List<string> { "đặc biệt", "featured", "nổi bật", "signature", "đặc sản", "chef" } },
            { "BOOK_TABLE", new List<string> { "đặt bàn", "book", "reservation", "giữ chỗ", "đặt chỗ", "đặt trước", "đặt lịch" } },
            { "COMBO", new List<string> { "combo", "set", "người", "nhóm", "tiệc", "party", "sinh nhật" } },
            { "INGREDIENTS", new List<string> { "nguyên liệu", "thành phần", "gồm", "có gì", "làm từ" } },
            { "HOURS", new List<string> { "giờ", "mở cửa", "đóng cửa", "thời gian", "hoạt động", "mấy giờ" } },
            { "LOCATION", new List<string> { "địa chỉ", "ở đâu", "location", "chỗ nào", "đường nào" } },
            { "PROMOTION", new List<string> { "khuyến mãi", "giảm giá", "ưu đãi", "voucher", "sale", "discount", "mã giảm" } },
            { "THANKS", new List<string> { "cảm ơn", "thanks", "thank you", "cám ơn", "tks", "ok" } },
            { "GOODBYE", new List<string> { "tạm biệt", "bye", "goodbye", "hẹn gặp", "thôi nhé" } },
            { "SPICY", new List<string> { "cay", "매운", "spicy", "ớt", "chua cay" } },
            { "VEGETARIAN", new List<string> { "chay", "rau", "vegetarian", "vegan", "không thịt" } },
            { "HEALTHY", new List<string> { "healthy", "lành mạnh", "diet", "ít calo", "giảm cân", "salad" } },
            { "ALLERGIES", new List<string> { "dị ứng", "allergy", "không ăn được", "kiêng" } },
            { "ORDER_STATUS", new List<string> { "đơn hàng", "order", "trạng thái", "bao lâu", "khi nào" } },
            { "ADD_TO_CART", new List<string> { "thêm", "đặt món", "order món", "gọi món", "cho tôi", "lấy", "mua", "muốn ăn", "muốn gọi", "thêm vào" } },
            { "VIEW_CART", new List<string> { "giỏ hàng", "cart", "xem giỏ", "đã đặt gì", "đã chọn", "đơn hiện tại" } },
            { "CONFIRM_ORDER", new List<string> { "xác nhận đơn", "đặt hàng", "thanh toán", "hoàn tất đơn", "đặt ngay", "order ngay", "xác nhận" } },
            { "CONFIRM_BOOKING", new List<string> { "xác nhận đặt bàn", "đặt bàn ngay", "ok đặt", "đồng ý đặt", "xác nhận bàn" } }
        };

        // FAQ Database
        private static readonly List<FAQItem> FAQDatabase = new List<FAQItem>
        {
            new FAQItem 
            { 
                Question = "Nhà hàng mở cửa lúc mấy giờ?",
                Answer = "🕐 **Giờ mở cửa:**\n• Thứ 2 - Thứ 6: 10:00 - 22:00\n• Thứ 7 - Chủ nhật: 09:00 - 23:00\n\nChúng tôi phục vụ liên tục, không nghỉ trưa!",
                Keywords = new List<string> { "giờ", "mở cửa", "đóng cửa", "thời gian" }
            },
            new FAQItem 
            { 
                Question = "Địa chỉ nhà hàng ở đâu?",
                Answer = "📍 **Địa chỉ:**\nNhà hàng LDP\n123 Đường ABC, Quận XYZ\nTP. Hồ Chí Minh\n\n📞 Hotline: 0123 456 789",
                Keywords = new List<string> { "địa chỉ", "ở đâu", "location", "chỗ" }
            },
            new FAQItem 
            { 
                Question = "Có chỗ đậu xe không?",
                Answer = "🚗 **Bãi đậu xe:**\nCó bãi đậu xe miễn phí cho khách hàng!\n• Xe máy: Miễn phí\n• Ô tô: Miễn phí 2 giờ đầu",
                Keywords = new List<string> { "đậu xe", "parking", "gửi xe" }
            }
        };

        public AIChatbotService()
        {
            _db = new NhaHangLDPEntities();
            _geminiApiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            _useGeminiAI = !string.IsNullOrEmpty(_geminiApiKey);
        }

        public AIChatbotService(NhaHangLDPEntities db)
        {
            _db = db;
            _geminiApiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            _useGeminiAI = !string.IsNullOrEmpty(_geminiApiKey);
        }

        /// <summary>
        /// Xử lý tin nhắn chính
        /// </summary>
        public async Task<ChatBotResponseModel> ProcessMessageAsync(string message, string sessionId = null)
        {
            var response = new ChatBotResponseModel();

            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    response.Success = false;
                    response.Response = "Vui lòng nhập tin nhắn!";
                    return response;
                }

                // Lấy hoặc tạo session
                var session = GetOrCreateSession(sessionId);
                response.SessionId = session.SessionId;

                // Thêm tin nhắn user vào session
                session.Messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = message,
                    Timestamp = DateTime.Now
                });
                session.LastActivity = DateTime.Now;

                // Phân tích intent
                var intentResult = AnalyzeIntent(message.ToLower());
                response.Intent = intentResult.Intent;
                response.Confidence = intentResult.Confidence;

                // Xử lý theo intent
                if (_useGeminiAI && intentResult.Confidence < 0.7)
                {
                    // Dùng Gemini AI khi không chắc chắn intent
                    response = await ProcessWithGeminiAsync(message, session);
                }
                else
                {
                    // Xử lý rule-based
                    response = ProcessWithRules(message, intentResult, session);
                }

                // Thêm response vào session
                session.Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response.Response,
                    Timestamp = DateTime.Now,
                    Intent = intentResult.Intent
                });

                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Response = "Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại!";
                System.Diagnostics.Debug.WriteLine($"ChatBot Error: {ex.Message}");
            }

            return response;
        }

        /// <summary>
        /// Phân tích intent từ tin nhắn
        /// </summary>
        private IntentResult AnalyzeIntent(string message)
        {
            var result = new IntentResult();
            var scores = new Dictionary<string, double>();

            foreach (var pattern in IntentPatterns)
            {
                double score = 0;
                foreach (var keyword in pattern.Value)
                {
                    if (message.Contains(keyword))
                    {
                        score += 1.0 / pattern.Value.Count;
                    }
                }
                scores[pattern.Key] = score;
            }

            // Tìm intent có score cao nhất
            var topIntent = scores.OrderByDescending(s => s.Value).First();
            result.Intent = topIntent.Key;
            result.Confidence = Math.Min(topIntent.Value * 1.5, 1.0);

            // Extract entities
            result.Entities = ExtractEntities(message);

            return result;
        }

        /// <summary>
        /// Trích xuất entities từ tin nhắn
        /// </summary>
        private ExtractedEntities ExtractEntities(string message)
        {
            var entities = new ExtractedEntities();

            // Extract price
            var priceMatch = Regex.Match(message, @"(\d+)\s*(k|nghìn|ngàn|triệu)?");
            if (priceMatch.Success)
            {
                decimal price = decimal.Parse(priceMatch.Groups[1].Value);
                string unit = priceMatch.Groups[2].Value.ToLower();
                
                if (unit == "k" || unit == "nghìn" || unit == "ngàn")
                    price *= 1000;
                else if (unit == "triệu")
                    price *= 1000000;
                else if (price < 1000)
                    price *= 1000; // Assume "100" means "100k"

                if (message.Contains("dưới") || message.Contains("under") || message.Contains("<"))
                    entities.MaxPrice = price;
                else if (message.Contains("trên") || message.Contains("over") || message.Contains(">"))
                    entities.MinPrice = price;
                else
                    entities.MaxPrice = price;
            }

            // Extract category
            var categories = new Dictionary<string, string>
            {
                { "khai vị", "Món Khai Vị" },
                { "món chính", "Món Chính" },
                { "tráng miệng", "Tráng Miệng" },
                { "đồ uống", "Đồ Uống" },
                { "lẩu", "Lẩu" },
                { "nướng", "Nướng" }
            };

            foreach (var cat in categories)
            {
                if (message.Contains(cat.Key))
                {
                    entities.Category = cat.Value;
                    break;
                }
            }

            // Extract party size
            var partyMatch = Regex.Match(message, @"(\d+)\s*(người|ng|khách)");
            if (partyMatch.Success)
            {
                entities.PartySize = int.Parse(partyMatch.Groups[1].Value);
            }

            // Extract preferences
            if (message.Contains("cay")) entities.Preference = "spicy";
            else if (message.Contains("chay") || message.Contains("vegetarian")) entities.Preference = "vegetarian";
            else if (message.Contains("healthy") || message.Contains("lành mạnh")) entities.Preference = "healthy";

            return entities;
        }

        /// <summary>
        /// Xử lý với Gemini AI (miễn phí)
        /// </summary>
        private async Task<ChatBotResponseModel> ProcessWithGeminiAsync(string message, ChatSession session)
        {
            var response = new ChatBotResponseModel();

            try
            {
                // Lấy context menu
                var menuContext = GetMenuContext();
                var historyContext = GetHistoryContext(session);

                var systemPrompt = $@"
Bạn là trợ lý ảo của Nhà Hàng LDP, tên là LDP Bot.
Nhiệm vụ: Tư vấn món ăn, trả lời câu hỏi về nhà hàng.
Phong cách: Thân thiện, ngắn gọn, dùng emoji phù hợp.
Ngôn ngữ: Tiếng Việt.

MENU HIỆN TẠI:
{menuContext}

LỊCH SỬ HỘI THOẠI:
{historyContext}

HƯỚNG DẪN:
- Gợi ý món ăn cụ thể với giá
- Nếu hỏi về đặt bàn, hướng dẫn gọi hotline 0123 456 789
- Trả lời ngắn gọn, tối đa 3-4 câu
- Luôn đề xuất 2-3 món phù hợp

CÂU HỎI: {message}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = systemPrompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 500
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key={_geminiApiKey}";
                var httpResponse = await _httpClient.PostAsync(apiUrl, content);
                var result = await httpResponse.Content.ReadAsStringAsync();

                if (httpResponse.IsSuccessStatusCode)
                {
                    dynamic data = JsonConvert.DeserializeObject(result);
                    response.Response = data?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "";

                    // Thêm suggestions từ database nếu response chứa tên món
                    response.Suggestions = GetRelevantSuggestions(response.Response);
                }
                else
                {
                    // Fallback to rules if API fails
                    return ProcessWithRules(message, AnalyzeIntent(message.ToLower()), session);
                }
            }
            catch
            {
                // Fallback to rules
                return ProcessWithRules(message, AnalyzeIntent(message.ToLower()), session);
            }

            response.QuickReplies = GetDefaultQuickReplies();
            return response;
        }

        /// <summary>
        /// Xử lý với Rule-based (không cần API) - Enhanced version
        /// </summary>
        private ChatBotResponseModel ProcessWithRules(string message, IntentResult intent, ChatSession session)
        {
            var response = new ChatBotResponseModel
            {
                Intent = intent.Intent,
                Confidence = intent.Confidence
            };

            message = message.ToLower();

            // Kiểm tra nếu đang trong booking flow (ưu tiên context)
            if (session.Context.ContainsKey("BookingInfo") && intent.Intent != "GREETING" && intent.Intent != "GOODBYE"
                && intent.Intent != "VIEW_CART" && intent.Intent != "ADD_TO_CART")
            {
                // Nếu user muốn hủy
                if (message.Contains("hủy") || message.Contains("thôi") || message.Contains("cancel"))
                {
                    session.Context.Remove("BookingInfo");
                    response.Response = "❌ Đã hủy đặt bàn.\n\nBạn cần gì thêm không?";
                    response.QuickReplies = new List<string> { "📅 Đặt bàn lại", "🍜 Xem menu", "🔥 Top bán chạy" };
                    return response;
                }
                return HandleConfirmBooking(message, session);
            }

            // Kiểm tra nếu đang trong order flow (ưu tiên context)
            if (session.Context.ContainsKey("OrderStep") && intent.Intent != "GREETING" && intent.Intent != "GOODBYE"
                && intent.Intent != "VIEW_CART" && intent.Intent != "BOOK_TABLE")
            {
                // Nếu user muốn hủy
                if (message.Contains("hủy") || message.Contains("xóa giỏ") || message.Contains("cancel"))
                {
                    session.Context["OrderCart"] = new ChatOrderCart();
                    session.Context.Remove("OrderStep");
                    response.Response = "🗑️ Đã hủy đơn hàng.\n\nBạn cần gì thêm không?";
                    response.QuickReplies = new List<string> { "🍜 Xem menu", "🔥 Top bán chạy", "📅 Đặt bàn" };
                    return response;
                }
                return HandleConfirmOrder(message, session);
            }

            switch (intent.Intent)
            {
                case "GREETING":
                    response.Response = GetGreetingResponse();
                    response.QuickReplies = new List<string>
                    {
                        "🔥 Top món bán chạy",
                        "💰 Món dưới 100k",
                        "🍜 Xem menu",
                        "📅 Đặt bàn"
                    };
                    break;

                case "BEST_SELLERS":
                    response = GetBestSellersResponse();
                    break;

                case "BUDGET":
                    response = GetBudgetResponse(intent.Entities);
                    break;

                case "CATEGORY":
                    response = GetCategoryResponse(intent.Entities.Category ?? DetectCategory(message));
                    break;

                case "NEW_DISHES":
                    response = GetNewDishesResponse();
                    break;

                case "FEATURED":
                    response = GetFeaturedResponse();
                    break;

                case "COMBO":
                    response = GetComboResponse(intent.Entities.PartySize ?? 2);
                    break;

                case "BOOK_TABLE":
                    response = GetSmartBookingResponse(message, intent.Entities, session);
                    break;

                case "HOURS":
                case "LOCATION":
                    response = GetFAQResponse(intent.Intent);
                    break;

                case "PROMOTION":
                    response = GetPromotionResponse();
                    break;

                case "SPICY":
                    response = GetPreferenceResponse("spicy", message);
                    break;

                case "VEGETARIAN":
                    response = GetPreferenceResponse("vegetarian", message);
                    break;

                case "HEALTHY":
                    response = GetPreferenceResponse("healthy", message);
                    break;

                case "ALLERGIES":
                    response = GetAllergyResponse(message);
                    break;

                case "ORDER_STATUS":
                    response = GetOrderStatusResponse();
                    break;

                case "ADD_TO_CART":
                    response = HandleAddToCart(message, intent.Entities, session);
                    break;

                case "VIEW_CART":
                    response = HandleViewCart(session);
                    break;

                case "CONFIRM_ORDER":
                    response = HandleConfirmOrder(message, session);
                    break;

                case "CONFIRM_BOOKING":
                    response = HandleConfirmBooking(message, session);
                    break;

                case "THANKS":
                    response.Response = "😊 Não gate gì! Nếu cần gì thêm, cứ hỏi mình nhé!";
                    response.QuickReplies = new List<string> { "🍜 Xem thêm món", "📅 Đặt bàn", "👋 Tạm biệt" };
                    break;

                case "GOODBYE":
                    response.Response = "👋 Hẹn gặp lại bạn! Chúc bạn ngon miệng! 🍽️";
                    break;

                case "FIND_DISH":
                case "RECOMMEND":
                default:
                    // Tìm kiếm thông minh
                    response = SmartSearch(message, intent.Entities);
                    break;
            }

            return response;
        }

        /// <summary>
        /// Greeting response với thời gian
        /// </summary>
        private string GetGreetingResponse()
        {
            var hour = DateTime.Now.Hour;
            string greeting;

            if (hour < 10)
                greeting = "Chào buổi sáng";
            else if (hour < 14)
                greeting = "Chào buổi trưa";
            else if (hour < 18)
                greeting = "Chào buổi chiều";
            else
                greeting = "Chào buổi tối";

            return $"👋 {greeting}! Mình là **LDP Bot**.\n\n" +
                "Mình có thể giúp bạn:\n" +
                "🍽️ Tìm món ăn phù hợp\n" +
                "🛒 Đặt món nhanh qua chat\n" +
                "💰 Gợi ý theo ngân sách\n" +
                "⭐ Xem món bán chạy\n" +
                "📅 Đặt bàn trực tiếp\n\n" +
                "Bạn muốn gì hôm nay?";
        }

        /// <summary>
        /// Top món bán chạy
        /// </summary>
        private ChatBotResponseModel GetBestSellersResponse()
        {
            var response = new ChatBotResponseModel();

            // Cache top sellers trong 5 phút
            var topItems = _cache.GetOrSet(RedisCacheService.Keys.TopSellers, () =>
            {
                return _db.MenuItem
                    .Where(m => m.IsAvailable)
                    .OrderByDescending(m => m.SoldCount)
                    .Take(5)
                    .Select(m => new MenuSuggestionModel
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Price = m.Price,
                        ImageUrl = m.ImageUrl,
                        Description = m.Description,
                        Category = m.Category,
                        SoldCount = m.SoldCount
                    }).ToList();
            }, TimeSpan.FromMinutes(5));

            response.Response = $"🔥 **Top {topItems.Count} món bán chạy nhất:**\n\n" +
                "Đây là những món được yêu thích nhất!";

            response.Suggestions = topItems;

            response.QuickReplies = new List<string>
            {
                "💰 Món giá rẻ",
                "✨ Món mới",
                "🥗 Xem theo loại",
                "🛒 Đặt món ăn",
                "📅 Đặt bàn"
            };

            return response;
        }

        /// <summary>
        /// Tìm món theo ngân sách
        /// </summary>
        private ChatBotResponseModel GetBudgetResponse(ExtractedEntities entities)
        {
            var response = new ChatBotResponseModel();
            decimal maxPrice = entities.MaxPrice ?? 100000;

            var items = _db.MenuItem
                .Where(m => m.IsAvailable && m.Price <= maxPrice)
                .OrderBy(m => m.Price)
                .Take(6)
                .ToList();

            response.Response = $"💰 **Món ăn dưới {maxPrice / 1000:N0}k:**\n\n" +
                $"Tìm thấy **{items.Count} món** phù hợp ngân sách!";

            response.Suggestions = items.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                Category = m.Category
            }).ToList();

            response.QuickReplies = new List<string>
            {
                "💰 Món dưới 200k",
                "🔥 Món bán chạy",
                "⭐ Món đặc biệt"
            };

            return response;
        }

        /// <summary>
        /// Tìm món theo danh mục
        /// </summary>
        private ChatBotResponseModel GetCategoryResponse(string category)
        {
            var response = new ChatBotResponseModel();

            if (string.IsNullOrEmpty(category))
            {
                // Liệt kê các danh mục
                var categories = _db.MenuItem
                    .Where(m => m.IsAvailable)
                    .GroupBy(m => m.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .ToList();

                response.Response = "📋 **Danh mục món ăn:**\n\n" +
                    string.Join("\n", categories.Select(c => $"• {c.Category}: {c.Count} món"));

                response.QuickReplies = categories.Select(c => $"🍽️ {c.Category}").Take(4).ToList();
                return response;
            }

            var items = _db.MenuItem
                .Where(m => m.IsAvailable && m.Category.Contains(category))
                .OrderByDescending(m => m.SoldCount)
                .Take(6)
                .ToList();

            var emoji = GetCategoryEmoji(category);
            response.Response = $"{emoji} **{category}:**\n\n" +
                $"Có **{items.Count} món** trong danh mục này!";

            response.Suggestions = items.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                Category = m.Category
            }).ToList();

            response.QuickReplies = GetOtherCategoryReplies(category);
            return response;
        }

        /// <summary>
        /// Món mới
        /// </summary>
        private ChatBotResponseModel GetNewDishesResponse()
        {
            var response = new ChatBotResponseModel();

            var items = _db.MenuItem
                .Where(m => m.IsAvailable && m.IsNew)
                .OrderByDescending(m => m.CreatedDate)
                .Take(5)
                .ToList();

            response.Response = $"✨ **Món mới ra mắt:**\n\n" +
                $"Nhà hàng vừa có **{items.Count} món mới** đặc sắc!";

            response.Suggestions = items.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                IsNew = true
            }).ToList();

            response.QuickReplies = new List<string> { "🔥 Món bán chạy", "⭐ Món đặc biệt", "💰 Món giá rẻ" };
            return response;
        }

        /// <summary>
        /// Món đặc biệt
        /// </summary>
        private ChatBotResponseModel GetFeaturedResponse()
        {
            var response = new ChatBotResponseModel();

            var items = _db.MenuItem
                .Where(m => m.IsAvailable && m.IsFeatured)
                .OrderByDescending(m => m.Rating)
                .Take(5)
                .ToList();

            response.Response = $"⭐ **Món đặc biệt hôm nay:**\n\n" +
                $"Đầu bếp đề xuất **{items.Count} món** signature!";

            response.Suggestions = items.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                IsFeatured = true,
                Rating = (double)m.Rating
            }).ToList();

            response.QuickReplies = new List<string> { "🔥 Món bán chạy", "✨ Món mới", "💰 Món giá rẻ" };
            return response;
        }

        /// <summary>
        /// Gợi ý combo theo số người
        /// </summary>
        private ChatBotResponseModel GetComboResponse(int partySize)
        {
            var response = new ChatBotResponseModel();

            // Lấy combo từ database nếu có
            var combos = _db.MenuCombo
                .Where(c => c.IsActive)
                .ToList();

            if (combos.Any())
            {
                response.Response = $"🎯 **Combo cho {partySize} người:**\n\n";
                
                foreach (var combo in combos.Take(3))
                {
                    response.Response += $"• **{combo.Name}**: {combo.ComboPrice:N0}đ\n";
                }
            }
            else
            {
                // Tự động tạo combo suggestion
                var mainDish = _db.MenuItem
                    .Where(m => m.IsAvailable && m.Category == "Món Chính")
                    .OrderByDescending(m => m.SoldCount)
                    .FirstOrDefault();

                var sideDish = _db.MenuItem
                    .Where(m => m.IsAvailable && m.Category == "Món Khai Vị")
                    .OrderByDescending(m => m.SoldCount)
                    .FirstOrDefault();

                var drink = _db.MenuItem
                    .Where(m => m.IsAvailable && m.Category == "Đồ Uống")
                    .OrderByDescending(m => m.SoldCount)
                    .FirstOrDefault();

                var items = new List<MenuItem> { mainDish, sideDish, drink }.Where(x => x != null).ToList();
                var totalPrice = items.Sum(i => i.Price) * partySize;

                response.Response = $"🎯 **Gợi ý combo cho {partySize} người:**\n\n" +
                    $"• {mainDish?.Name} x{partySize}\n" +
                    $"• {sideDish?.Name} x{partySize}\n" +
                    $"• {drink?.Name} x{partySize}\n\n" +
                    $"💵 **Tổng: {totalPrice:N0}đ**";

                response.Suggestions = items.Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl
                }).ToList();
            }

            response.QuickReplies = new List<string> { "📅 Đặt bàn ngay", "🔥 Xem thêm món", "💰 Combo rẻ hơn" };
            return response;
        }

        /// <summary>
        /// Hướng dẫn đặt bàn
        /// </summary>
        private ChatBotResponseModel GetBookingResponse()
        {
            var response = new ChatBotResponseModel
            {
                Response = "📅 **Đặt bàn tại Nhà Hàng LDP:**\n\n" +
                    "Bạn có thể đặt bàn qua:\n\n" +
                    "📞 **Hotline:** 0123 456 789\n" +
                    "🌐 **Website:** nhahangldp.com/dat-ban\n\n" +
                    "Hoặc cho mình biết:\n" +
                    "• Số người\n" +
                    "• Thời gian\n" +
                    "• Số điện thoại\n\n" +
                    "Mình sẽ hỗ trợ đặt bàn cho bạn! 🍽️",
                QuickReplies = new List<string>
                {
                    "2 người tối nay",
                    "4 người cuối tuần",
                    "🍜 Xem menu trước",
                    "🏠 Địa chỉ nhà hàng"
                }
            };

            return response;
        }

        /// <summary>
        /// FAQ Response
        /// </summary>
        private ChatBotResponseModel GetFAQResponse(string intent)
        {
            var response = new ChatBotResponseModel();

            var faq = FAQDatabase.FirstOrDefault(f => 
                (intent == "HOURS" && f.Keywords.Any(k => k.Contains("giờ"))) ||
                (intent == "LOCATION" && f.Keywords.Any(k => k.Contains("địa chỉ"))));

            response.Response = faq?.Answer ?? "Xin lỗi, mình chưa có thông tin này. Vui lòng gọi hotline 0123 456 789!";
            response.QuickReplies = new List<string> { "🍜 Xem menu", "📅 Đặt bàn", "🔥 Món bán chạy" };

            return response;
        }

        /// <summary>
        /// Khuyến mãi
        /// </summary>
        private ChatBotResponseModel GetPromotionResponse()
        {
            var response = new ChatBotResponseModel();

            var activePromotions = _db.Promotion
                .Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .ToList();

            if (activePromotions.Any())
            {
                response.Response = "🎉 **Khuyến mãi đang diễn ra:**\n\n";
                foreach (var promo in activePromotions.Take(3))
                {
                    response.Response += $"🏷️ **{promo.Name}**\n";
                    response.Response += $"   Giảm {(promo.DiscountType == "Percentage" ? $"{promo.DiscountValue}%" : $"{promo.DiscountValue:N0}đ")}\n";
                    response.Response += $"   Đến: {promo.EndDate:dd/MM/yyyy}\n\n";
                }
            }
            else
            {
                response.Response = "📢 Hiện tại chưa có khuyến mãi nào.\n\nTheo dõi trang của chúng tôi để cập nhật ưu đãi mới nhất!";
            }

            response.QuickReplies = new List<string> { "🔥 Món bán chạy", "📅 Đặt bàn", "🍜 Xem menu" };
            return response;
        }

        /// <summary>
        /// Tìm kiếm thông minh với fuzzy matching
        /// </summary>
        private ChatBotResponseModel SmartSearch(string message, ExtractedEntities entities)
        {
            var response = new ChatBotResponseModel();

            // Tìm kiếm trong database
            var allItems = _db.MenuItem.Where(m => m.IsAvailable).ToList();

            // Fuzzy search
            var results = allItems
                .Select(m => new
                {
                    Item = m,
                    NameScore = CalculateSimilarity(message, m.Name.ToLower()),
                    DescScore = m.Description != null ? CalculateSimilarity(message, m.Description.ToLower()) : 0
                })
                .Where(x => x.NameScore > 0.3 || x.DescScore > 0.3)
                .OrderByDescending(x => Math.Max(x.NameScore, x.DescScore))
                .Take(5)
                .Select(x => x.Item)
                .ToList();

            // Apply price filter if exists
            if (entities.MaxPrice.HasValue)
            {
                results = results.Where(r => r.Price <= entities.MaxPrice.Value).ToList();
            }

            // Apply category filter if exists
            if (!string.IsNullOrEmpty(entities.Category))
            {
                results = results.Where(r => r.Category.Contains(entities.Category)).ToList();
            }

            if (results.Any())
            {
                response.Response = $"🔍 **Kết quả tìm kiếm:**\n\n" +
                    $"Mình tìm thấy **{results.Count} món** phù hợp!";

                response.Suggestions = results.Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description,
                    Category = m.Category
                }).ToList();
            }
            else
            {
                response.Response = "🤔 Mình chưa tìm thấy món nào phù hợp.\n\n" +
                    "Bạn có thể thử:\n" +
                    "• Xem menu theo danh mục\n" +
                    "• Xem top món bán chạy\n" +
                    "• Tìm theo ngân sách";
            }

            response.QuickReplies = new List<string>
            {
                "🔥 Top món bán chạy",
                "💰 Món dưới 100k",
                "⭐ Món đặc biệt",
                "📋 Xem theo loại"
            };

            return response;
        }

        /// <summary>
        /// Tính độ tương đồng giữa 2 chuỗi (Jaccard similarity)
        /// </summary>
        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;

            // Simple word-based Jaccard similarity
            var words1 = new HashSet<string>(s1.Split(' ').Where(w => w.Length > 1));
            var words2 = new HashSet<string>(s2.Split(' ').Where(w => w.Length > 1));

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        /// <summary>
        /// Helper: Lấy context menu cho AI
        /// </summary>
        private string GetMenuContext()
        {
            var topItems = _db.MenuItem
                .Where(m => m.IsAvailable)
                .OrderByDescending(m => m.SoldCount)
                .Take(10)
                .Select(m => $"- {m.Name} ({m.Category}): {m.Price:N0}đ")
                .ToList();

            return string.Join("\n", topItems);
        }

        /// <summary>
        /// Helper: Lấy lịch sử hội thoại
        /// </summary>
        private string GetHistoryContext(ChatSession session)
        {
            var recentMessages = session.Messages.Skip(Math.Max(0, session.Messages.Count - 6)).ToList();
            return string.Join("\n", recentMessages.Select(m => $"{m.Role}: {m.Content}"));
        }

        /// <summary>
        /// Helper: Lấy suggestions liên quan từ response text
        /// </summary>
        private List<MenuSuggestionModel> GetRelevantSuggestions(string responseText)
        {
            var items = _db.MenuItem
                .Where(m => m.IsAvailable)
                .OrderByDescending(m => m.SoldCount)
                .Take(3)
                .ToList();

            return items.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description
            }).ToList();
        }

        /// <summary>
        /// Helper: Detect category từ message
        /// </summary>
        private string DetectCategory(string message)
        {
            if (message.Contains("khai vị")) return "Món Khai Vị";
            if (message.Contains("món chính") || message.Contains("chính")) return "Món Chính";
            if (message.Contains("tráng miệng")) return "Tráng Miệng";
            if (message.Contains("đồ uống") || message.Contains("nước")) return "Đồ Uống";
            if (message.Contains("lẩu")) return "Lẩu";
            return null;
        }

        /// <summary>
        /// Helper: Get emoji cho category
        /// </summary>
        private string GetCategoryEmoji(string category)
        {
            if (category.Contains("Khai")) return "🥗";
            if (category.Contains("Chính")) return "🍜";
            if (category.Contains("Tráng")) return "🍰";
            if (category.Contains("Uống")) return "🥤";
            if (category.Contains("Lẩu")) return "🍲";
            return "🍽️";
        }

        /// <summary>
        /// Helper: Get other category quick replies
        /// </summary>
        private List<string> GetOtherCategoryReplies(string currentCategory)
        {
            var all = new List<string> { "🥗 Món Khai Vị", "🍜 Món Chính", "🍰 Tráng Miệng", "🥤 Đồ Uống" };
            return all.Where(c => !c.Contains(currentCategory.Split(' ').Last())).Take(3).ToList();
        }

        /// <summary>
        /// Helper: Default quick replies
        /// </summary>
        private List<string> GetDefaultQuickReplies()
        {
            return new List<string>
            {
                "🔥 Món bán chạy",
                "💰 Món dưới 100k",
                "📅 Đặt bàn",
                "🍜 Xem menu"
            };
        }

        /// <summary>
        /// Session management
        /// </summary>
        private ChatSession GetOrCreateSession(string sessionId)
        {
            // Clean old sessions (older than 30 minutes)
            var cutoff = DateTime.Now.AddMinutes(-30);
            var oldSessions = _sessions.Where(s => s.Value.LastActivity < cutoff).Select(s => s.Key).ToList();
            foreach (var key in oldSessions)
            {
                _sessions.Remove(key);
            }

            if (!string.IsNullOrEmpty(sessionId) && _sessions.ContainsKey(sessionId))
            {
                return _sessions[sessionId];
            }

            var newSession = new ChatSession();
            _sessions[newSession.SessionId] = newSession;
            return newSession;
        }

        /// <summary>
        /// Lấy lịch sử chat của session
        /// </summary>
        public List<ChatMessage> GetChatHistory(string sessionId)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                return _sessions[sessionId].Messages;
            }
            return new List<ChatMessage>();
        }

        /// <summary>
        /// Xóa session
        /// </summary>
        public void ClearSession(string sessionId)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                _sessions.Remove(sessionId);
            }
        }

        /// <summary>
        /// Đặt bàn thông minh - Phân tích thông tin từ tin nhắn
        /// </summary>
        private ChatBotResponseModel GetSmartBookingResponse(string message, ExtractedEntities entities, ChatSession session)
        {
            var response = new ChatBotResponseModel();
            
            // Kiểm tra nếu đang trong booking flow
            if (session.Context.ContainsKey("BookingInfo"))
            {
                return HandleConfirmBooking(message, session);
            }

            // Phân tích thời gian và số khách từ tin nhắn
            DateTime? bookingTime = ParseBookingTime(message);
            int guestCount = entities.PartySize ?? 0;

            if (guestCount == 0)
            {
                var match = Regex.Match(message, @"(\d+)\s*(người|ng|khách)?");
                if (match.Success)
                {
                    guestCount = int.Parse(match.Groups[1].Value);
                }
            }

            // Nếu có cả thời gian và số khách → bắt đầu flow xác nhận
            if (bookingTime.HasValue && guestCount > 0)
            {
                var booking = new ChatBookingInfo
                {
                    NumberOfGuests = guestCount,
                    BookingDateTime = bookingTime,
                    Step = "name"
                };
                session.Context["BookingInfo"] = booking;

                var availableTables = _db.RestaurantTable
                    .Where(t => t.Status == "Available" && t.Capacity >= guestCount)
                    .OrderBy(t => t.Capacity)
                    .Take(3)
                    .ToList();

                response.Response = $"📅 **Đặt bàn:**\n\n" +
                    $"👥 Số khách: **{guestCount} người**\n" +
                    $"🕐 Thời gian: **{bookingTime.Value:dddd dd/MM/yyyy HH:mm}**\n" +
                    (availableTables.Any() ? $"✅ Có **{availableTables.Count} bàn** phù hợp!\n\n" : "⚠️ Bàn hơi đông nhưng sẽ cố sắp xếp!\n\n") +
                    "👤 Cho mình **họ tên** để đặt bàn:";
            }
            else if (guestCount > 0)
            {
                // Có số người, thiếu thời gian
                var booking = new ChatBookingInfo
                {
                    NumberOfGuests = guestCount,
                    Step = "time"
                };
                session.Context["BookingInfo"] = booking;

                response.Response = $"👥 Số khách: **{guestCount} người**\n\n🕐 Bạn muốn đặt vào **thời gian nào**?";
                response.QuickReplies = new List<string> { "🌙 Tối nay", "☀️ Trưa mai", "📅 Cuối tuần", "📆 Ngày khác" };
            }
            else
            {
                // Bắt đầu flow từ đầu
                var booking = new ChatBookingInfo { Step = "guests" };
                session.Context["BookingInfo"] = booking;

                response.Response = "📅 **Đặt bàn Nhà Hàng LDP**\n\n" +
                    "Mình sẽ hỗ trợ đặt bàn nhanh cho bạn!\n\n" +
                    "👥 Bạn đi **bao nhiêu người**?";
                response.QuickReplies = new List<string> { "2 người", "4 người", "6 người", "8 người", "10+ người" };
            }

            return response;
        }

        /// <summary>
        /// Tìm món theo sở thích (cay, chay, healthy)
        /// </summary>
        private ChatBotResponseModel GetPreferenceResponse(string preference, string message)
        {
            var response = new ChatBotResponseModel();

            var query = _db.MenuItem.Where(m => m.IsAvailable);

            switch (preference)
            {
                case "spicy":
                    // Tìm món cay trong description hoặc tags
                    query = query.Where(m => 
                        m.Description.Contains("cay") || 
                        m.Description.Contains("ớt") || 
                        m.Name.Contains("cay"));
                    
                    response.Response = "🌶️ **Món cay cho bạn:**\n\n" +
                        "Những món ăn đậm đà, kích thích vị giác!";
                    break;

                case "vegetarian":
                    query = query.Where(m => 
                        m.Description.Contains("chay") || 
                        m.Description.Contains("rau") ||
                        m.Category.Contains("Chay") ||
                        m.Name.Contains("chay"));
                    
                    response.Response = "🥗 **Món chay:**\n\n" +
                        "Lựa chọn lành mạnh cho bạn!";
                    break;

                case "healthy":
                    query = query.Where(m => 
                        m.Description.Contains("healthy") || 
                        m.Description.Contains("salad") ||
                        m.Category.Contains("Salad") ||
                        m.Description.Contains("lành mạnh"));
                    
                    response.Response = "🥬 **Món healthy:**\n\n" +
                        "Tốt cho sức khỏe, ngon cho vị giác!";
                    break;
            }

            var items = query.OrderByDescending(m => m.SoldCount).Take(5).ToList();

            if (items.Any())
            {
                response.Suggestions = items.Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description,
                    Category = m.Category
                }).ToList();
            }
            else
            {
                response.Response += "\n\n😔 Hiện tại chưa có món phù hợp. Bạn có thể thử xem menu đầy đủ!";
            }

            response.QuickReplies = new List<string>
            {
                "🔥 Món bán chạy",
                "🍜 Xem menu",
                "💰 Món giá rẻ"
            };

            return response;
        }

        /// <summary>
        /// Xử lý dị ứng thực phẩm
        /// </summary>
        private ChatBotResponseModel GetAllergyResponse(string message)
        {
            var response = new ChatBotResponseModel
            {
                Response = "⚠️ **Thông tin dị ứng:**\n\n" +
                    "Để đảm bảo an toàn cho bạn, xin vui lòng:\n\n" +
                    "📞 **Gọi trực tiếp:** 0123 456 789\n" +
                    "💬 **Thông báo nhân viên** khi đến nhà hàng\n\n" +
                    "Chúng tôi sẽ kiểm tra thành phần món ăn và đề xuất các món phù hợp cho bạn!",
                QuickReplies = new List<string>
                {
                    "📞 Gọi hotline",
                    "🥗 Xem món chay",
                    "🍜 Xem menu"
                }
            };

            return response;
        }

        /// <summary>
        /// Trạng thái đơn hàng (placeholder)
        /// </summary>
        private ChatBotResponseModel GetOrderStatusResponse()
        {
            var response = new ChatBotResponseModel
            {
                Response = "📦 **Kiểm tra đơn hàng:**\n\n" +
                    "Để kiểm tra trạng thái đơn hàng, bạn cần:\n\n" +
                    "1. **Mã đơn hàng** (nếu có)\n" +
                    "2. **Số điện thoại** đặt hàng\n\n" +
                    "Hoặc liên hệ: **0123 456 789**",
                QuickReplies = new List<string>
                {
                    "📞 Gọi hotline",
                    "📅 Đặt bàn mới",
                    "🍜 Xem menu"
                }
            };

            return response;
        }

        // ===== CHATBOT ORDERING FLOW =====

        /// <summary>
        /// Lấy giỏ hàng chatbot từ session
        /// </summary>
        private ChatOrderCart GetCartFromSession(ChatSession session)
        {
            if (session.Context.ContainsKey("OrderCart"))
            {
                var cartJson = JsonConvert.SerializeObject(session.Context["OrderCart"]);
                return JsonConvert.DeserializeObject<ChatOrderCart>(cartJson);
            }
            var cart = new ChatOrderCart();
            session.Context["OrderCart"] = cart;
            return cart;
        }

        /// <summary>
        /// Lưu giỏ hàng chatbot vào session
        /// </summary>
        private void SaveCartToSession(ChatSession session, ChatOrderCart cart)
        {
            session.Context["OrderCart"] = cart;
        }

        /// <summary>
        /// Xử lý thêm món vào giỏ qua chat
        /// </summary>
        private ChatBotResponseModel HandleAddToCart(string message, ExtractedEntities entities, ChatSession session)
        {
            var response = new ChatBotResponseModel();
            var cart = GetCartFromSession(session);
            message = message.ToLower();

            // Tìm món ăn phù hợp nhất từ tin nhắn
            var allItems = _db.MenuItem.Where(m => m.IsAvailable).ToList();

            // Trích xuất số lượng
            int quantity = 1;
            var qtyMatch = Regex.Match(message, @"(\d+)\s*(phần|suất|dĩa|tô|ly|chai|lon|cái)");
            if (qtyMatch.Success)
            {
                quantity = int.Parse(qtyMatch.Groups[1].Value);
            }

            // Tìm món bằng fuzzy matching
            var bestMatch = allItems
                .Select(m => new
                {
                    Item = m,
                    Score = CalculateSimilarity(message, m.Name.ToLower())
                })
                .Where(x => x.Score > 0.25)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (bestMatch != null)
            {
                var item = bestMatch.Item;

                // Kiểm tra đã có trong giỏ chưa
                var existingItem = cart.Items.FirstOrDefault(i => i.MenuItemId == item.Id);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    cart.Items.Add(new ChatOrderItem
                    {
                        MenuItemId = item.Id,
                        Name = item.Name,
                        Price = item.Price,
                        Quantity = quantity,
                        ImageUrl = item.ImageUrl
                    });
                }

                SaveCartToSession(session, cart);

                response.Response = $"✅ Đã thêm **{quantity}x {item.Name}** ({item.Price:N0}đ) vào giỏ!\n\n" +
                    $"🛒 **Giỏ hàng:** {cart.TotalItems} món - **{cart.TotalAmount:N0}đ**\n\n" +
                    "Bạn muốn gọi thêm hay đặt ngay?";

                response.Suggestions = new List<MenuSuggestionModel>
                {
                    new MenuSuggestionModel
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Price = item.Price,
                        ImageUrl = item.ImageUrl
                    }
                };

                response.QuickReplies = new List<string>
                {
                    "🔥 Gọi thêm món",
                    "🛒 Xem giỏ hàng",
                    "✅ Đặt hàng ngay",
                    "❌ Xóa giỏ hàng"
                };

                response.Metadata["action"] = "item_added";
                response.Metadata["cart_total"] = cart.TotalAmount;
                response.Metadata["cart_count"] = cart.TotalItems;
            }
            else
            {
                // Không tìm thấy → gợi ý top món
                var topItems = allItems.OrderByDescending(m => m.SoldCount).Take(5).ToList();

                response.Response = "🤔 Mình chưa tìm thấy món bạn muốn.\n\n" +
                    "Hãy chọn một trong những món bán chạy dưới đây, hoặc nói rõ tên món nhé!";

                response.Suggestions = topItems.Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Category = m.Category,
                    SoldCount = m.SoldCount
                }).ToList();

                response.QuickReplies = new List<string> { "🍜 Xem menu", "🔥 Top bán chạy" };
            }

            return response;
        }

        /// <summary>
        /// Thêm món vào giỏ theo ID (từ nút bấm)
        /// </summary>
        public ChatBotResponseModel AddItemToCart(int menuItemId, int quantity, string sessionId)
        {
            var response = new ChatBotResponseModel();
            var session = GetOrCreateSession(sessionId);
            response.SessionId = session.SessionId;
            var cart = GetCartFromSession(session);

            var item = _db.MenuItem.FirstOrDefault(m => m.Id == menuItemId && m.IsAvailable);
            if (item == null)
            {
                response.Success = false;
                response.Response = "Món ăn không tồn tại hoặc đã hết!";
                return response;
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.MenuItemId == item.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new ChatOrderItem
                {
                    MenuItemId = item.Id,
                    Name = item.Name,
                    Price = item.Price,
                    Quantity = quantity,
                    ImageUrl = item.ImageUrl
                });
            }

            SaveCartToSession(session, cart);

            response.Success = true;
            response.Response = $"✅ Đã thêm **{quantity}x {item.Name}** ({item.Price:N0}đ) vào giỏ!\n\n" +
                $"🛒 **Giỏ hàng:** {cart.TotalItems} món - **{cart.TotalAmount:N0}đ**";

            response.QuickReplies = new List<string>
            {
                "🔥 Gọi thêm món",
                "🛒 Xem giỏ hàng",
                "✅ Đặt hàng ngay"
            };

            response.Metadata["action"] = "item_added";
            response.Metadata["cart_total"] = cart.TotalAmount;
            response.Metadata["cart_count"] = cart.TotalItems;

            return response;
        }

        /// <summary>
        /// Xem giỏ hàng chatbot
        /// </summary>
        private ChatBotResponseModel HandleViewCart(ChatSession session)
        {
            var response = new ChatBotResponseModel();
            var cart = GetCartFromSession(session);

            if (!cart.Items.Any())
            {
                response.Response = "🛒 Giỏ hàng của bạn đang trống!\n\n" +
                    "Hãy chọn món từ menu hoặc nói tên món bạn muốn gọi.";
                response.QuickReplies = new List<string> { "🔥 Top bán chạy", "🍜 Xem menu", "💰 Món dưới 100k" };
                return response;
            }

            var cartText = "🛒 **Giỏ hàng của bạn:**\n\n";
            int idx = 1;
            foreach (var item in cart.Items)
            {
                cartText += $"{idx}. **{item.Name}** x{item.Quantity} = {(item.Price * item.Quantity):N0}đ\n";
                idx++;
            }
            cartText += $"\n💵 **Tổng cộng: {cart.TotalAmount:N0}đ**\n\n" +
                "Bạn muốn thêm món hay đặt hàng?";

            response.Response = cartText;
            response.QuickReplies = new List<string>
            {
                "✅ Đặt hàng ngay",
                "🔥 Gọi thêm món",
                "❌ Xóa giỏ hàng"
            };

            response.Metadata["action"] = "view_cart";
            response.Metadata["cart_total"] = cart.TotalAmount;
            response.Metadata["cart_count"] = cart.TotalItems;

            return response;
        }

        /// <summary>
        /// Xử lý xác nhận đơn hàng qua chat
        /// </summary>
        private ChatBotResponseModel HandleConfirmOrder(string message, ChatSession session)
        {
            var response = new ChatBotResponseModel();
            var cart = GetCartFromSession(session);

            if (!cart.Items.Any())
            {
                response.Response = "🛒 Giỏ hàng trống! Hãy chọn món trước nhé.";
                response.QuickReplies = new List<string> { "🔥 Top bán chạy", "🍜 Xem menu" };
                return response;
            }

            // Kiểm tra đã có thông tin khách chưa
            string step = session.Context.ContainsKey("OrderStep") ? session.Context["OrderStep"].ToString() : "start";

            switch (step)
            {
                case "start":
                    // Hiển thị giỏ hàng và hỏi thông tin
                    var cartText = "📋 **Xác nhận đơn hàng:**\n\n";
                    foreach (var item in cart.Items)
                    {
                        cartText += $"• {item.Name} x{item.Quantity} = {(item.Price * item.Quantity):N0}đ\n";
                    }
                    cartText += $"\n💵 **Tổng: {cart.TotalAmount:N0}đ**\n\n" +
                        "Vui lòng cho mình **họ tên** của bạn:";

                    response.Response = cartText;
                    session.Context["OrderStep"] = "name";
                    break;

                case "name":
                    cart.CustomerName = message.Trim();
                    SaveCartToSession(session, cart);
                    response.Response = $"👤 Tên: **{cart.CustomerName}**\n\nVui lòng cho mình **số điện thoại**:";
                    session.Context["OrderStep"] = "phone";
                    break;

                case "phone":
                    var phoneMatch = Regex.Match(message, @"(0\d{9,10})");
                    if (phoneMatch.Success)
                    {
                        cart.CustomerPhone = phoneMatch.Groups[1].Value;
                        SaveCartToSession(session, cart);
                        response.Response = $"📞 SĐT: **{cart.CustomerPhone}**\n\n" +
                            "Bạn muốn **ăn tại nhà hàng** hay **giao hàng**?";
                        response.QuickReplies = new List<string> { "🍽️ Ăn tại nhà hàng", "🚗 Giao hàng", "🛍️ Mang về" };
                        session.Context["OrderStep"] = "type";
                    }
                    else
                    {
                        response.Response = "❌ Số điện thoại chưa hợp lệ. Vui lòng nhập lại (VD: 0901234567):";
                    }
                    break;

                case "type":
                    if (message.Contains("giao") || message.Contains("delivery"))
                    {
                        cart.OrderType = "Delivery";
                        SaveCartToSession(session, cart);
                        response.Response = "🏠 Vui lòng cho mình **địa chỉ giao hàng**:";
                        session.Context["OrderStep"] = "address";
                    }
                    else if (message.Contains("mang về") || message.Contains("takeaway"))
                    {
                        cart.OrderType = "TakeAway";
                        SaveCartToSession(session, cart);
                        session.Context["OrderStep"] = "final_confirm";
                        return ShowFinalOrderConfirmation(cart, response, session);
                    }
                    else
                    {
                        cart.OrderType = "DineIn";
                        SaveCartToSession(session, cart);
                        session.Context["OrderStep"] = "final_confirm";
                        return ShowFinalOrderConfirmation(cart, response, session);
                    }
                    break;

                case "address":
                    cart.DeliveryAddress = message.Trim();
                    SaveCartToSession(session, cart);
                    session.Context["OrderStep"] = "final_confirm";
                    return ShowFinalOrderConfirmation(cart, response, session);

                case "final_confirm":
                    // Người dùng xác nhận → tạo đơn
                    if (message.Contains("xác nhận") || message.Contains("ok") || message.Contains("đồng ý") || message.Contains("đặt"))
                    {
                        return CreateOrderFromChat(cart, session);
                    }
                    else
                    {
                        response.Response = "Bạn muốn **xác nhận đặt hàng** hay **chỉnh sửa**?";
                        response.QuickReplies = new List<string> { "✅ Xác nhận đặt hàng", "✏️ Sửa đơn", "❌ Hủy đơn" };
                    }
                    break;
            }

            return response;
        }

        /// <summary>
        /// Hiển thị xác nhận cuối cùng trước khi đặt
        /// </summary>
        private ChatBotResponseModel ShowFinalOrderConfirmation(ChatOrderCart cart, ChatBotResponseModel response, ChatSession session)
        {
            var orderTypeText = cart.OrderType == "Delivery" ? "Giao hàng" :
                               cart.OrderType == "TakeAway" ? "Mang về" : "Ăn tại nhà hàng";

            var confirmText = "📋 **XÁC NHẬN ĐƠN HÀNG:**\n\n";
            confirmText += $"👤 Tên: **{cart.CustomerName}**\n";
            confirmText += $"📞 SĐT: **{cart.CustomerPhone}**\n";
            confirmText += $"📦 Hình thức: **{orderTypeText}**\n";
            if (cart.OrderType == "Delivery" && !string.IsNullOrEmpty(cart.DeliveryAddress))
            {
                confirmText += $"🏠 Địa chỉ: **{cart.DeliveryAddress}**\n";
            }
            confirmText += "\n**Món đã đặt:**\n";
            foreach (var item in cart.Items)
            {
                confirmText += $"• {item.Name} x{item.Quantity} = {(item.Price * item.Quantity):N0}đ\n";
            }
            confirmText += $"\n💵 **Tổng cộng: {cart.TotalAmount:N0}đ**\n\n";
            confirmText += "Bấm **Xác nhận** để hoàn tất đặt hàng!";

            response.Response = confirmText;
            response.QuickReplies = new List<string>
            {
                "✅ Xác nhận đặt hàng",
                "✏️ Sửa đơn",
                "❌ Hủy đơn"
            };

            response.Metadata["action"] = "order_review";
            return response;
        }

        /// <summary>
        /// Tạo đơn hàng thực tế từ giỏ chatbot
        /// </summary>
        private ChatBotResponseModel CreateOrderFromChat(ChatOrderCart cart, ChatSession session)
        {
            var response = new ChatBotResponseModel();

            try
            {
                // Tạo mã đơn hàng
                var orderCode = "DH" + DateTime.Now.ToString("yyMMdd") + new Random().Next(1000, 9999).ToString();

                // Insert CustomerOrder
                var sql = @"INSERT INTO CustomerOrder 
                    (OrderCode, CustomerName, CustomerPhone, OrderType, DeliveryAddress,
                     SubTotal, DeliveryFee, Discount, TotalAmount, PaymentMethod, PaymentStatus, 
                     Status, Note, OrderDate, EstimatedDeliveryTime) 
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, GETDATE(), DATEADD(HOUR, 1, GETDATE()));
                    SELECT SCOPE_IDENTITY();";

                decimal deliveryFee = cart.OrderType == "Delivery" ? 30000 : 0;
                var totalAmount = cart.TotalAmount + deliveryFee;

                var orderId = _db.Database.SqlQuery<decimal>(sql,
                    orderCode,
                    cart.CustomerName ?? "Khách chatbot",
                    cart.CustomerPhone ?? "",
                    cart.OrderType,
                    cart.DeliveryAddress ?? "",
                    cart.TotalAmount,
                    deliveryFee,
                    0m, // discount
                    totalAmount,
                    "COD",
                    "Pending",
                    "Pending",
                    "Đặt qua chatbot" + (string.IsNullOrEmpty(cart.Note) ? "" : " - " + cart.Note)
                ).FirstOrDefault();

                if (orderId > 0)
                {
                    // Insert OrderDetails
                    foreach (var item in cart.Items)
                    {
                        var detailSql = @"INSERT INTO CustomerOrderDetail 
                            (CustomerOrderId, MenuItemId, ItemName, Quantity, UnitPrice, Subtotal) 
                            VALUES (@p0, @p1, @p2, @p3, @p4, @p5)";

                        _db.Database.ExecuteSqlCommand(detailSql,
                            (int)orderId,
                            item.MenuItemId,
                            item.Name,
                            item.Quantity,
                            item.Price,
                            item.Price * item.Quantity
                        );
                    }

                    // Reset cart
                    session.Context["OrderCart"] = new ChatOrderCart();
                    session.Context.Remove("OrderStep");

                    var orderTypeText = cart.OrderType == "Delivery" ? "Giao hàng" :
                                       cart.OrderType == "TakeAway" ? "Mang về" : "Ăn tại nhà hàng";

                    response.Success = true;
                    response.Response = $"🎉 **ĐẶT HÀNG THÀNH CÔNG!**\n\n" +
                        $"📦 Mã đơn: **{orderCode}**\n" +
                        $"📋 Hình thức: **{orderTypeText}**\n" +
                        $"💵 Tổng tiền: **{totalAmount:N0}đ**\n" +
                        $"💳 Thanh toán: **Khi nhận hàng (COD)**\n\n" +
                        "Cảm ơn bạn đã đặt hàng! 🙏\n" +
                        "Nhà hàng sẽ xác nhận đơn trong vài phút.";

                    response.QuickReplies = new List<string>
                    {
                        "🍜 Đặt thêm đơn mới",
                        "📅 Đặt bàn",
                        "🔥 Xem menu"
                    };

                    response.Metadata["action"] = "order_created";
                    response.Metadata["order_code"] = orderCode;
                    response.Metadata["order_id"] = (int)orderId;
                }
                else
                {
                    response.Success = false;
                    response.Response = "❌ Có lỗi khi tạo đơn hàng. Vui lòng thử lại hoặc gọi **0123 456 789**!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateOrderFromChat Error: {ex.Message}");
                response.Success = false;
                response.Response = "❌ Có lỗi khi tạo đơn hàng. Vui lòng gọi **0123 456 789** để được hỗ trợ!";
            }

            return response;
        }

        /// <summary>
        /// Xóa giỏ hàng chatbot
        /// </summary>
        public ChatBotResponseModel ClearCart(string sessionId)
        {
            var response = new ChatBotResponseModel();
            var session = GetOrCreateSession(sessionId);
            response.SessionId = session.SessionId;

            session.Context["OrderCart"] = new ChatOrderCart();
            session.Context.Remove("OrderStep");

            response.Success = true;
            response.Response = "🗑️ Đã xóa giỏ hàng!\n\nBạn muốn chọn món mới?";
            response.QuickReplies = new List<string> { "🔥 Top bán chạy", "🍜 Xem menu", "📅 Đặt bàn" };

            return response;
        }

        /// <summary>
        /// Lấy giỏ hàng hiện tại (public cho controller)
        /// </summary>
        public ChatOrderCart GetCurrentCart(string sessionId)
        {
            var session = GetOrCreateSession(sessionId);
            return GetCartFromSession(session);
        }

        // ===== CHATBOT BOOKING FLOW =====

        /// <summary>
        /// Lấy thông tin booking từ session
        /// </summary>
        private ChatBookingInfo GetBookingFromSession(ChatSession session)
        {
            if (session.Context.ContainsKey("BookingInfo"))
            {
                var json = JsonConvert.SerializeObject(session.Context["BookingInfo"]);
                return JsonConvert.DeserializeObject<ChatBookingInfo>(json);
            }
            var booking = new ChatBookingInfo();
            session.Context["BookingInfo"] = booking;
            return booking;
        }

        /// <summary>
        /// Xử lý xác nhận đặt bàn qua chat (multi-step)
        /// </summary>
        private ChatBotResponseModel HandleConfirmBooking(string message, ChatSession session)
        {
            var response = new ChatBotResponseModel();
            var booking = GetBookingFromSession(session);
            message = message.ToLower();

            string step = booking.Step ?? "init";

            switch (step)
            {
                case "guests":
                    var guestMatch = Regex.Match(message, @"(\d+)");
                    if (guestMatch.Success)
                    {
                        booking.NumberOfGuests = int.Parse(guestMatch.Groups[1].Value);
                        booking.Step = "time";
                        session.Context["BookingInfo"] = booking;
                        response.Response = $"👥 Số khách: **{booking.NumberOfGuests} người**\n\n" +
                            "🕐 Bạn muốn đặt vào **thời gian nào**?";
                        response.QuickReplies = new List<string>
                        {
                            "🌙 Tối nay",
                            "☀️ Trưa mai",
                            "📅 Cuối tuần",
                            "📆 Ngày khác"
                        };
                    }
                    else
                    {
                        response.Response = "Vui lòng nhập **số người** (VD: 4):";
                    }
                    break;

                case "time":
                    DateTime? bookingTime = ParseBookingTime(message);
                    if (bookingTime.HasValue)
                    {
                        booking.BookingDateTime = bookingTime;
                        booking.Step = "name";
                        session.Context["BookingInfo"] = booking;
                        response.Response = $"🕐 Thời gian: **{bookingTime.Value:dddd dd/MM/yyyy HH:mm}**\n\n" +
                            "👤 Cho mình **họ tên** để đặt bàn:";
                    }
                    else
                    {
                        response.Response = "Mình chưa hiểu thời gian. Vui lòng nói rõ hơn (VD: tối nay, trưa mai, thứ 7):";
                        response.QuickReplies = new List<string> { "🌙 Tối nay", "☀️ Trưa mai", "📅 Cuối tuần" };
                    }
                    break;

                case "name":
                    booking.CustomerName = message.Trim();
                    if (booking.CustomerName.Length > 1)
                    {
                        booking.Step = "phone";
                        session.Context["BookingInfo"] = booking;
                        response.Response = $"👤 Tên: **{booking.CustomerName}**\n\n📞 Cho mình **số điện thoại** để liên hệ:";
                    }
                    else
                    {
                        response.Response = "Vui lòng nhập **họ tên** đầy đủ:";
                    }
                    break;

                case "phone":
                    var phoneMatch = Regex.Match(message, @"(0\d{9,10})");
                    if (phoneMatch.Success)
                    {
                        booking.CustomerPhone = phoneMatch.Groups[1].Value;
                        booking.Step = "confirm";
                        session.Context["BookingInfo"] = booking;
                        return ShowBookingConfirmation(booking, response);
                    }
                    else
                    {
                        response.Response = "❌ Số điện thoại chưa hợp lệ. Vui lòng nhập lại (VD: 0901234567):";
                    }
                    break;

                case "confirm":
                    if (message.Contains("xác nhận") || message.Contains("ok") || message.Contains("đồng ý") || message.Contains("đặt"))
                    {
                        return CreateBookingFromChat(booking, session);
                    }
                    else
                    {
                        response.Response = "Bạn muốn **xác nhận đặt bàn** hay **thay đổi**?";
                        response.QuickReplies = new List<string> { "✅ Xác nhận đặt bàn", "🔄 Đặt lại từ đầu", "❌ Hủy" };
                    }
                    break;

                default:
                    // Start booking flow
                    booking.Step = "guests";
                    session.Context["BookingInfo"] = booking;
                    response.Response = "📅 **Đặt bàn Nhà Hàng LDP**\n\n👥 Bạn đi **bao nhiêu người**?";
                    response.QuickReplies = new List<string> { "2 người", "4 người", "6 người", "8 người", "10+ người" };
                    break;
            }

            return response;
        }

        /// <summary>
        /// Parse thời gian đặt bàn từ ngôn ngữ tự nhiên
        /// </summary>
        private DateTime? ParseBookingTime(string message)
        {
            if (message.Contains("tối nay") || message.Contains("tonight"))
                return DateTime.Today.AddHours(19);
            if (message.Contains("trưa nay"))
                return DateTime.Today.AddHours(12);
            if (message.Contains("trưa mai"))
                return DateTime.Today.AddDays(1).AddHours(12);
            if (message.Contains("tối mai"))
                return DateTime.Today.AddDays(1).AddHours(19);
            if (message.Contains("mai"))
                return DateTime.Today.AddDays(1).AddHours(19);
            if (message.Contains("cuối tuần") || message.Contains("weekend") || message.Contains("thứ 7") || message.Contains("thứ bảy"))
            {
                var nextSat = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7);
                if (nextSat == DateTime.Today) nextSat = nextSat.AddDays(7);
                return nextSat.AddHours(19);
            }
            if (message.Contains("chủ nhật"))
            {
                var nextSun = DateTime.Today.AddDays(((int)DayOfWeek.Sunday - (int)DateTime.Today.DayOfWeek + 7) % 7);
                if (nextSun == DateTime.Today) nextSun = nextSun.AddDays(7);
                return nextSun.AddHours(12);
            }

            // Try parse explicit time
            var timeMatch = Regex.Match(message, @"(\d{1,2})[h:](\d{0,2})");
            if (timeMatch.Success)
            {
                int hour = int.Parse(timeMatch.Groups[1].Value);
                int minute = timeMatch.Groups[2].Success && timeMatch.Groups[2].Value.Length > 0
                    ? int.Parse(timeMatch.Groups[2].Value) : 0;
                var date = message.Contains("mai") ? DateTime.Today.AddDays(1) : DateTime.Today;
                if (hour >= 0 && hour <= 23)
                    return date.AddHours(hour).AddMinutes(minute);
            }

            return null;
        }

        /// <summary>
        /// Hiển thị xác nhận đặt bàn
        /// </summary>
        private ChatBotResponseModel ShowBookingConfirmation(ChatBookingInfo booking, ChatBotResponseModel response)
        {
            // Kiểm tra bàn trống
            var availableTables = _db.RestaurantTable
                .Where(t => t.Status == "Available" && t.Capacity >= booking.NumberOfGuests)
                .OrderBy(t => t.Capacity)
                .Take(3)
                .ToList();

            var confirmText = "📋 **XÁC NHẬN ĐẶT BÀN:**\n\n";
            confirmText += $"👤 Tên: **{booking.CustomerName}**\n";
            confirmText += $"📞 SĐT: **{booking.CustomerPhone}**\n";
            confirmText += $"👥 Số khách: **{booking.NumberOfGuests} người**\n";
            confirmText += $"🕐 Thời gian: **{booking.BookingDateTime:dddd dd/MM/yyyy HH:mm}**\n\n";

            if (availableTables.Any())
            {
                confirmText += $"✅ Có **{availableTables.Count} bàn** phù hợp!\n\n";
            }
            else
            {
                confirmText += "⚠️ Hiện tại bàn hơi đông, nhưng chúng tôi sẽ cố gắng sắp xếp!\n\n";
            }

            confirmText += "Bấm **Xác nhận** để hoàn tất đặt bàn!";

            response.Response = confirmText;
            response.QuickReplies = new List<string>
            {
                "✅ Xác nhận đặt bàn",
                "🔄 Đặt lại từ đầu",
                "❌ Hủy"
            };

            response.Metadata["action"] = "booking_review";
            return response;
        }

        /// <summary>
        /// Tạo reservation thực tế từ chatbot
        /// </summary>
        private ChatBotResponseModel CreateBookingFromChat(ChatBookingInfo booking, ChatSession session)
        {
            var response = new ChatBotResponseModel();

            try
            {
                // Tạo reservation code
                var resCode = "RES" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999).ToString();

                // Tìm bàn phù hợp
                var table = _db.RestaurantTable
                    .Where(t => t.Status == "Available" && t.Capacity >= booking.NumberOfGuests)
                    .OrderBy(t => t.Capacity)
                    .FirstOrDefault();

                int? tableId = table?.Id;

                // Insert Reservation
                var sql = @"INSERT INTO Reservation 
                    (ReservationCode, CustomerName, CustomerPhone, ReservationDate, ReservationTime, 
                     NumberOfGuests, TableId, SpecialRequests, Status, CreatedDate) 
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                var bookingDate = booking.BookingDateTime ?? DateTime.Now.AddHours(2);
                var bookingTimeSpan = bookingDate.TimeOfDay;

                var resId = _db.Database.SqlQuery<decimal>(sql,
                    resCode,
                    booking.CustomerName,
                    booking.CustomerPhone,
                    bookingDate.Date,
                    bookingTimeSpan,
                    booking.NumberOfGuests,
                    tableId.HasValue ? (object)tableId.Value : DBNull.Value,
                    booking.SpecialRequests ?? "Đặt qua chatbot",
                    "Pending"
                ).FirstOrDefault();

                if (resId > 0)
                {
                    // Xóa booking info
                    session.Context.Remove("BookingInfo");

                    response.Success = true;
                    response.Response = $"🎉 **ĐẶT BÀN THÀNH CÔNG!**\n\n" +
                        $"📋 Mã đặt bàn: **{resCode}**\n" +
                        $"👤 Tên: **{booking.CustomerName}**\n" +
                        $"👥 Số khách: **{booking.NumberOfGuests} người**\n" +
                        $"🕐 Thời gian: **{bookingDate:dddd dd/MM/yyyy HH:mm}**\n" +
                        (table != null ? $"🪑 Bàn số: **{table.TableNumber}** (sức chứa {table.Capacity})\n" : "") +
                        "\nCảm ơn bạn! Nhà hàng sẽ xác nhận trong vài phút. 🙏\n" +
                        "📞 Hotline: **0123 456 789**";

                    response.QuickReplies = new List<string>
                    {
                        "🍜 Xem menu trước",
                        "🔥 Gợi ý combo",
                        "👋 Cảm ơn"
                    };

                    response.Metadata["action"] = "booking_created";
                    response.Metadata["reservation_code"] = resCode;
                }
                else
                {
                    response.Success = false;
                    response.Response = "❌ Có lỗi khi đặt bàn. Vui lòng gọi **0123 456 789**!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateBookingFromChat Error: {ex.Message}");
                response.Success = false;
                response.Response = "❌ Có lỗi khi đặt bàn. Vui lòng gọi **0123 456 789** để được hỗ trợ!";
            }

            return response;
        }
    }
}
