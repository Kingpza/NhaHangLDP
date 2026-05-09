using NhaHangLDP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
            // Tính lại phí giao hàng theo cài đặt mới nhất
            if (cart != null && cart.Items != null && cart.Items.Any())
            {
                UpdateCartTotals(cart);
                SaveCart(cart);
            }
            ViewBag.FreeDeliveryMinOrder = GetFreeDeliveryMinOrder();

            // Kiểm tra user có địa chỉ đã lưu không
            var customerId = GetCustomerId();
            ViewBag.HasAddress = false;
            if (customerId.HasValue)
            {
                var addressCount = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM CustomerAddress WHERE CustomerId = @p0", customerId.Value
                ).FirstOrDefault();
                ViewBag.HasAddress = addressCount > 0;
            }

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
                // Xóa giỏ hàng trong session
                Session[CART_SESSION_KEY] = null;

                // Nếu đã đăng nhập, xóa giỏ hàng trong database
                var customerId = GetCustomerId();
                if (customerId.HasValue)
                {
                    ClearDatabaseCart(customerId.Value);
                }

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
        /// Lấy CustomerId từ Session
        /// </summary>
        private int? GetCustomerId()
        {
            return Session["CustomerId"] as int?;
        }

        /// <summary>
        /// Lấy giỏ hàng - ưu tiên từ database cho người dùng đã đăng nhập
        /// </summary>
        private CartViewModel GetCart()
        {
            var customerId = GetCustomerId();

            // Nếu đã đăng nhập, lấy giỏ hàng từ database
            if (customerId.HasValue)
            {
                var sessionCart = Session[CART_SESSION_KEY] as CartViewModel;
                var dbCart = LoadCartFromDatabase(customerId.Value);

                // Nếu có giỏ hàng trong session nhưng chưa sync với database
                if (sessionCart != null && sessionCart.Items.Any())
                {
                    // Merge session cart vào database cart
                    dbCart = MergeSessionCartToDatabase(customerId.Value, sessionCart, dbCart);
                    // Xóa session cart sau khi đã merge
                    Session[CART_SESSION_KEY] = null;
                }

                return dbCart;
            }

            // Nếu chưa đăng nhập, lấy từ Session
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
        /// Lưu giỏ hàng - vào database cho người đã đăng nhập, vào Session cho khách
        /// </summary>
        private void SaveCart(CartViewModel cart)
        {
            var customerId = GetCustomerId();

            if (customerId.HasValue)
            {
                // Lưu vào database
                SaveCartToDatabase(customerId.Value, cart);
            }
            else
            {
                // Lưu vào Session
                Session[CART_SESSION_KEY] = cart;
            }
        }

        /// <summary>
        /// Cập nhật tổng tiền
        /// </summary>
        private void UpdateCartTotals(CartViewModel cart)
        {
            cart.SubTotal = cart.Items.Sum(i => i.Subtotal);
            cart.TotalItems = cart.Items.Sum(i => i.Quantity);

            // Đọc phí giao hàng và ngưỡng miễn phí từ cài đặt
            var defaultFee = GetDeliveryFeeFromSettings();
            var freeMinOrder = GetFreeDeliveryMinOrder();

            cart.DeliveryFee = (freeMinOrder > 0 && cart.SubTotal >= freeMinOrder) ? 0 : defaultFee;

            cart.TotalAmount = cart.SubTotal + cart.DeliveryFee - cart.Discount;
            if (cart.TotalAmount < 0) cart.TotalAmount = 0;
        }

        /// <summary>
        /// Lấy phí giao hàng mặc định từ DB
        /// </summary>
        private decimal GetDeliveryFeeFromSettings()
        {
            try
            {
                var result = _db.Database.SqlQuery<string>(
                    "SELECT SettingValue FROM DeliverySettings WHERE SettingKey = 'DefaultDeliveryFee'"
                ).FirstOrDefault();
                if (result != null && decimal.TryParse(result, out decimal value))
                    return value;
            }
            catch { }
            return 25000m;
        }

        /// <summary>
        /// Lấy ngưỡng đơn miễn phí giao hàng từ DB
        /// </summary>
        private decimal GetFreeDeliveryMinOrder()
        {
            try
            {
                var result = _db.Database.SqlQuery<string>(
                    "SELECT SettingValue FROM DeliverySettings WHERE SettingKey = 'FreeDeliveryMinOrder'"
                ).FirstOrDefault();
                if (result != null && decimal.TryParse(result, out decimal value))
                    return value;
            }
            catch { }
            return 300000m;
        }

        #endregion

        #region Database Cart Operations

        /// <summary>
        /// Load giỏ hàng từ database
        /// </summary>
        private CartViewModel LoadCartFromDatabase(int customerId)
        {
            try
            {
                var cart = _db.Cart
                    .Include("CartItem")
                    .Include("CartItem.MenuItem")
                    .FirstOrDefault(c => c.CustomerId == customerId);

                if (cart == null || !cart.CartItem.Any())
                {
                    return new CartViewModel
                    {
                        Items = new List<CartItemViewModel>(),
                        SubTotal = 0,
                        DeliveryFee = 0,
                        Discount = 0,
                        TotalAmount = 0,
                        TotalItems = 0
                    };
                }

                var cartViewModel = new CartViewModel
                {
                    Items = cart.CartItem.Select((ci, index) => new CartItemViewModel
                    {
                        Id = index + 1,
                        MenuItemId = ci.MenuItemId,
                        Name = ci.MenuItem?.Name ?? "Món ăn",
                        ImageUrl = ci.MenuItem?.ImageUrl,
                        Category = ci.MenuItem?.Category,
                        UnitPrice = ci.UnitPrice,
                        Quantity = ci.Quantity,
                        Subtotal = ci.UnitPrice * ci.Quantity,
                        SpecialInstructions = ci.SpecialInstructions
                    }).ToList()
                };

                UpdateCartTotals(cartViewModel);
                return cartViewModel;
            }
            catch
            {
                return new CartViewModel
                {
                    Items = new List<CartItemViewModel>(),
                    SubTotal = 0,
                    DeliveryFee = 0,
                    Discount = 0,
                    TotalAmount = 0,
                    TotalItems = 0
                };
            }
        }

        /// <summary>
        /// Lưu giỏ hàng vào database
        /// </summary>
        private void SaveCartToDatabase(int customerId, CartViewModel cartViewModel)
        {
            try
            {
                // Tìm hoặc tạo Cart cho customer
                var cart = _db.Cart.FirstOrDefault(c => c.CustomerId == customerId);
                
                if (cart == null)
                {
                    cart = new Cart
                    {
                        CustomerId = customerId,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    };
                    _db.Cart.Add(cart);
                    _db.SaveChanges();
                }
                else
                {
                    cart.UpdatedDate = DateTime.Now;
                }

                // Xóa tất cả CartItem cũ
                var oldItems = _db.CartItem.Where(ci => ci.CartId == cart.Id).ToList();
                foreach (var item in oldItems)
                {
                    _db.CartItem.Remove(item);
                }

                // Thêm CartItem mới
                foreach (var item in cartViewModel.Items)
                {
                    var cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        SpecialInstructions = item.SpecialInstructions,
                        AddedDate = DateTime.Now
                    };
                    _db.CartItem.Add(cartItem);
                }

                _db.SaveChanges();
            }
            catch
            {
                // Silent fail - fallback to session
            }
        }

        /// <summary>
        /// Merge giỏ hàng từ Session vào Database
        /// </summary>
        private CartViewModel MergeSessionCartToDatabase(int customerId, CartViewModel sessionCart, CartViewModel dbCart)
        {
            // Merge các món từ session vào database cart
            foreach (var sessionItem in sessionCart.Items)
            {
                var existingItem = dbCart.Items.FirstOrDefault(i => i.MenuItemId == sessionItem.MenuItemId);
                if (existingItem != null)
                {
                    // Cộng dồn số lượng
                    existingItem.Quantity += sessionItem.Quantity;
                    existingItem.Subtotal = existingItem.UnitPrice * existingItem.Quantity;
                }
                else
                {
                    // Thêm món mới
                    sessionItem.Id = dbCart.Items.Count + 1;
                    dbCart.Items.Add(sessionItem);
                }
            }

            UpdateCartTotals(dbCart);
            SaveCartToDatabase(customerId, dbCart);

            return dbCart;
        }

        /// <summary>
        /// Xóa giỏ hàng trong database
        /// </summary>
        private void ClearDatabaseCart(int customerId)
        {
            try
            {
                var cart = _db.Cart.FirstOrDefault(c => c.CustomerId == customerId);
                if (cart != null)
                {
                    var cartItems = _db.CartItem.Where(ci => ci.CartId == cart.Id).ToList();
                    foreach (var item in cartItems)
                    {
                        _db.CartItem.Remove(item);
                    }
                    _db.SaveChanges();
                }
            }
            catch
            {
                // Silent fail
            }
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
