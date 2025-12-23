using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using System.Text.RegularExpressions;

namespace NhaHangLDP.Controllers
{
    public class PublicController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        private AIChatbotService _chatbotService;

        public PublicController()
        {
            _chatbotService = new AIChatbotService();
        }
        
        // GET: Public
        public ActionResult Menu()
        {
            var menu = db.MenuItem
             .Where(m => m.IsAvailable == true)
             .ToList();
    
            // Load wishlist items for logged in customer
            var customerId = Session["CustomerId"] as int?;
            if (customerId.HasValue)
            {
                var wishlistIds = db.Database.SqlQuery<int>(
                    "SELECT MenuItemId FROM Wishlist WHERE CustomerId = @p0",
                    customerId.Value).ToList();
                ViewBag.WishlistIds = wishlistIds;
            }
            else
            {
                ViewBag.WishlistIds = new List<int>();
            }
    
            return View(menu);
        }

        // Hiển thị chi tiết món ăn
        public ActionResult Detail(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Vui lòng cung cấp ID món ăn.");
            }

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

        // ===== CHATBOT API - ENHANCED VERSION =====
        
        /// <summary>
        /// API endpoint chính cho chatbot (async với AI support)
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> ChatBotAI(string message, string sessionId = null)
        {
            try
            {
                var response = await _chatbotService.ProcessMessageAsync(message, sessionId);
                
                return Json(new
                {
                    success = response.Success,
                    response = response.Response,
                    suggestions = response.Suggestions,
                    quickReplies = response.QuickReplies,
                    intent = response.Intent,
                    confidence = response.Confidence,
                    sessionId = response.SessionId
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

        /// <summary>
        /// API endpoint cũ (backward compatible) - redirect to new endpoint
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> ChatBot(string message, string history = null)
        {
            return await ChatBotAI(message, null);
        }

        /// <summary>
        /// Lấy lịch sử chat
        /// </summary>
        [HttpGet]
        public JsonResult GetChatHistory(string sessionId)
        {
            try
            {
                var history = _chatbotService.GetChatHistory(sessionId);
                return Json(new { success = true, history = history }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Xóa session chat
        /// </summary>
        [HttpPost]
        public JsonResult ClearChatSession(string sessionId)
        {
            try
            {
                _chatbotService.ClearSession(sessionId);
                return Json(new { success = true, message = "Session đã được xóa" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API tìm kiếm món ăn nhanh
        /// </summary>
        [HttpGet]
        public JsonResult SearchMenu(string query, int limit = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Json(new { success = false, message = "Vui lòng nhập từ khóa" }, JsonRequestBehavior.AllowGet);
                }

                query = query.ToLower();

                var results = db.MenuItem
                    .Where(m => m.IsAvailable &&
                        (m.Name.ToLower().Contains(query) ||
                         m.Description.ToLower().Contains(query) ||
                         m.Category.ToLower().Contains(query)))
                    .OrderByDescending(m => m.SoldCount)
                    .Take(limit)
                    .Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        price = m.Price,
                        imageUrl = m.ImageUrl,
                        category = m.Category
                    })
                    .ToList();

                return Json(new { success = true, results = results }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// API lấy gợi ý nhanh (quick suggestions)
        /// </summary>
        [HttpGet]
        public JsonResult GetQuickSuggestions()
        {
            try
            {
                var hour = DateTime.Now.Hour;
                string mealType;

                if (hour >= 6 && hour < 10)
                    mealType = "Bữa sáng";
                else if (hour >= 10 && hour < 14)
                    mealType = "Bữa trưa";
                else if (hour >= 14 && hour < 18)
                    mealType = "Bữa chiều";
                else
                    mealType = "Bữa tối";

                var suggestions = new List<object>
                {
                    new { icon = "🔥", text = "Top món bán chạy", action = "top_sellers" },
                    new { icon = "💰", text = "Món dưới 100k", action = "budget" },
                    new { icon = "⭐", text = "Món đặc biệt", action = "featured" },
                    new { icon = "✨", text = "Món mới", action = "new" },
                    new { icon = "🍽️", text = $"Gợi ý {mealType}", action = "meal_suggestion" }
                };

                return Json(new { success = true, suggestions = suggestions, mealType = mealType }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ===== VIEW MODELS FOR CHATBOT (Legacy support) =====
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