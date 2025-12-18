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

            // Tạo ViewModel
            var viewModel = new MenuItemDetailViewModel
            {
                Id = menuItem.Id,
                Name = menuItem.Name,
                Description = menuItem.Description,
                Price = menuItem.Price,
                ImageUrl = menuItem.ImageUrl,
                Ingredients = ingredients
            };

            return View(viewModel);
        }

        // API Chatbot - Xử lý tin nhắn và gợi ý món ăn
        [HttpPost]
        public JsonResult ChatBot(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Json(new { success = false, message = "Vui lòng nhập tin nhắn!" });
                }

                message = message.ToLower().Trim();
                string response = "";
                List<object> suggestions = null;

                // Xử lý các câu hỏi về giá
                if (message.Contains("giá") || message.Contains("rẻ") || message.Contains("mắc") || 
                    message.Contains("bao nhiêu") || message.Contains("tiền"))
                {
                    var priceMatch = Regex.Match(message, @"(\d+)");
                    if (priceMatch.Success)
                    {
                        decimal targetPrice = decimal.Parse(priceMatch.Value) * 1000; // Giả sử nhập số nghìn
                        var items = db.MenuItem
                            .Where(m => m.IsAvailable && m.Price <= targetPrice)
                            .OrderBy(m => m.Price)
                            .Take(5)
                            .Select(m => new
                            {
                                id = m.Id,
                                name = m.Name,
                                price = m.Price,
                                description = m.Description,
                                imageUrl = m.ImageUrl
                            })
                            .ToList<object>();

                        response = string.Format("Dưới đây là các món ăn dưới {0:N0}đ:", targetPrice);
                        suggestions = items;
                    }
                    else if (message.Contains("rẻ"))
                    {
                        var items = db.MenuItem
                            .Where(m => m.IsAvailable)
                            .OrderBy(m => m.Price)
                            .Take(5)
                            .Select(m => new
                            {
                                id = m.Id,
                                name = m.Name,
                                price = m.Price,
                                description = m.Description,
                                imageUrl = m.ImageUrl
                            })
                            .ToList<object>();

                        response = "Dưới đây là các món ăn có giá rẻ nhất của chúng tôi:";
                        suggestions = items;
                    }
                    else
                    {
                        response = "Bạn có thể cho tôi biết mức giá bạn mong muốn không? Ví dụ: 'Món dưới 100 nghìn'";
                    }
                }
                // Xử lý câu hỏi về combo
                else if (message.Contains("combo") || message.Contains("set"))
                {
                    var combos = db.MenuCombo
                        .Where(c => c.IsActive)
                        .Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            price = c.ComboPrice,
                            description = c.Description,
                            isCombo = true
                        })
                        .ToList<object>();

                    if (combos.Any())
                    {
                        response = "Chúng tôi có các combo hấp dẫn sau:";
                        suggestions = combos;
                    }
                    else
                    {
                        response = "Hiện tại chúng tôi chưa có combo nào. Bạn có thể xem các món lẻ.";
                    }
                }
                // Xử lý câu hỏi về loại món
                else if (message.Contains("món chính") || message.Contains("chính"))
                {
                    suggestions = GetMenuByCategory("Món chính");
                    response = "Đây là các món chính của chúng tôi:";
                }
                else if (message.Contains("khai vị") || message.Contains("khai vi"))
                {
                    suggestions = GetMenuByCategory("Khai vị");
                    response = "Đây là các món khai vị của chúng tôi:";
                }
                else if (message.Contains("tráng miệng") || message.Contains("trang mieng") || message.Contains("ngọt"))
                {
                    suggestions = GetMenuByCategory("Tráng miệng");
                    response = "Đây là các món tráng miệng của chúng tôi:";
                }
                else if (message.Contains("nước") || message.Contains("uống") || message.Contains("giải khát"))
                {
                    suggestions = GetMenuByCategory("Đồ uống");
                    response = "Đây là các đồ uống của chúng tôi:";
                }
                // Xử lý tìm kiếm món cụ thể
                else if (message.Contains("tìm") || message.Contains("có") || message.Contains("món"))
                {
                    var searchTerms = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length > 2 && !new[] { "món", "tìm", "cho", "tôi", "mình", "có", "không" }.Contains(w))
                        .ToList();

                    if (searchTerms.Any())
                    {
                        var items = db.MenuItem
                            .Where(m => m.IsAvailable)
                            .ToList()
                            .Where(m => searchTerms.Any(term => 
                                RemoveDiacritics(m.Name.ToLower()).Contains(RemoveDiacritics(term)) ||
                                (m.Description != null && RemoveDiacritics(m.Description.ToLower()).Contains(RemoveDiacritics(term)))))
                            .Take(5)
                            .Select(m => new
                            {
                                id = m.Id,
                                name = m.Name,
                                price = m.Price,
                                description = m.Description,
                                imageUrl = m.ImageUrl
                            })
                            .ToList<object>();

                        if (items.Any())
                        {
                            response = string.Format("Tôi tìm thấy các món liên quan đến '{0}':", string.Join(", ", searchTerms));
                            suggestions = items;
                        }
                        else
                        {
                            response = "Xin lỗi, tôi không tìm thấy món nào phù hợp. Bạn có thể thử từ khóa khác!";
                        }
                    }
                }
                // Xử lý chào hỏi
                else if (message.Contains("chào") || message.Contains("hello") || message.Contains("hi") || message.Contains("xin chào"))
                {
                    response = "Xin chào! Tôi là trợ lý ảo của Nhà Hàng LDP. Tôi có thể giúp bạn:\n" +
                               "- Tìm món theo giá (VD: 'Món dưới 100 nghìn')\n" +
                               "- Xem combo (VD: 'Có combo nào không?')\n" +
                               "- Gợi ý món theo loại (VD: 'Món chính', 'Khai vị')\n" +
                               "- Tìm món cụ thể (VD: 'Có món bò không?')\n" +
                               "Bạn cần tôi giúp gì?";
                }
                // Xử lý gợi ý ngẫu nhiên
                else if (message.Contains("gợi ý") || message.Contains("đề xuất") || message.Contains("recommend"))
                {
                    var random = new Random();
                    var items = db.MenuItem
                        .Where(m => m.IsAvailable)
                        .ToList()
                        .OrderBy(x => random.Next())
                        .Take(5)
                        .Select(m => new
                        {
                            id = m.Id,
                            name = m.Name,
                            price = m.Price,
                            description = m.Description,
                            imageUrl = m.ImageUrl
                        })
                        .ToList<object>();

                    response = "Dưới đây là một số món ăn được nhiều khách hàng yêu thích:";
                    suggestions = items;
                }
                // Mặc định
                else
                {
                    response = "Xin lỗi, tôi chưa hiểu câu hỏi của bạn. Bạn có thể hỏi tôi về:\n" +
                               "- Giá món ăn\n" +
                               "- Combo/Set\n" +
                               "- Loại món (món chính, khai vị, tráng miệng, đồ uống)\n" +
                               "- Tìm món cụ thể\n" +
                               "Hoặc gõ 'gợi ý' để xem món ngẫu nhiên!";
                }

                return Json(new
                {
                    success = true,
                    response = response,
                    suggestions = suggestions
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Helper method để lấy món theo category
        private List<object> GetMenuByCategory(string category)
        {
            return db.MenuItem
                .Where(m => m.IsAvailable && m.Category == category)
                .OrderBy(m => m.Price)
                .Take(5)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    price = m.Price,
                    description = m.Description,
                    imageUrl = m.ImageUrl
                })
                .ToList<object>();
        }

        // Helper method để loại bỏ dấu tiếng Việt
        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
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
}