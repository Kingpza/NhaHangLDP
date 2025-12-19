using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NhaHangLDP.Models;
using System.Text.RegularExpressions;

namespace NhaHangLDP.Controllers
{
    public class PublicController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        
        // GET: Public
        public ActionResult Menu()
        {
            var menu = db.MenuItem
             .Where(m => m.IsAvailable == true)
             .ToList();
            return View(menu);
        }

        // Hiển thị chi tiết món ăn
        public ActionResult Detail(int? id)
        {
            if (id == null)
            {
                // Tùy chọn 1: Trả về lỗi 400 Bad Request
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Vui lòng cung cấp ID món ăn.");

                // Tùy chọn 2: Hoặc chuyển hướng về trang Menu
                // return RedirectToAction("Menu");
            }

            // SỬA 3: Dùng "id.Value" vì 'id' bây giờ là kiểu nullable
            var menuItem = db.MenuItem.Find(id.Value);

            if (menuItem == null)
            {
                return HttpNotFound();
            }

            // Truy vấn nguyên liệu
            var ingredients = db.MenuItemIngredient
                                .Include(mi => mi.Ingredient)
                                .Where(mi => mi.MenuItemId == id.Value)
                                .Select(mi => new MenuItemIngredientViewModel
                                {
                                    IngredientId = mi.IngredientId,
                                    IngredientName = mi.Ingredient.Name,
                                    Unit = mi.Ingredient.Unit,
                                    RequiredQuantity = mi.RequiredQuantity
                                })
                                .ToList();

            // Lấy món ăn liên quan (cùng category, khác ID, lấy 4 món)
            var relatedItems = db.MenuItem
                                .Where(m => m.IsAvailable && 
                                           m.Category == menuItem.Category && 
                                           m.Id != id.Value)
                                .OrderByDescending(m => m.SoldCount)
                                .ThenByDescending(m => m.Rating)
                                .Take(4)
                                .ToList();

            // Tạo ViewModel
            var viewModel = new MenuItemDetailViewModel
            {
                Id = menuItem.Id,
                Name = menuItem.Name,
                Description = menuItem.Description,
                Price = menuItem.Price,
                ImageUrl = menuItem.ImageUrl,
                Ingredients = ingredients,
                RelatedItems = relatedItems
            };

            return View(viewModel);
        }

        // ===== CHATBOT API =====
        [HttpPost]
        public JsonResult ChatBot(string message, string history = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Json(new
                    {
                        success = false,
                        response = "Vui lòng nhập tin nhắn!"
                    });
                }

                message = message.Trim().ToLower();

                // Xử lý tin nhắn và tạo response
                var chatResponse = ProcessChatMessage(message);

                return Json(new
                {
                    success = true,
                    response = chatResponse.Response,
                    suggestions = chatResponse.Suggestions,
                    quickReplies = chatResponse.QuickReplies
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChatBot Error: {ex.Message}");
                return Json(new
                {
                    success = false,
                    response = "Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại!"
                });
            }
        }

        private ChatBotResponse ProcessChatMessage(string message)
        {
            var response = new ChatBotResponse
            {
                Response = "",
                Suggestions = new List<MenuSuggestion>(),
                QuickReplies = new List<string>()
            };

            // 1. Chào hỏi
            if (IsGreeting(message))
            {
                response.Response = "👋 Xin chào! Mình là **LDP Bot**.\n\n" +
                    "Mình có thể giúp bạn:\n" +
                    "🍽️ Tìm món ăn\n" +
                    "💰 Gợi ý theo ngân sách\n" +
                    "⭐ Xem món bán chạy\n" +
                    "🎯 Tư vấn combo\n\n" +
                    "Bạn muốn tìm món gì?";
                response.QuickReplies = new List<string>
                {
                    "🔥 Top món bán chạy",
                    "💰 Món dưới 100k",
                    "🍜 Món chính",
                    "🥗 Món khai vị"
                };
                return response;
            }

            // 2. Top món bán chạy
            if (IsAskingBestSellers(message))
            {
                var topItems = db.MenuItem
                    .Where(m => m.IsAvailable)
                    .OrderByDescending(m => m.SoldCount)
                    .Take(5)
                    .ToList();

                response.Response = $"🔥 **Top {topItems.Count} món bán chạy nhất:**\n\n" +
                    "Đây là những món được khách hàng yêu thích nhất tại nhà hàng!";
                response.Suggestions = topItems.Select(m => new MenuSuggestion
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                }).ToList();
                response.QuickReplies = new List<string>
                {
                    "💰 Món giá rẻ",
                    "⭐ Món mới",
                    "🍜 Món chính"
                };
                return response;
            }

            // 3. Tìm món theo ngân sách
            var budgetMatch = Regex.Match(message, @"(dưới|dưới|under|<|duoi)\s*(\d+)");
            if (budgetMatch.Success || message.Contains("giá rẻ") || message.Contains("gia re") || message.Contains("rẻ"))
            {
                decimal maxPrice = 100000; // Mặc định 100k
                if (budgetMatch.Success)
                {
                    maxPrice = decimal.Parse(budgetMatch.Groups[2].Value) * 1000;
                }

                var affordableItems = db.MenuItem
                    .Where(m => m.IsAvailable && m.Price <= maxPrice)
                    .OrderBy(m => m.Price)
                    .Take(6)
                    .ToList();

                response.Response = $"💰 **Món ăn dưới {maxPrice / 1000}k:**\n\n" +
                    $"Mình tìm thấy **{affordableItems.Count} món** phù hợp với ngân sách của bạn!";
                response.Suggestions = affordableItems.Select(m => new MenuSuggestion
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                }).ToList();
                response.QuickReplies = new List<string>
                {
                    "🔥 Top món bán chạy",
                    "💰 Món dưới 200k",
                    "⭐ Món đặc biệt"
                };
                return response;
            }

            // 4. Tìm món theo danh mục
            var category = DetectCategory(message);
            if (!string.IsNullOrEmpty(category))
            {
                var categoryItems = db.MenuItem
                    .Where(m => m.IsAvailable && m.Category == category)
                    .OrderByDescending(m => m.Rating)
                    .ThenByDescending(m => m.SoldCount)
                    .Take(6)
                    .ToList();

                var categoryEmoji = GetCategoryEmoji(category);
                response.Response = $"{categoryEmoji} **{category}:**\n\n" +
                    $"Mình tìm thấy **{categoryItems.Count} món** trong danh mục này!";
                response.Suggestions = categoryItems.Select(m => new MenuSuggestion
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                }).ToList();
                response.QuickReplies = GetOtherCategories(category);
                return response;
            }

            // 5. Món mới
            if (message.Contains("mới") || message.Contains("new") || message.Contains("moi"))
            {
                var newItems = db.MenuItem
                    .Where(m => m.IsAvailable && m.IsNew)
                    .OrderByDescending(m => m.CreatedDate)
                    .Take(5)
                    .ToList();

                response.Response = $"✨ **Món mới ra mắt:**\n\n" +
                    $"Nhà hàng vừa ra mắt **{newItems.Count} món mới** đặc sắc!";
                response.Suggestions = newItems.Select(m => new MenuSuggestion
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                }).ToList();
                response.QuickReplies = new List<string>
                {
                    "🔥 Món bán chạy",
                    "⭐ Món đặc biệt",
                    "💰 Món giá rẻ"
                };
                return response;
            }

            // 6. Món đặc biệt / Featured
            if (message.Contains("đặc biệt") || message.Contains("dac biet") || message.Contains("featured") || message.Contains("nổi bật"))
            {
                var featuredItems = db.MenuItem
                    .Where(m => m.IsAvailable && m.IsFeatured)
                    .OrderByDescending(m => m.Rating)
                    .Take(5)
                    .ToList();

                response.Response = $"⭐ **Món đặc biệt hôm nay:**\n\n" +
                    $"Đầu bếp đề xuất **{featuredItems.Count} món** đặc sắc!";
                response.Suggestions = featuredItems.Select(m => new MenuSuggestion
                {
                    Id = m.Id,
                    Name = m.Name,
                    Price = m.Price,
                    ImageUrl = m.ImageUrl,
                    Description = m.Description
                }).ToList();
                response.QuickReplies = new List<string>
                {
                    "🔥 Món bán chạy",
                    "💰 Món giá rẻ",
                    "🍜 Món chính"
                };
                return response;
            }

            // 7. Tìm kiếm món ăn theo tên
            if (message.Length > 2)
            {
                var searchResults = db.MenuItem
                    .Where(m => m.IsAvailable && 
                        (m.Name.ToLower().Contains(message) || 
                         m.Description.ToLower().Contains(message)))
                    .OrderByDescending(m => m.SoldCount)
                    .Take(5)
                    .ToList();

                if (searchResults.Any())
                {
                    response.Response = $"🔍 **Kết quả tìm kiếm \"{message}\":**\n\n" +
                        $"Mình tìm thấy **{searchResults.Count} món** phù hợp!";
                    response.Suggestions = searchResults.Select(m => new MenuSuggestion
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Price = m.Price,
                        ImageUrl = m.ImageUrl,
                        Description = m.Description
                    }).ToList();
                    response.QuickReplies = new List<string>
                    {
                        "🔥 Món bán chạy",
                        "💰 Món giá rẻ",
                        "⭐ Món đặc biệt"
                    };
                    return response;
                }
            }

            // 8. Mặc định - không hiểu câu hỏi
            response.Response = "🤔 Mình chưa hiểu rõ câu hỏi của bạn.\n\n" +
                "Bạn có thể hỏi mình về:\n" +
                "• Món ăn theo danh mục\n" +
                "• Món theo ngân sách\n" +
                "• Top món bán chạy\n" +
                "• Món mới, món đặc biệt\n\n" +
                "Hoặc thử các gợi ý bên dưới nhé! 😊";
            response.QuickReplies = new List<string>
            {
                "🔥 Top món bán chạy",
                "💰 Món dưới 100k",
                "⭐ Món đặc biệt",
                "✨ Món mới"
            };

            return response;
        }

        // Helper methods
        private bool IsGreeting(string message)
        {
            string[] greetings = { "hi", "hello", "chào", "chao", "xin chào", "xin chao", "hey", "alo" };
            return greetings.Any(g => message.Contains(g));
        }

        private bool IsAskingBestSellers(string message)
        {
            return message.Contains("bán chạy") || message.Contains("ban chay") || 
                   message.Contains("best") || message.Contains("top") || 
                   message.Contains("nổi tiếng") || message.Contains("noi tieng") ||
                   message.Contains("phổ biến") || message.Contains("pho bien");
        }

        private string DetectCategory(string message)
        {
            if (message.Contains("khai vị") || message.Contains("khai vi") || message.Contains("appetizer"))
                return "Món Khai Vị";
            if (message.Contains("món chính") || message.Contains("mon chinh") || message.Contains("main") || message.Contains("chính"))
                return "Món Chính";
            if (message.Contains("tráng miệng") || message.Contains("trang mieng") || message.Contains("dessert") || message.Contains("ngọt"))
                return "Tráng Miệng";
            if (message.Contains("đồ uống") || message.Contains("do uong") || message.Contains("nước") || message.Contains("nuoc") || message.Contains("drink"))
                return "Đồ Uống";
            return null;
        }

        private string GetCategoryEmoji(string category)
        {
            switch (category)
            {
                case "Món Khai Vị": return "🥗";
                case "Món Chính": return "🍜";
                case "Tráng Miệng": return "🍰";
                case "Đồ Uống": return "🥤";
                default: return "🍽️";
            }
        }

        private List<string> GetOtherCategories(string currentCategory)
        {
            var allCategories = new List<string>
            {
                "🥗 Món khai vị",
                "🍜 Món chính",
                "🍰 Tráng miệng",
                "🥤 Đồ uống"
            };
            var categoryMap = new Dictionary<string, string>
            {
                { "Món Khai Vị", "🥗 Món khai vị" },
                { "Món Chính", "🍜 Món chính" },
                { "Tráng Miệng", "🍰 Tráng miệng" },
                { "Đồ Uống", "🥤 Đồ uống" }
            };

            string currentMapped = categoryMap.ContainsKey(currentCategory) ? categoryMap[currentCategory] : "";
            return allCategories.Where(c => c != currentMapped).ToList();
        }
    }

    // ===== VIEW MODELS FOR CHATBOT =====
    public class ChatBotResponse
    {
        public string Response { get; set; }
        public List<MenuSuggestion> Suggestions { get; set; }
        public List<string> QuickReplies { get; set; }
    }

    public class MenuSuggestion
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
    }
}