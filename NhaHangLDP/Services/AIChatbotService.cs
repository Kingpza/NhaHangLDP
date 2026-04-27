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
            { "ORDER_STATUS", new List<string> { "đơn hàng", "order", "trạng thái", "bao lâu", "khi nào" } }
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
                Answer = "📍 **Địa chỉ:**\nNhà hàng Hỷ Lạc Hotpot\n123 Đường ABC, Quận XYZ\nTP. Hồ Chí Minh\n\n📞 Hotline: 0123 456 789",
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
Bạn là trợ lý ảo của Nhà hàng Hỷ Lạc Hotpot, tên là LDP Bot.
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
                "💰 Gợi ý theo ngân sách\n" +
                "⭐ Xem món bán chạy\n" +
                "📅 Hỗ trợ đặt bàn\n\n" +
                "Bạn muốn tìm gì hôm nay?";
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
                Response = "📅 **Đặt bàn tại Nhà hàng Hỷ Lạc Hotpot:**\n\n" +
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
                response.Response = "📅 **Đặt bàn tại Nhà hàng Hỷ Lạc Hotpot:**\n\n" +
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
    }
}
