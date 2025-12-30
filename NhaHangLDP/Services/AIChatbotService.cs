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
            { "ORDER", new List<string> { "đặt món", "order", "mua", "đặt hàng", "thêm vào", "gọi món", "lấy", "muốn ăn", "muốn mua", "cho tôi", "cho mình", "lấy cho" } },
            { "CONFIRM_YES", new List<string> { "đúng", "ok", "xác nhận", "đồng ý", "được", "yes", "có", "ừ", "oke", "chắc chắn", "vâng" } },
            { "CONFIRM_NO", new List<string> { "không", "hủy", "thôi", "cancel", "no", "sai", "chưa", "bỏ", "dừng" } }
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

            // Check if we're in an active order or booking flow first
            if (session.Context.ContainsKey("OrderContext"))
            {
                var orderContext = session.Context["OrderContext"] as ChatbotOrderContext;
                if (orderContext != null && orderContext.State != "IDLE")
                {
                    return ProcessOrderFlow(message, intent, session, orderContext);
                }
            }

            if (session.Context.ContainsKey("ReservationContext"))
            {
                var reservationContext = session.Context["ReservationContext"] as ChatbotReservationContext;
                if (reservationContext != null && reservationContext.State != "IDLE")
                {
                    return ProcessReservationFlow(message, intent, session, reservationContext);
                }
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
                        "📅 Đặt bàn",
                        "🛒 Đặt món"
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
                    response = StartReservationFlow(message, intent.Entities, session);
                    break;

                case "ORDER":
                    response = StartOrderFlow(message, intent.Entities, session);
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

                case "THANKS":
                    response.Response = "😊 Không có gì! Nếu cần gì thêm, cứ hỏi mình nhé!";
                    response.QuickReplies = new List<string> { "🍜 Xem thêm món", "📅 Đặt bàn", "🛒 Đặt món", "👋 Tạm biệt" };
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
                "🍽️ Tìm và gợi ý món ăn\n" +
                "🛒 Đặt món giao hàng\n" +
                "📅 Đặt bàn nhà hàng\n" +
                "❓ Trả lời câu hỏi\n\n" +
                "Bạn muốn làm gì hôm nay?";
        }

        /// <summary>
        /// Top món bán chạy
        /// </summary>
        private ChatBotResponseModel GetBestSellersResponse()
        {
            var response = new ChatBotResponseModel();

            var topItems = _db.MenuItem
                .Where(m => m.IsAvailable)
                .OrderByDescending(m => m.SoldCount)
                .Take(5)
                .ToList();

            response.Response = $"🔥 **Top {topItems.Count} món bán chạy nhất:**\n\n" +
                "Đây là những món được yêu thích nhất!";

            response.Suggestions = topItems.Select(m => new MenuSuggestionModel
            {
                Id = m.Id,
                Name = m.Name,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Description = m.Description,
                Category = m.Category,
                SoldCount = m.SoldCount
            }).ToList();

            response.QuickReplies = new List<string>
            {
                "💰 Món giá rẻ",
                "✨ Món mới",
                "🥗 Xem theo loại",
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
            
            // Phân tích thời gian từ tin nhắn
            DateTime? bookingTime = null;
            int guestCount = entities.PartySize ?? 0;

            // Detect time expressions
            if (message.Contains("tối nay") || message.Contains("tonight"))
            {
                bookingTime = DateTime.Today.AddHours(19);
            }
            else if (message.Contains("trưa nay") || message.Contains("trưa"))
            {
                bookingTime = DateTime.Today.AddHours(12);
            }
            else if (message.Contains("cuối tuần") || message.Contains("weekend"))
            {
                var nextSaturday = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7);
                bookingTime = nextSaturday.AddHours(19);
            }
            else if (message.Contains("mai") || message.Contains("tomorrow"))
            {
                bookingTime = DateTime.Today.AddDays(1).AddHours(19);
            }

            // Detect guest count if not found
            if (guestCount == 0)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s*(người|ng|khách)?");
                if (match.Success)
                {
                    guestCount = int.Parse(match.Groups[1].Value);
                }
            }

            // Lưu vào context
            if (bookingTime.HasValue)
            {
                session.Context["BookingTime"] = bookingTime.Value;
            }
            if (guestCount > 0)
            {
                session.Context["GuestCount"] = guestCount;
            }

            // Tạo response dựa trên thông tin đã có
            if (bookingTime.HasValue && guestCount > 0)
            {
                // Kiểm tra bàn trống
                var availableTables = _db.RestaurantTable
                    .Where(t => t.Status == "Available" && t.Capacity >= guestCount)
                    .OrderBy(t => t.Capacity)
                    .Take(3)
                    .ToList();

                if (availableTables.Any())
                {
                    response.Response = $"📅 **Thông tin đặt bàn:**\n\n" +
                        $"👥 Số khách: **{guestCount} người**\n" +
                        $"🕐 Thời gian: **{bookingTime.Value:dddd, dd/MM/yyyy HH:mm}**\n\n" +
                        $"✅ Có **{availableTables.Count} bàn** phù hợp!\n\n" +
                        "📞 Để xác nhận, vui lòng:\n" +
                        "• Gọi: **0123 456 789**\n" +
                        "• Hoặc cho mình số điện thoại của bạn";

                    response.QuickReplies = new List<string>
                    {
                        "✅ Gọi ngay 0123 456 789",
                        "🔄 Đổi thời gian",
                        "🍜 Xem menu trước"
                    };
                }
                else
                {
                    response.Response = $"😔 Rất tiếc, hiện không có bàn phù hợp cho **{guestCount} người** vào thời gian này.\n\n" +
                        "Bạn có thể:\n" +
                        "• Thử thời gian khác\n" +
                        "• Gọi hotline **0123 456 789** để được hỗ trợ";

                    response.QuickReplies = new List<string>
                    {
                        "🕐 Thử giờ khác",
                        "📞 Gọi hotline"
                    };
                }
            }
            else
            {
                // Chưa đủ thông tin
                response.Response = "📅 **Đặt bàn tại Nhà Hàng LDP:**\n\n" +
                    "Cho mình biết thêm:\n" +
                    "• 👥 Số người?\n" +
                    "• 🕐 Thời gian? (VD: tối nay, trưa mai, cuối tuần)\n\n" +
                    "Hoặc gọi ngay: **0123 456 789**";

                response.QuickReplies = new List<string>
                {
                    "2 người tối nay",
                    "4 người cuối tuần",
                    "6 người trưa mai",
                    "📞 Gọi hotline"
                };
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

        #region Order Flow Methods

        /// <summary>
        /// Start order flow - Initial entry point
        /// </summary>
        private ChatBotResponseModel StartOrderFlow(string message, ExtractedEntities entities, ChatSession session)
        {
            var response = new ChatBotResponseModel();

            // Create or get order context
            var orderContext = new ChatbotOrderContext
            {
                State = "COLLECTING_ITEMS",
                LastUpdated = DateTime.Now
            };

            // Try to extract dish name from message
            var dishName = ExtractDishName(message);
            var quantity = ExtractQuantity(message);

            if (!string.IsNullOrEmpty(dishName))
            {
                // Search for the dish
                var foundItems = SearchDishByName(dishName);
                
                if (foundItems.Any())
                {
                    if (foundItems.Count == 1)
                    {
                        // Found exact match - add to order
                        var item = foundItems.First();
                        orderContext.Items.Add(new ChatbotOrderItem
                        {
                            MenuItemId = item.Id,
                            Name = item.Name,
                            Price = item.Price,
                            Quantity = quantity > 0 ? quantity : 1,
                            ImageUrl = item.ImageUrl
                        });

                        response.Response = $"🛒 **Đã thêm vào đơn:**\n\n" +
                            $"• {item.Name} x{(quantity > 0 ? quantity : 1)} - {item.Price * (quantity > 0 ? quantity : 1):N0}đ\n\n" +
                            "Bạn muốn thêm món khác không?";

                        response.Suggestions = GetSuggestedItemsForOrder(orderContext);
                        response.QuickReplies = new List<string>
                        {
                            "✅ Xong, đặt đơn",
                            "➕ Thêm món khác",
                            "👀 Xem giỏ hàng",
                            "❌ Hủy đơn"
                        };
                    }
                    else
                    {
                        // Multiple matches - show options
                        response.Response = $"🔍 Mình tìm thấy **{foundItems.Count} món** phù hợp. Bạn muốn món nào?\n\n" +
                            "Nhấn chọn món bên dưới hoặc gõ tên chính xác:";

                        response.Suggestions = foundItems.Take(5).Select(m => new MenuSuggestionModel
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Price = m.Price,
                            ImageUrl = m.ImageUrl,
                            Description = m.Description
                        }).ToList();

                        response.QuickReplies = new List<string> { "❌ Hủy đơn", "🍜 Xem menu" };
                    }
                }
                else
                {
                    response.Response = $"🤔 Mình không tìm thấy món \"{dishName}\".\n\n" +
                        "Bạn có thể chọn từ các món phổ biến bên dưới:";

                    response.Suggestions = GetBestSellerSuggestions(5);
                    response.QuickReplies = new List<string> { "🔥 Xem top món", "❌ Hủy đặt hàng" };
                }
            }
            else
            {
                // No specific dish mentioned - show popular items
                response.Response = "🛒 **Đặt món:**\n\n" +
                    "Bạn muốn đặt món gì? Chọn từ danh sách hoặc gõ tên món:\n\n" +
                    "💡 *Ví dụ: \"Cho mình 2 phần cơm gà\" hoặc \"1 lẩu bò\"*";

                response.Suggestions = GetBestSellerSuggestions(6);
                response.QuickReplies = new List<string>
                {
                    "🔥 Top bán chạy",
                    "💰 Món dưới 100k",
                    "⭐ Món đặc biệt",
                    "❌ Không đặt nữa"
                };
            }

            session.Context["OrderContext"] = orderContext;
            return response;
        }

        /// <summary>
        /// Process ongoing order flow
        /// </summary>
        private ChatBotResponseModel ProcessOrderFlow(string message, IntentResult intent, ChatSession session, ChatbotOrderContext orderContext)
        {
            var response = new ChatBotResponseModel();
            message = message.ToLower();

            // Check for cancel commands
            if (intent.Intent == "CONFIRM_NO" || message.Contains("hủy") || message.Contains("không đặt") || message.Contains("thôi"))
            {
                session.Context.Remove("OrderContext");
                response.Response = "❌ Đã hủy đơn hàng.\n\nBạn cần mình hỗ trợ gì khác không?";
                response.QuickReplies = new List<string> { "🍜 Xem menu", "📅 Đặt bàn", "🔥 Top món" };
                return response;
            }

            switch (orderContext.State)
            {
                case "COLLECTING_ITEMS":
                    return ProcessCollectingItemsState(message, intent, session, orderContext);

                case "CONFIRM_ITEMS":
                    return ProcessConfirmItemsState(message, intent, session, orderContext);

                case "COLLECTING_INFO":
                    return ProcessCollectingInfoState(message, intent, session, orderContext);

                case "CONFIRM_ORDER":
                    return ProcessConfirmOrderState(message, intent, session, orderContext);

                default:
                    return StartOrderFlow(message, intent.Entities, session);
            }
        }

        /// <summary>
        /// Process collecting items state
        /// </summary>
        private ChatBotResponseModel ProcessCollectingItemsState(string message, IntentResult intent, ChatSession session, ChatbotOrderContext orderContext)
        {
            var response = new ChatBotResponseModel();

            // Check for "done/finish" commands
            if (message.Contains("xong") || message.Contains("đủ") || message.Contains("đặt đơn") || 
                message.Contains("hoàn tất") || message.Contains("thanh toán"))
            {
                if (!orderContext.Items.Any())
                {
                    response.Response = "🛒 Giỏ hàng đang trống! Bạn hãy chọn món trước nhé.";
                    response.Suggestions = GetBestSellerSuggestions(4);
                    response.QuickReplies = new List<string> { "🔥 Top bán chạy", "❌ Hủy" };
                    return response;
                }

                // Move to confirm items state
                orderContext.State = "CONFIRM_ITEMS";
                session.Context["OrderContext"] = orderContext;

                var orderSummary = GetOrderSummaryText(orderContext);
                response.Response = $"📋 **Xác nhận đơn hàng:**\n\n{orderSummary}\n\nĐơn hàng đúng chưa?";
                response.QuickReplies = new List<string>
                {
                    "✅ Đúng rồi, tiếp tục",
                    "✏️ Sửa đơn hàng",
                    "❌ Hủy đơn"
                };
                return response;
            }

            // Check for "view cart" command
            if (message.Contains("xem giỏ") || message.Contains("giỏ hàng"))
            {
                var orderSummary = GetOrderSummaryText(orderContext);
                if (orderContext.Items.Any())
                {
                    response.Response = $"🛒 **Giỏ hàng hiện tại:**\n\n{orderSummary}";
                }
                else
                {
                    response.Response = "🛒 Giỏ hàng đang trống!";
                }
                response.QuickReplies = new List<string>
                {
                    "➕ Thêm món",
                    "✅ Đặt đơn",
                    "❌ Hủy"
                };
                return response;
            }

            // Try to add item from message
            var dishName = ExtractDishName(message);
            var quantity = ExtractQuantity(message);
            if (quantity <= 0) quantity = 1;

            // Check if user clicked on a suggestion (by ID)
            var menuItemId = ExtractMenuItemId(message);
            if (menuItemId > 0)
            {
                var menuItem = _db.MenuItem.Find(menuItemId);
                if (menuItem != null && menuItem.IsAvailable)
                {
                    AddOrUpdateOrderItem(orderContext, menuItem, quantity);
                    session.Context["OrderContext"] = orderContext;

                    response.Response = $"✅ Đã thêm **{menuItem.Name}** x{quantity}\n\n" +
                        GetOrderSummaryText(orderContext) + "\n\nThêm món khác không?";
                    response.Suggestions = GetSuggestedItemsForOrder(orderContext);
                    response.QuickReplies = new List<string>
                    {
                        "✅ Xong, đặt đơn",
                        "➕ Thêm món khác",
                        "❌ Hủy đơn"
                    };
                    return response;
                }
            }

            // Search by name
            if (!string.IsNullOrEmpty(dishName))
            {
                var foundItems = SearchDishByName(dishName);

                if (foundItems.Count == 1)
                {
                    var item = foundItems.First();
                    AddOrUpdateOrderItem(orderContext, item, quantity);
                    session.Context["OrderContext"] = orderContext;

                    response.Response = $"✅ Đã thêm **{item.Name}** x{quantity}\n\n" +
                        GetOrderSummaryText(orderContext) + "\n\nThêm món khác không?";
                    response.Suggestions = GetSuggestedItemsForOrder(orderContext);
                    response.QuickReplies = new List<string>
                    {
                        "✅ Xong, đặt đơn",
                        "➕ Thêm món khác",
                        "❌ Hủy đơn"
                    };
                }
                else if (foundItems.Count > 1)
                {
                    response.Response = $"🔍 Tìm thấy **{foundItems.Count} món** phù hợp \"{dishName}\".\nChọn món bạn muốn:";
                    response.Suggestions = foundItems.Take(5).Select(m => new MenuSuggestionModel
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Price = m.Price,
                        ImageUrl = m.ImageUrl
                    }).ToList();
                    response.QuickReplies = new List<string> { "✅ Đặt đơn", "❌ Hủy" };
                }
                else
                {
                    response.Response = $"🤔 Không tìm thấy món \"{dishName}\".\nChọn từ các món gợi ý:";
                    response.Suggestions = GetBestSellerSuggestions(4);
                    response.QuickReplies = new List<string> { "🔥 Top món", "✅ Đặt đơn", "❌ Hủy" };
                }
            }
            else
            {
                response.Response = "Gõ tên món hoặc chọn từ danh sách bên dưới:";
                response.Suggestions = GetBestSellerSuggestions(6);
                response.QuickReplies = new List<string>
                {
                    "✅ Đặt đơn",
                    "👀 Xem giỏ hàng",
                    "❌ Hủy"
                };
            }

            return response;
        }

        /// <summary>
        /// Process confirm items state
        /// </summary>
        private ChatBotResponseModel ProcessConfirmItemsState(string message, IntentResult intent, ChatSession session, ChatbotOrderContext orderContext)
        {
            var response = new ChatBotResponseModel();

            if (intent.Intent == "CONFIRM_YES" || message.Contains("đúng") || message.Contains("tiếp") || message.Contains("ok"))
            {
                // Move to collecting customer info
                orderContext.State = "COLLECTING_INFO";
                session.Context["OrderContext"] = orderContext;

                response.Response = "📝 **Thông tin giao hàng:**\n\n" +
                    "Vui lòng cho mình biết:\n" +
                    "• **Họ tên** người nhận\n" +
                    "• **Số điện thoại**\n" +
                    "• **Địa chỉ** giao hàng\n\n" +
                    "💡 *Ví dụ: \"Nguyễn Văn A, 0912345678, 123 Đường ABC Quận 1\"*";
                response.QuickReplies = new List<string> { "❌ Hủy đơn" };
            }
            else if (message.Contains("sửa") || message.Contains("thay đổi"))
            {
                orderContext.State = "COLLECTING_ITEMS";
                session.Context["OrderContext"] = orderContext;

                response.Response = "✏️ **Sửa đơn hàng:**\n\n" + GetOrderSummaryText(orderContext) +
                    "\n\nGõ tên món để thêm hoặc \"xóa [tên món]\" để xóa.";
                response.Suggestions = GetBestSellerSuggestions(4);
                response.QuickReplies = new List<string> { "✅ Xong, đặt đơn", "❌ Hủy" };
            }
            else
            {
                response.Response = "Vui lòng chọn:\n• ✅ **Đúng rồi** - tiếp tục đặt hàng\n• ✏️ **Sửa đơn** - chỉnh sửa món";
                response.QuickReplies = new List<string> { "✅ Đúng rồi, tiếp tục", "✏️ Sửa đơn hàng", "❌ Hủy" };
            }

            return response;
        }

        /// <summary>
        /// Process collecting customer info state
        /// </summary>
        private ChatBotResponseModel ProcessCollectingInfoState(string message, IntentResult intent, ChatSession session, ChatbotOrderContext orderContext)
        {
            var response = new ChatBotResponseModel();

            // Try to extract customer info from message
            var (name, phone, address) = ExtractCustomerInfo(message);

            if (!string.IsNullOrEmpty(name)) orderContext.CustomerName = name;
            if (!string.IsNullOrEmpty(phone)) orderContext.CustomerPhone = phone;
            if (!string.IsNullOrEmpty(address)) orderContext.CustomerAddress = address;

            // Check if we have all required info
            if (!string.IsNullOrEmpty(orderContext.CustomerName) &&
                !string.IsNullOrEmpty(orderContext.CustomerPhone) &&
                !string.IsNullOrEmpty(orderContext.CustomerAddress))
            {
                // All info collected - confirm order
                orderContext.State = "CONFIRM_ORDER";
                session.Context["OrderContext"] = orderContext;

                var totalAmount = orderContext.Items.Sum(i => i.Price * i.Quantity);
                var deliveryFee = totalAmount >= 300000 ? 0 : 25000;

                response.Response = "📋 **Xác nhận đơn hàng cuối cùng:**\n\n" +
                    GetOrderSummaryText(orderContext) + "\n" +
                    $"🚚 Phí giao hàng: {deliveryFee:N0}đ\n" +
                    $"💰 **Tổng cộng: {totalAmount + deliveryFee:N0}đ**\n\n" +
                    "**Thông tin giao hàng:**\n" +
                    $"👤 {orderContext.CustomerName}\n" +
                    $"📞 {orderContext.CustomerPhone}\n" +
                    $"📍 {orderContext.CustomerAddress}\n\n" +
                    "✅ Xác nhận đặt hàng?";
                response.QuickReplies = new List<string>
                {
                    "✅ Xác nhận đặt hàng",
                    "✏️ Sửa thông tin",
                    "❌ Hủy đơn"
                };
            }
            else
            {
                // Ask for missing info
                var missing = new List<string>();
                if (string.IsNullOrEmpty(orderContext.CustomerName)) missing.Add("**Họ tên**");
                if (string.IsNullOrEmpty(orderContext.CustomerPhone)) missing.Add("**Số điện thoại**");
                if (string.IsNullOrEmpty(orderContext.CustomerAddress)) missing.Add("**Địa chỉ giao hàng**");

                session.Context["OrderContext"] = orderContext;

                response.Response = $"📝 Vui lòng cho mình biết thêm:\n{string.Join(", ", missing)}\n\n" +
                    "💡 *Ví dụ: \"Nguyễn Văn A, 0912345678, 123 Đường ABC Quận 1\"*";
                response.QuickReplies = new List<string> { "❌ Hủy đơn" };
            }

            return response;
        }

        /// <summary>
        /// Process confirm order state
        /// </summary>
        private ChatBotResponseModel ProcessConfirmOrderState(string message, IntentResult intent, ChatSession session, ChatbotOrderContext orderContext)
        {
            var response = new ChatBotResponseModel();

            if (intent.Intent == "CONFIRM_YES" || message.Contains("xác nhận") || message.Contains("đặt") || message.Contains("ok"))
            {
                // Create the order
                var result = CreateOrderFromChatbot(orderContext);

                if (result.Success)
                {
                    session.Context.Remove("OrderContext");

                    response.Response = $"🎉 **Đặt hàng thành công!**\n\n" +
                        $"📦 Mã đơn: **{result.OrderCode}**\n" +
                        $"💰 Tổng tiền: **{result.TotalAmount:N0}đ**\n" +
                        $"⏰ Dự kiến giao: **{result.EstimatedTime}**\n\n" +
                        "Cảm ơn bạn đã đặt hàng! 🙏\n" +
                        "Mình sẽ liên hệ xác nhận sớm nhất!";
                    response.QuickReplies = new List<string>
                    {
                        "📍 Theo dõi đơn hàng",
                        "🍜 Đặt thêm",
                        "👋 Tạm biệt"
                    };
                }
                else
                {
                    response.Response = $"❌ Không thể tạo đơn hàng: {result.Message}\n\nVui lòng thử lại hoặc gọi hotline 0123 456 789";
                    response.QuickReplies = new List<string> { "🔄 Thử lại", "📞 Gọi hotline", "❌ Hủy" };
                }
            }
            else if (message.Contains("sửa"))
            {
                orderContext.State = "COLLECTING_INFO";
                orderContext.CustomerName = null;
                orderContext.CustomerPhone = null;
                orderContext.CustomerAddress = null;
                session.Context["OrderContext"] = orderContext;

                response.Response = "✏️ Nhập lại thông tin giao hàng:\n\n" +
                    "💡 *Ví dụ: \"Nguyễn Văn A, 0912345678, 123 Đường ABC Quận 1\"*";
                response.QuickReplies = new List<string> { "❌ Hủy" };
            }
            else
            {
                response.Response = "Vui lòng xác nhận:\n• ✅ **Xác nhận** - hoàn tất đơn hàng\n• ✏️ **Sửa** - chỉnh sửa thông tin";
                response.QuickReplies = new List<string> { "✅ Xác nhận đặt hàng", "✏️ Sửa thông tin", "❌ Hủy" };
            }

            return response;
        }

        #endregion

        #region Reservation Flow Methods

        /// <summary>
        /// Start reservation flow
        /// </summary>
        private ChatBotResponseModel StartReservationFlow(string message, ExtractedEntities entities, ChatSession session)
        {
            var response = new ChatBotResponseModel();

            // Create reservation context
            var reservationContext = new ChatbotReservationContext
            {
                State = "COLLECTING_INFO",
                LastUpdated = DateTime.Now
            };

            // Try to extract info from message
            var (date, time) = ExtractDateTime(message);
            var guests = entities.PartySize ?? ExtractGuestCount(message);

            if (date.HasValue) reservationContext.ReservationDate = date;
            if (time.HasValue) reservationContext.ReservationTime = time;
            if (guests > 0) reservationContext.NumberOfGuests = guests;

            session.Context["ReservationContext"] = reservationContext;

            // Check what info we have
            var hasDate = reservationContext.ReservationDate.HasValue;
            var hasTime = reservationContext.ReservationTime.HasValue;
            var hasGuests = reservationContext.NumberOfGuests > 0;

            if (hasDate && hasTime && hasGuests)
            {
                // All info provided - check availability
                return CheckAvailabilityAndProceed(session, reservationContext);
            }

            // Ask for missing info
            var prompt = "📅 **Đặt bàn tại Nhà Hàng LDP:**\n\n";
            
            if (!hasGuests)
            {
                prompt += "👥 Bạn đi **mấy người**?\n\n";
                response.QuickReplies = new List<string> { "2 người", "4 người", "6 người", "8 người", "❌ Hủy" };
            }
            else if (!hasDate)
            {
                prompt += $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n\n";
                prompt += "📆 Bạn muốn đặt **ngày nào**?";
                response.QuickReplies = new List<string> { "Hôm nay", "Ngày mai", "Cuối tuần", "❌ Hủy" };
            }
            else if (!hasTime)
            {
                prompt += $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n";
                prompt += $"📆 Ngày: **{reservationContext.ReservationDate.Value:dd/MM/yyyy}**\n\n";
                prompt += "🕐 Bạn muốn đến lúc **mấy giờ**?";
                response.QuickReplies = new List<string> { "11:00", "12:00", "18:00", "19:00", "20:00", "❌ Hủy" };
            }

            response.Response = prompt;
            return response;
        }

        /// <summary>
        /// Process ongoing reservation flow
        /// </summary>
        private ChatBotResponseModel ProcessReservationFlow(string message, IntentResult intent, ChatSession session, ChatbotReservationContext reservationContext)
        {
            var response = new ChatBotResponseModel();
            message = message.ToLower();

            // Check for cancel
            if (intent.Intent == "CONFIRM_NO" || message.Contains("hủy") || message.Contains("thôi"))
            {
                session.Context.Remove("ReservationContext");
                response.Response = "❌ Đã hủy đặt bàn.\n\nBạn cần mình hỗ trợ gì khác không?";
                response.QuickReplies = new List<string> { "🍜 Xem menu", "🛒 Đặt món", "🔥 Top món" };
                return response;
            }

            switch (reservationContext.State)
            {
                case "COLLECTING_INFO":
                    return ProcessReservationCollectingInfo(message, intent, session, reservationContext);

                case "COLLECTING_CONTACT":
                    return ProcessReservationCollectingContact(message, intent, session, reservationContext);

                case "CONFIRM_BOOKING":
                    return ProcessReservationConfirm(message, intent, session, reservationContext);

                default:
                    return StartReservationFlow(message, intent.Entities, session);
            }
        }

        /// <summary>
        /// Process collecting reservation info
        /// </summary>
        private ChatBotResponseModel ProcessReservationCollectingInfo(string message, IntentResult intent, ChatSession session, ChatbotReservationContext reservationContext)
        {
            var response = new ChatBotResponseModel();

            // Extract info from message
            var guests = ExtractGuestCount(message);
            var (date, time) = ExtractDateTime(message);

            if (guests > 0) reservationContext.NumberOfGuests = guests;
            if (date.HasValue) reservationContext.ReservationDate = date;
            if (time.HasValue) reservationContext.ReservationTime = time;

            reservationContext.LastUpdated = DateTime.Now;
            session.Context["ReservationContext"] = reservationContext;

            // Check what info we have
            var hasDate = reservationContext.ReservationDate.HasValue;
            var hasTime = reservationContext.ReservationTime.HasValue;
            var hasGuests = reservationContext.NumberOfGuests > 0;

            if (hasDate && hasTime && hasGuests)
            {
                return CheckAvailabilityAndProceed(session, reservationContext);
            }

            // Ask for next missing info
            var prompt = "📅 **Đặt bàn:**\n";
            
            if (hasGuests) prompt += $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n";
            if (hasDate) prompt += $"📆 Ngày: **{reservationContext.ReservationDate.Value:dd/MM/yyyy}**\n";
            if (hasTime) prompt += $"🕐 Giờ: **{reservationContext.ReservationTime.Value:hh\\:mm}**\n";

            prompt += "\n";

            if (!hasGuests)
            {
                prompt += "👥 Bạn đi **mấy người**?";
                response.QuickReplies = new List<string> { "2 người", "4 người", "6 người", "8 người", "❌ Hủy" };
            }
            else if (!hasDate)
            {
                prompt += "📆 Bạn muốn đặt **ngày nào**?";
                response.QuickReplies = new List<string> { "Hôm nay", "Ngày mai", "Cuối tuần", "❌ Hủy" };
            }
            else if (!hasTime)
            {
                prompt += "🕐 Bạn muốn đến lúc **mấy giờ**?";
                response.QuickReplies = new List<string> { "11:00", "12:00", "18:00", "19:00", "20:00", "❌ Hủy" };
            }

            response.Response = prompt;
            return response;
        }

        /// <summary>
        /// Check availability and proceed to contact info
        /// </summary>
        private ChatBotResponseModel CheckAvailabilityAndProceed(ChatSession session, ChatbotReservationContext reservationContext)
        {
            var response = new ChatBotResponseModel();

            // Check table availability
            var availableTables = GetAvailableTableCount(
                reservationContext.ReservationDate.Value,
                reservationContext.ReservationTime.Value,
                reservationContext.NumberOfGuests
            );

            if (availableTables > 0)
            {
                reservationContext.State = "COLLECTING_CONTACT";
                session.Context["ReservationContext"] = reservationContext;

                response.Response = $"✅ **Có {availableTables} bàn trống!**\n\n" +
                    $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n" +
                    $"📆 Ngày: **{reservationContext.ReservationDate.Value:dd/MM/yyyy}**\n" +
                    $"🕐 Giờ: **{reservationContext.ReservationTime.Value:hh\\:mm}**\n\n" +
                    "📝 Cho mình **họ tên** và **số điện thoại** để hoàn tất đặt bàn:";
                response.QuickReplies = new List<string> { "❌ Hủy" };
            }
            else
            {
                response.Response = $"😔 Rất tiếc, không còn bàn trống cho **{reservationContext.NumberOfGuests} người** " +
                    $"vào **{reservationContext.ReservationDate.Value:dd/MM/yyyy}** lúc **{reservationContext.ReservationTime.Value:hh\\:mm}**.\n\n" +
                    "Bạn có muốn thử thời gian khác không?";
                
                reservationContext.ReservationDate = null;
                reservationContext.ReservationTime = null;
                session.Context["ReservationContext"] = reservationContext;
                
                response.QuickReplies = new List<string> { "🕐 Thử giờ khác", "📆 Thử ngày khác", "❌ Hủy" };
            }

            return response;
        }

        /// <summary>
        /// Process collecting contact info for reservation
        /// </summary>
        private ChatBotResponseModel ProcessReservationCollectingContact(string message, IntentResult intent, ChatSession session, ChatbotReservationContext reservationContext)
        {
            var response = new ChatBotResponseModel();

            var (name, phone, _) = ExtractCustomerInfo(message);

            if (!string.IsNullOrEmpty(name)) reservationContext.CustomerName = name;
            if (!string.IsNullOrEmpty(phone)) reservationContext.CustomerPhone = phone;

            // Check if email mentioned
            var emailMatch = Regex.Match(message, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            if (emailMatch.Success) reservationContext.CustomerEmail = emailMatch.Value;

            session.Context["ReservationContext"] = reservationContext;

            if (!string.IsNullOrEmpty(reservationContext.CustomerName) && !string.IsNullOrEmpty(reservationContext.CustomerPhone))
            {
                // All info collected - confirm
                reservationContext.State = "CONFIRM_BOOKING";
                session.Context["ReservationContext"] = reservationContext;

                response.Response = "📋 **Xác nhận đặt bàn:**\n\n" +
                    $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n" +
                    $"📆 Ngày: **{reservationContext.ReservationDate.Value:dd/MM/yyyy}**\n" +
                    $"🕐 Giờ: **{reservationContext.ReservationTime.Value:hh\\:mm}**\n\n" +
                    $"👤 Tên: **{reservationContext.CustomerName}**\n" +
                    $"📞 SĐT: **{reservationContext.CustomerPhone}**\n\n" +
                    "✅ Xác nhận đặt bàn?";
                response.QuickReplies = new List<string> { "✅ Xác nhận", "✏️ Sửa thông tin", "❌ Hủy" };
            }
            else
            {
                var missing = new List<string>();
                if (string.IsNullOrEmpty(reservationContext.CustomerName)) missing.Add("**Họ tên**");
                if (string.IsNullOrEmpty(reservationContext.CustomerPhone)) missing.Add("**Số điện thoại**");

                response.Response = $"📝 Vui lòng cho mình biết {string.Join(" và ", missing)}:";
                response.QuickReplies = new List<string> { "❌ Hủy" };
            }

            return response;
        }

        /// <summary>
        /// Process reservation confirmation
        /// </summary>
        private ChatBotResponseModel ProcessReservationConfirm(string message, IntentResult intent, ChatSession session, ChatbotReservationContext reservationContext)
        {
            var response = new ChatBotResponseModel();

            if (intent.Intent == "CONFIRM_YES" || message.Contains("xác nhận") || message.Contains("ok") || message.Contains("đúng"))
            {
                var result = CreateReservationFromChatbot(reservationContext);

                if (result.Success)
                {
                    session.Context.Remove("ReservationContext");

                    response.Response = $"🎉 **Đặt bàn thành công!**\n\n" +
                        $"📋 Mã đặt bàn: **{result.ReservationCode}**\n" +
                        $"👥 Số khách: **{reservationContext.NumberOfGuests} người**\n" +
                        $"📆 Ngày: **{reservationContext.ReservationDate.Value:dd/MM/yyyy}**\n" +
                        $"🕐 Giờ: **{reservationContext.ReservationTime.Value:hh\\:mm}**\n\n" +
                        "Cảm ơn bạn! Chúng tôi sẽ liên hệ xác nhận sớm nhất. 🙏";
                    response.QuickReplies = new List<string> { "🍜 Xem menu", "🛒 Đặt món trước", "👋 Tạm biệt" };
                }
                else
                {
                    response.Response = $"❌ Không thể đặt bàn: {result.Message}\n\nVui lòng thử lại hoặc gọi 0123 456 789";
                    response.QuickReplies = new List<string> { "🔄 Thử lại", "📞 Gọi hotline" };
                }
            }
            else if (message.Contains("sửa"))
            {
                reservationContext.State = "COLLECTING_INFO";
                reservationContext.ReservationDate = null;
                reservationContext.ReservationTime = null;
                reservationContext.NumberOfGuests = 0;
                session.Context["ReservationContext"] = reservationContext;

                response.Response = "✏️ Nhập lại thông tin đặt bàn.\n\n👥 Bạn đi **mấy người**?";
                response.QuickReplies = new List<string> { "2 người", "4 người", "6 người", "❌ Hủy" };
            }
            else
            {
                response.Response = "Vui lòng chọn:\n• ✅ **Xác nhận** - hoàn tất đặt bàn\n• ✏️ **Sửa** - nhập lại thông tin";
                response.QuickReplies = new List<string> { "✅ Xác nhận", "✏️ Sửa thông tin", "❌ Hủy" };
            }

            return response;
        }

        #endregion

        #region Helper Methods for Order/Reservation

        /// <summary>
        /// Extract dish name from message
        /// </summary>
        private string ExtractDishName(string message)
        {
            // Remove common phrases
            var cleaned = Regex.Replace(message.ToLower(), 
                @"(cho (mình|tôi|tui)|đặt|order|mua|gọi|lấy|thêm|\d+\s*(phần|suất|đĩa|tô|ly|chai|lon))",
                "").Trim();
            
            // Remove quantity patterns
            cleaned = Regex.Replace(cleaned, @"^\d+\s*", "").Trim();
            
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 2) return null;
            
            return cleaned;
        }

        /// <summary>
        /// Extract quantity from message
        /// </summary>
        private int ExtractQuantity(string message)
        {
            var match = Regex.Match(message, @"(\d+)\s*(phần|suất|đĩa|tô|ly|chai|lon|cái)?");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 1;
        }

        /// <summary>
        /// Extract menu item ID from message (when user clicks suggestion)
        /// </summary>
        private int ExtractMenuItemId(string message)
        {
            var match = Regex.Match(message, @"menu_item_(\d+)");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;
        }

        /// <summary>
        /// Search dish by name with fuzzy matching
        /// </summary>
        private List<MenuItem> SearchDishByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<MenuItem>();

            name = name.ToLower().Trim();

            // First try exact match
            var exactMatch = _db.MenuItem
                .Where(m => m.IsAvailable && m.Name.ToLower() == name)
                .ToList();

            if (exactMatch.Any()) return exactMatch;

            // Then try contains
            var containsMatch = _db.MenuItem
                .Where(m => m.IsAvailable && m.Name.ToLower().Contains(name))
                .OrderByDescending(m => m.SoldCount)
                .Take(5)
                .ToList();

            if (containsMatch.Any()) return containsMatch;

            // Fuzzy search - look for partial matches
            var allItems = _db.MenuItem.Where(m => m.IsAvailable).ToList();
            var fuzzyMatches = allItems
                .Where(m => {
                    var itemName = m.Name.ToLower();
                    var words = name.Split(' ');
                    return words.Any(w => w.Length > 2 && itemName.Contains(w));
                })
                .OrderByDescending(m => m.SoldCount)
                .Take(5)
                .ToList();

            return fuzzyMatches;
        }

        /// <summary>
        /// Add or update item in order
        /// </summary>
        private void AddOrUpdateOrderItem(ChatbotOrderContext orderContext, MenuItem menuItem, int quantity)
        {
            var existingItem = orderContext.Items.FirstOrDefault(i => i.MenuItemId == menuItem.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                orderContext.Items.Add(new ChatbotOrderItem
                {
                    MenuItemId = menuItem.Id,
                    Name = menuItem.Name,
                    Price = menuItem.Price,
                    Quantity = quantity,
                    ImageUrl = menuItem.ImageUrl
                });
            }
            orderContext.LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Get order summary text
        /// </summary>
        private string GetOrderSummaryText(ChatbotOrderContext orderContext)
        {
            if (!orderContext.Items.Any()) return "🛒 *Giỏ hàng trống*";

            var lines = orderContext.Items.Select(i => 
                $"• {i.Name} x{i.Quantity} - {i.Price * i.Quantity:N0}đ"
            );
            var total = orderContext.Items.Sum(i => i.Price * i.Quantity);

            return string.Join("\n", lines) + $"\n\n💵 **Tạm tính: {total:N0}đ**";
        }

        /// <summary>
        /// Get suggested items for order (excluding already ordered)
        /// </summary>
        private List<MenuSuggestionModel> GetSuggestedItemsForOrder(ChatbotOrderContext orderContext)
        {
            var orderedIds = orderContext.Items.Select(i => i.MenuItemId).ToList();

            return _db.MenuItem
                .Where(m => m.IsAvailable && !orderedIds.Contains(m.Id))
                .OrderByDescending(m => m.SoldCount)
                .Take(4)
                .Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl
                })
                .ToList();
        }

        /// <summary>
        /// Get best seller suggestions
        /// </summary>
        private List<MenuSuggestionModel> GetBestSellerSuggestions(int count)
        {
            return _db.MenuItem
                .Where(m => m.IsAvailable)
                .OrderByDescending(m => m.SoldCount)
                .Take(count)
                .Select(m => new MenuSuggestionModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                })
                .ToList();
        }

        /// <summary>
        /// Extract customer info (name, phone, address)
        /// </summary>
        private (string name, string phone, string address) ExtractCustomerInfo(string message)
        {
            string name = null;
            string phone = null;
            string address = null;

            // Extract phone
            var phoneMatch = Regex.Match(message, @"0\d{9,10}");
            if (phoneMatch.Success)
            {
                phone = phoneMatch.Value;
            }

            // Remove phone from message for easier name extraction
            var cleaned = phoneMatch.Success ? message.Replace(phoneMatch.Value, " ") : message;

            // Try to extract address (usually after "số", "đường", "quận", etc.)
            var addressMatch = Regex.Match(cleaned, @"(số\s*\d+|đường|quận|phường|tp|hẻm).+", RegexOptions.IgnoreCase);
            if (addressMatch.Success)
            {
                address = addressMatch.Value.Trim();
                cleaned = cleaned.Replace(addressMatch.Value, " ");
            }

            // Try comma-separated format
            var parts = cleaned.Split(new[] { ',', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count >= 1)
            {
                // First part might be name
                var firstPart = parts[0].Trim();
                if (firstPart.Length >= 2 && firstPart.Length <= 50 && !Regex.IsMatch(firstPart, @"^\d"))
                {
                    name = firstPart;
                }
            }

            if (parts.Count >= 3 && string.IsNullOrEmpty(address))
            {
                // Third part might be address
                address = parts[2].Trim();
            }

            return (name, phone, address);
        }

        /// <summary>
        /// Extract date and time from message
        /// </summary>
        private (DateTime? date, TimeSpan? time) ExtractDateTime(string message)
        {
            DateTime? date = null;
            TimeSpan? time = null;

            message = message.ToLower();

            // Extract date
            if (message.Contains("hôm nay") || message.Contains("today"))
            {
                date = DateTime.Today;
            }
            else if (message.Contains("ngày mai") || message.Contains("mai") || message.Contains("tomorrow"))
            {
                date = DateTime.Today.AddDays(1);
            }
            else if (message.Contains("cuối tuần") || message.Contains("weekend"))
            {
                var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7;
                if (daysUntilSaturday == 0) daysUntilSaturday = 7;
                date = DateTime.Today.AddDays(daysUntilSaturday);
            }
            else if (message.Contains("thứ 7") || message.Contains("thứ bảy"))
            {
                var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7;
                if (daysUntilSaturday == 0) daysUntilSaturday = 7;
                date = DateTime.Today.AddDays(daysUntilSaturday);
            }
            else if (message.Contains("chủ nhật") || message.Contains("cn"))
            {
                var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)DateTime.Today.DayOfWeek + 7) % 7;
                if (daysUntilSunday == 0) daysUntilSunday = 7;
                date = DateTime.Today.AddDays(daysUntilSunday);
            }
            else
            {
                // Try to parse date like "25/12" or "25-12"
                var dateMatch = Regex.Match(message, @"(\d{1,2})[/\-](\d{1,2})");
                if (dateMatch.Success)
                {
                    var day = int.Parse(dateMatch.Groups[1].Value);
                    var month = int.Parse(dateMatch.Groups[2].Value);
                    var year = DateTime.Now.Year;
                    if (month < DateTime.Now.Month) year++;
                    
                    try
                    {
                        date = new DateTime(year, month, day);
                    }
                    catch { }
                }
            }

            // Extract time
            var timeMatch = Regex.Match(message, @"(\d{1,2})[:\.]?(\d{2})?\s*(giờ|h|am|pm)?");
            if (timeMatch.Success)
            {
                var hour = int.Parse(timeMatch.Groups[1].Value);
                var minute = timeMatch.Groups[2].Success ? int.Parse(timeMatch.Groups[2].Value) : 0;

                // Adjust for common restaurant hours
                if (hour < 6) hour += 12; // Assume PM for small numbers

                if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                {
                    time = new TimeSpan(hour, minute, 0);
                }
            }
            else if (message.Contains("tối") || message.Contains("evening"))
            {
                time = new TimeSpan(19, 0, 0);
            }
            else if (message.Contains("trưa") || message.Contains("noon"))
            {
                time = new TimeSpan(12, 0, 0);
            }

            return (date, time);
        }

        /// <summary>
        /// Extract guest count from message
        /// </summary>
        private int ExtractGuestCount(string message)
        {
            var match = Regex.Match(message, @"(\d+)\s*(người|ng|khách)?");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return 0;
        }

        /// <summary>
        /// Get available table count
        /// </summary>
        private int GetAvailableTableCount(DateTime date, TimeSpan time, int guests)
        {
            try
            {
                var timeMinutes = (int)time.TotalMinutes;

                var sql = @"
                    SELECT COUNT(*)
                    FROM RestaurantTable t
                    WHERE t.Status IN ('Available', 'Occupied')
                      AND t.Capacity >= @p0
                      AND NOT EXISTS (
                          SELECT 1 FROM Reservation r 
                          WHERE r.TableId = t.Id 
                            AND r.ReservationDate = @p1
                            AND r.Status IN ('Pending', 'Confirmed')
                            AND ABS(DATEDIFF(MINUTE, '00:00:00', r.ReservationTime) - @p2) < 120
                      )";

                return _db.Database.SqlQuery<int>(sql, guests, date, timeMinutes).FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Create order from chatbot context
        /// </summary>
        private OrderCreationResult CreateOrderFromChatbot(ChatbotOrderContext orderContext)
        {
            try
            {
                var orderCode = "DH" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(100, 999);
                var subTotal = orderContext.Items.Sum(i => i.Price * i.Quantity);
                var deliveryFee = subTotal >= 300000 ? 0 : 25000;
                var totalAmount = subTotal + deliveryFee;

                var sql = @"
                    INSERT INTO CustomerOrder 
                    (OrderCode, CustomerName, CustomerPhone, OrderType, DeliveryAddress, 
                     SubTotal, DeliveryFee, Discount, TotalAmount, PaymentMethod, PaymentStatus, 
                     Status, Note, OrderDate, EstimatedDeliveryTime)
                    VALUES 
                    (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 0, @p7, 'COD', 'Pending', 'Pending', @p8, GETDATE(), DATEADD(HOUR, 1, GETDATE()));
                    SELECT SCOPE_IDENTITY();";

                var orderId = _db.Database.SqlQuery<decimal>(sql,
                    orderCode,
                    orderContext.CustomerName,
                    orderContext.CustomerPhone,
                    orderContext.OrderType,
                    orderContext.CustomerAddress,
                    subTotal,
                    deliveryFee,
                    totalAmount,
                    orderContext.Note ?? ""
                ).FirstOrDefault();

                var orderIdInt = (int)orderId;

                // Insert order details
                foreach (var item in orderContext.Items)
                {
                    _db.Database.ExecuteSqlCommand(
                        @"INSERT INTO CustomerOrderDetail (CustomerOrderId, MenuItemId, ItemName, Quantity, UnitPrice, Subtotal)
                          VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
                        orderIdInt, item.MenuItemId, item.Name, item.Quantity, item.Price, item.Price * item.Quantity);
                }

                return new OrderCreationResult
                {
                    Success = true,
                    OrderCode = orderCode,
                    TotalAmount = totalAmount,
                    EstimatedTime = "45-60 phút"
                };
            }
            catch (Exception ex)
            {
                return new OrderCreationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Create reservation from chatbot context
        /// </summary>
        private ReservationCreationResult CreateReservationFromChatbot(ChatbotReservationContext context)
        {
            try
            {
                var reservationCode = "RES" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999);

                // Find available table
                var timeMinutes = (int)context.ReservationTime.Value.TotalMinutes;
                var tableIdSql = @"
                    SELECT TOP 1 t.Id
                    FROM RestaurantTable t
                    WHERE t.Status IN ('Available')
                      AND t.Capacity >= @p0
                      AND NOT EXISTS (
                          SELECT 1 FROM Reservation r 
                          WHERE r.TableId = t.Id 
                            AND r.ReservationDate = @p1
                            AND r.Status IN ('Pending', 'Confirmed')
                            AND ABS(DATEDIFF(MINUTE, '00:00:00', r.ReservationTime) - @p2) < 120
                      )
                    ORDER BY t.Capacity";

                var tableId = _db.Database.SqlQuery<int?>(tableIdSql, 
                    context.NumberOfGuests, 
                    context.ReservationDate.Value, 
                    timeMinutes).FirstOrDefault();

                var sql = @"
                    INSERT INTO Reservation 
                    (ReservationCode, CustomerName, CustomerPhone, CustomerEmail, 
                     ReservationDate, ReservationTime, NumberOfGuests, TableId, Status, CreatedDate)
                    VALUES 
                    (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, 'Pending', GETDATE())";

                _db.Database.ExecuteSqlCommand(sql,
                    reservationCode,
                    context.CustomerName,
                    context.CustomerPhone,
                    context.CustomerEmail ?? "",
                    context.ReservationDate.Value,
                    context.ReservationTime.Value,
                    context.NumberOfGuests,
                    tableId
                );

                return new ReservationCreationResult
                {
                    Success = true,
                    ReservationCode = reservationCode
                };
            }
            catch (Exception ex)
            {
                return new ReservationCreationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        #endregion

        #region Result Classes

        private class OrderCreationResult
        {
            public bool Success { get; set; }
            public string OrderCode { get; set; }
            public decimal TotalAmount { get; set; }
            public string EstimatedTime { get; set; }
            public string Message { get; set; }
        }

        private class ReservationCreationResult
        {
            public bool Success { get; set; }
            public string ReservationCode { get; set; }
            public string Message { get; set; }
        }

        #endregion
    }
}
