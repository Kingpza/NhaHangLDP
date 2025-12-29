using NhaHangLDP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý giỏ hàng
    /// </summary>
    public class CartController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();

        #region Session Keys
        private const string CART_SESSION_KEY = "CustomerCart";
        #endregion

        #region Cart Page

        /// <summary>
        /// Trang giỏ hàng
        /// </summary>
        public ActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }

        #endregion

        #region Cart Operations (AJAX)

        /// <summary>
        /// Thêm món vào giỏ hàng
        /// </summary>
        [HttpPost]
        public JsonResult AddToCart(int menuItemId, int quantity = 1, string specialInstructions = null)
        {
            try
            {
                var menuItem = _db.MenuItem.Find(menuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    return Json(new { success = false, message = "Món ăn không tồn tại hoặc đã hết!" });
                }

                var cart = GetCart();
                var existingItem = cart.Items.FirstOrDefault(i => i.MenuItemId == menuItemId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    existingItem.Subtotal = existingItem.UnitPrice * existingItem.Quantity;
                }
                else
                {
                    cart.Items.Add(new CartItemViewModel
                    {
                        Id = cart.Items.Count + 1,
                        MenuItemId = menuItemId,
                        Name = menuItem.Name,
                        ImageUrl = menuItem.ImageUrl,
                        Category = menuItem.Category,
                        UnitPrice = menuItem.Price,
                        Quantity = quantity,
                        Subtotal = menuItem.Price * quantity,
                        SpecialInstructions = specialInstructions
                    });
                }

                UpdateCartTotals(cart);
                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {menuItem.Name} vào giỏ hàng!",
                    cartCount = cart.TotalItems,
                    cartTotal = cart.TotalAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật số lượng
        /// </summary>
        [HttpPost]
        public JsonResult UpdateQuantity(int itemId, int quantity)
        {
            try
            {
                var cart = GetCart();
                var item = cart.Items.FirstOrDefault(i => i.Id == itemId);

                if (item == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy món trong giỏ!" });
                }

                if (quantity <= 0)
                {
                    cart.Items.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                    item.Subtotal = item.UnitPrice * quantity;
                }

                UpdateCartTotals(cart);
                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    cart = cart,
                    itemSubtotal = item?.Subtotal ?? 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa món khỏi giỏ
        /// </summary>
        [HttpPost]
        public JsonResult RemoveItem(int itemId)
        {
            try
            {
                var cart = GetCart();
                var item = cart.Items.FirstOrDefault(i => i.Id == itemId);

                if (item != null)
                {
                    cart.Items.Remove(item);
                    UpdateCartTotals(cart);
                    SaveCart(cart);
                }

                return Json(new
                {
                    success = true,
                    message = "Đã xóa món khỏi giỏ hàng!",
                    cart = cart
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng
        /// </summary>
        [HttpPost]
        public JsonResult ClearCart()
        {
            try
            {
                Session[CART_SESSION_KEY] = null;
                return Json(new { success = true, message = "Đã xóa giỏ hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin giỏ hàng (AJAX)
        /// </summary>
        [HttpGet]
        public JsonResult GetCartData()
        {
            var cart = GetCart();
            return Json(new { success = true, cart = cart }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Lấy số lượng món trong giỏ
        /// </summary>
        [HttpGet]
        public JsonResult GetCartCount()
        {
            var cart = GetCart();
            return Json(new { count = cart.TotalItems }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Áp dụng voucher
        /// </summary>
        [HttpPost]
        public JsonResult ApplyVoucher(string voucherCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(voucherCode))
                {
                    return Json(new { success = false, message = "Vui lòng nhập mã giảm giá!" });
                }

                var cart = GetCart();
                
                // Tìm voucher trong database
                var voucher = _db.Database.SqlQuery<VoucherInfo>(
                    "SELECT * FROM Voucher WHERE Code = @p0 AND IsActive = 1 AND StartDate <= GETDATE() AND EndDate >= GETDATE()",
                    voucherCode.Trim().ToUpper()).FirstOrDefault();

                if (voucher == null)
                {
                    return Json(new { success = false, message = "Mã giảm giá không hợp lệ hoặc đã hết hạn!" });
                }

                // Kiểm tra điều kiện
                if (voucher.MinOrderAmount.HasValue && cart.SubTotal < voucher.MinOrderAmount.Value)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Đơn hàng tối thiểu {voucher.MinOrderAmount:N0}đ để sử dụng mã này!" 
                    });
                }

                if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
                {
                    return Json(new { success = false, message = "Mã giảm giá đã hết lượt sử dụng!" });
                }

                // Tính discount
                decimal discount = 0;
                if (voucher.DiscountType == "Percentage")
                {
                    discount = cart.SubTotal * voucher.DiscountValue / 100;
                    if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
                    {
                        discount = voucher.MaxDiscountAmount.Value;
                    }
                }
                else
                {
                    discount = voucher.DiscountValue;
                }

                cart.Discount = discount;
                cart.VoucherCode = voucherCode.Trim().ToUpper();
                UpdateCartTotals(cart);
                SaveCart(cart);

                return Json(new
                {
                    success = true,
                    message = $"Áp dụng mã {voucherCode} thành công! Giảm {discount:N0}đ",
                    discount = discount,
                    cart = cart
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa voucher
        /// </summary>
        [HttpPost]
        public JsonResult RemoveVoucher()
        {
            var cart = GetCart();
            cart.Discount = 0;
            cart.VoucherCode = null;
            UpdateCartTotals(cart);
            SaveCart(cart);

            return Json(new { success = true, cart = cart });
        }

        #endregion

        #region Mini Cart (Partial View)

        /// <summary>
        /// Render mini cart dropdown
        /// </summary>
        public PartialViewResult MiniCart()
        {
            var cart = GetCart();
            return PartialView("_MiniCart", cart);
        }

        #endregion

        #region Suggested Items

        /// <summary>
        /// Lấy danh sách món gợi ý
        /// </summary>
        [HttpGet]
        public JsonResult GetSuggestedItems()
        {
            try
            {
                var cart = GetCart();
                var cartMenuItemIds = cart.Items.Select(i => i.MenuItemId).ToList();
                var cartCategories = cart.Items.Select(i => i.Category).Distinct().ToList();

                List<MenuItem> suggestedItems;

                if (cartCategories.Any())
                {
                    // Gợi ý các món cùng danh mục nhưng chưa có trong giỏ
                    suggestedItems = _db.MenuItem
                        .Where(m => m.IsAvailable && 
                                    cartCategories.Contains(m.Category) && 
                                    !cartMenuItemIds.Contains(m.Id))
                        .OrderByDescending(m => m.SoldCount)
                        .Take(4)
                        .ToList();

                    // Nếu không đủ 4 món, bổ sung từ các món bán chạy khác
                    if (suggestedItems.Count < 4)
                    {
                        var existingIds = suggestedItems.Select(s => s.Id).ToList();
                        existingIds.AddRange(cartMenuItemIds);

                        var additionalItems = _db.MenuItem
                            .Where(m => m.IsAvailable && !existingIds.Contains(m.Id))
                            .OrderByDescending(m => m.SoldCount)
                            .Take(4 - suggestedItems.Count)
                            .ToList();

                        suggestedItems.AddRange(additionalItems);
                    }
                }
                else
                {
                    // Nếu giỏ hàng trống hoặc không có danh mục, lấy các món bán chạy
                    suggestedItems = _db.MenuItem
                        .Where(m => m.IsAvailable)
                        .OrderByDescending(m => m.SoldCount)
                        .Take(4)
                        .ToList();
                }

                var result = suggestedItems.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    price = m.Price,
                    imageUrl = m.ImageUrl,
                    category = m.Category
                }).ToList();

                return Json(new { success = true, items = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Lấy giỏ hàng từ Session
        /// </summary>
        private CartViewModel GetCart()
        {
            var cart = Session[CART_SESSION_KEY] as CartViewModel;
            if (cart == null)
            {
                cart = new CartViewModel
                {
                    Items = new List<CartItemViewModel>(),
                    SubTotal = 0,
                    DeliveryFee = 0,
                    Discount = 0,
                    TotalAmount = 0,
                    TotalItems = 0
                };
            }
            return cart;
        }

        /// <summary>
        /// Lưu giỏ hàng vào Session
        /// </summary>
        private void SaveCart(CartViewModel cart)
        {
            Session[CART_SESSION_KEY] = cart;
        }

        /// <summary>
        /// Cập nhật tổng tiền
        /// </summary>
        private void UpdateCartTotals(CartViewModel cart)
        {
            cart.SubTotal = cart.Items.Sum(i => i.Subtotal);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);
            
            // Phí ship (miễn phí đơn từ 300k)
            cart.DeliveryFee = cart.SubTotal >= 300000 ? 0 : 25000;
            
            cart.TotalAmount = cart.SubTotal + cart.DeliveryFee - cart.Discount;
            if (cart.TotalAmount < 0) cart.TotalAmount = 0;
        }

        #endregion

        #region Helper Classes
        
        private class VoucherInfo
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string DiscountType { get; set; }
            public decimal DiscountValue { get; set; }
            public decimal? MaxDiscountAmount { get; set; }
            public decimal? MinOrderAmount { get; set; }
            public int? UsageLimit { get; set; }
            public int UsedCount { get; set; }
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
}
