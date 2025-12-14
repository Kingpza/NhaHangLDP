using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NhaHangLDP.Models;

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
    }
}