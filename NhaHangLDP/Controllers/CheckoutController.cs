using NhaHangLDP.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller xử lý thanh toán
    /// </summary>
    public class CheckoutController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();
        private const string CART_SESSION_KEY = "CustomerCart";

        #region Checkout Page

        /// <summary>
        /// Trang thanh toán
        /// </summary>
        public ActionResult Index()
        {
            var cart = GetCart();
            if (cart == null || !cart.Items.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            var viewModel = new CheckoutViewModel
            {
                Cart = cart,
                CustomerInfo = GetCustomerInfo(),
                SavedAddresses = GetSavedAddresses(),
                AvailableVouchers = GetAvailableVouchers(),
                Form = new CheckoutFormModel
                {
                    OrderType = "Delivery",
                    PaymentMethod = "COD"
                }
            };

            // Pre-fill form if logged in
            if (viewModel.CustomerInfo.IsLoggedIn)
            {
                viewModel.Form.CustomerName = viewModel.CustomerInfo.FullName;
                viewModel.Form.CustomerPhone = viewModel.CustomerInfo.Phone;
                viewModel.Form.CustomerEmail = viewModel.CustomerInfo.Email;

                // Auto-fill default address
                var defaultAddress = viewModel.SavedAddresses?.FirstOrDefault(a => a.IsDefault)
                    ?? viewModel.SavedAddresses?.FirstOrDefault();
                if (defaultAddress != null)
                {
                    viewModel.Form.DeliveryAddress = defaultAddress.AddressLine;
                    viewModel.Form.Ward = defaultAddress.Ward;
                    viewModel.Form.District = defaultAddress.District;
                    viewModel.Form.City = defaultAddress.City;
                }
            }

            return View(viewModel);
        }

        /// <summary>
        /// Xử lý đặt hàng
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(CheckoutFormModel form)
        {
            try
            {
                var cart = GetCart();
                if (cart == null || !cart.Items.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống!" });
                }

                // Validate
                if (string.IsNullOrWhiteSpace(form.CustomerName) || 
                    string.IsNullOrWhiteSpace(form.CustomerPhone))
                {
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
                }

                if (form.OrderType == "Delivery" && string.IsNullOrWhiteSpace(form.DeliveryAddress))
                {
                    return Json(new { success = false, message = "Vui lòng nhập địa chỉ giao hàng!" });
                }

                // Set delivery fee to 0 for pickup/dine-in
                if (form.OrderType == "TakeAway" || form.OrderType == "DineIn")
                {
                    cart.DeliveryFee = 0;
                    cart.TotalAmount = cart.SubTotal - cart.Discount;
                }

                // Enforce payment for non-COD methods
                if (form.PaymentMethod != "COD" && form.PaymentMethod != null)
                {
                    // For VNPay/MoMo, create order first then redirect to payment
                    var paymentOrderCode = GenerateOrderCode();
                    var paymentOrderId = CreateOrderInDatabase(paymentOrderCode, form, cart);

                    // Clear cart (session + database)
                    ClearAllCarts();

                    // Store order info for payment processing
                    Session["PendingPaymentOrderId"] = paymentOrderId;
                    Session["PendingPaymentOrderCode"] = paymentOrderCode;
                    Session["PendingPaymentAmount"] = cart.TotalAmount;

                    return Json(new
                    {
                        success = true,
                        message = "Vui lòng hoàn tất thanh toán!",
                        orderCode = paymentOrderCode,
                        requirePayment = true,
                        paymentMethod = form.PaymentMethod,
                        redirectUrl = Url.Action("ProcessOnlinePayment", new { code = paymentOrderCode, method = form.PaymentMethod })
                    });
                }

                // Generate order code
                var orderCode = GenerateOrderCode();

                // Create order using raw SQL (since entities are not in EDMX yet)
                var orderId = CreateOrderInDatabase(orderCode, form, cart);

                // Clear cart (session + database)
                ClearAllCarts();

                return Json(new { 
                    success = true, 
                    message = "Đặt hàng thành công!",
                    orderCode = orderCode,
                    redirectUrl = Url.Action("Confirmation", new { code = orderCode })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Trang xác nhận đơn hàng
        /// </summary>
        public ActionResult Confirmation(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return RedirectToAction("Menu", "Public");
            }

            var order = GetOrderByCode(code);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Menu", "Public");
            }

            return View(order);
        }

        /// <summary>
        /// Theo dõi đơn hàng
        /// </summary>
        public ActionResult TrackOrder(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return View("TrackOrderSearch");
            }

            var order = GetOrderByCode(code);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng với mã " + code;
                return View("TrackOrderSearch");
            }

            var tracking = new OrderTrackingViewModel
            {
                OrderCode = code,
                Status = order.Status,
                OrderInfo = order,
                Steps = GetOrderTrackingSteps(order)
            };

            return View(tracking);
        }

        /// <summary>
        /// Tìm kiếm đơn hàng
        /// </summary>
        [HttpPost]
        public ActionResult SearchOrder(string orderCode, string phone)
        {
            if (string.IsNullOrEmpty(orderCode) || string.IsNullOrEmpty(phone))
            {
                TempData["Error"] = "Vui lòng nhập mã đơn và số điện thoại!";
                return RedirectToAction("TrackOrder");
            }

            // Verify order belongs to this phone
            var exists = _db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM CustomerOrder WHERE OrderCode = @p0 AND CustomerPhone = @p1",
                orderCode, phone).FirstOrDefault();

            if (exists == 0)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng phù hợp!";
                return RedirectToAction("TrackOrder");
            }

            return RedirectToAction("TrackOrder", new { code = orderCode });
        }

        #endregion

        #region Online Payment

        /// <summary>
        /// Xử lý thanh toán trực tuyến (VNPay/MoMo)
        /// </summary>
        public ActionResult ProcessOnlinePayment(string code, string method)
        {
            if (string.IsNullOrEmpty(code))
            {
                return RedirectToAction("Menu", "Public");
            }

            var order = GetOrderByCode(code);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Menu", "Public");
            }

            ViewBag.PaymentMethod = method;
            ViewBag.OrderCode = code;
            ViewBag.TotalAmount = order.TotalAmount;
            return View(order);
        }

        /// <summary>
        /// Xác nhận thanh toán trực tuyến (sandbox mode)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ConfirmOnlinePayment(string orderCode)
        {
            try
            {
                if (string.IsNullOrEmpty(orderCode))
                {
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                }

                // Verify order ownership
                var customerId = Session["CustomerId"] as int?;
                var pendingCode = Session["PendingPaymentOrderCode"] as string;
                if (pendingCode != orderCode)
                {
                    return Json(new { success = false, message = "Không có quyền xác nhận thanh toán cho đơn hàng này!" });
                }

                // Update payment status
                _db.Database.ExecuteSqlCommand(
                    "UPDATE CustomerOrder SET PaymentStatus = 'Paid' WHERE OrderCode = @p0",
                    orderCode);

                // Clear pending payment session
                Session["PendingPaymentOrderId"] = null;
                Session["PendingPaymentOrderCode"] = null;
                Session["PendingPaymentAmount"] = null;

                return Json(new
                {
                    success = true,
                    message = "Thanh toán thành công!",
                    redirectUrl = Url.Action("Confirmation", new { code = orderCode })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Tạo URL thanh toán VNPay sandbox và chuyển hướng
        /// </summary>
        public ActionResult CreateVNPayPayment(string orderCode)
        {
            try
            {
                var order = GetOrderByCode(orderCode);
                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng!";
                    return RedirectToAction("Menu", "Public");
                }

                var vnp_TmnCode = System.Configuration.ConfigurationManager.AppSettings["VNPay:TmnCode"];
                var vnp_HashSecret = System.Configuration.ConfigurationManager.AppSettings["VNPay:HashSecret"];
                var vnp_BaseUrl = System.Configuration.ConfigurationManager.AppSettings["VNPay:BaseUrl"];
                var vnp_ReturnUrl = System.Configuration.ConfigurationManager.AppSettings["VNPay:ReturnUrl"];

                // Build absolute return URL
                var returnUrl = Request.Url.GetLeftPart(UriPartial.Authority) + vnp_ReturnUrl;

                var vnp_Params = new SortedDictionary<string, string>
                {
                    { "vnp_Version", "2.1.0" },
                    { "vnp_Command", "pay" },
                    { "vnp_TmnCode", vnp_TmnCode },
                    { "vnp_Amount", ((long)(order.TotalAmount * 100)).ToString() },
                    { "vnp_CurrCode", "VND" },
                    { "vnp_TxnRef", orderCode },
                    { "vnp_OrderInfo", "Thanh toan don hang " + orderCode },
                    { "vnp_OrderType", "other" },
                    { "vnp_Locale", "vn" },
                    { "vnp_ReturnUrl", returnUrl },
                    { "vnp_IpAddr", Request.UserHostAddress ?? "127.0.0.1" },
                    { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") }
                };

                // Build query string and hash
                var queryBuilder = new StringBuilder();
                foreach (var kv in vnp_Params)
                {
                    if (queryBuilder.Length > 0) queryBuilder.Append("&");
                    queryBuilder.Append(HttpUtility.UrlEncode(kv.Key) + "=" + HttpUtility.UrlEncode(kv.Value));
                }

                var signData = queryBuilder.ToString();
                var vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);
                var paymentUrl = vnp_BaseUrl + "?" + signData + "&vnp_SecureHash=" + vnp_SecureHash;

                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi tạo thanh toán VNPay: " + ex.Message;
                return RedirectToAction("ProcessOnlinePayment", new { code = orderCode, method = "VNPay" });
            }
        }

        /// <summary>
        /// Xử lý kết quả trả về từ VNPay
        /// </summary>
        public ActionResult VNPayReturn()
        {
            try
            {
                var vnp_HashSecret = System.Configuration.ConfigurationManager.AppSettings["VNPay:HashSecret"];
                var vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
                var orderCode = Request.QueryString["vnp_TxnRef"];
                var vnp_ResponseCode = Request.QueryString["vnp_ResponseCode"];

                // Build data for hash verification
                var vnp_Params = new SortedDictionary<string, string>();
                foreach (string key in Request.QueryString.AllKeys)
                {
                    if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_") && key != "vnp_SecureHash" && key != "vnp_SecureHashType")
                    {
                        vnp_Params[key] = Request.QueryString[key];
                    }
                }

                var queryBuilder = new StringBuilder();
                foreach (var kv in vnp_Params)
                {
                    if (queryBuilder.Length > 0) queryBuilder.Append("&");
                    queryBuilder.Append(HttpUtility.UrlEncode(kv.Key) + "=" + HttpUtility.UrlEncode(kv.Value));
                }

                var checkHash = HmacSHA512(vnp_HashSecret, queryBuilder.ToString());
                var isValidHash = SecureCompare(checkHash, vnp_SecureHash);

                if (isValidHash && vnp_ResponseCode == "00")
                {
                    MarkOrderAsPaid(orderCode);

                    TempData["PaymentSuccess"] = true;
                    TempData["PaymentMessage"] = "Thanh toán VNPay thành công!";
                    return RedirectToAction("Confirmation", new { code = orderCode });
                }
                else
                {
                    TempData["Error"] = "Thanh toán VNPay không thành công. Mã lỗi: " + vnp_ResponseCode;
                    return RedirectToAction("TrackOrder", new { code = orderCode });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi xử lý kết quả VNPay: " + ex.Message;
                return RedirectToAction("Menu", "Public");
            }
        }

        /// <summary>
        /// Tạo thanh toán MoMo sandbox và chuyển hướng
        /// </summary>
        public ActionResult CreateMoMoPayment(string orderCode)
        {
            try
            {
                var order = GetOrderByCode(orderCode);
                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng!";
                    return RedirectToAction("Menu", "Public");
                }

                var partnerCode = System.Configuration.ConfigurationManager.AppSettings["MoMo:PartnerCode"];
                var accessKey = System.Configuration.ConfigurationManager.AppSettings["MoMo:AccessKey"];
                var secretKey = System.Configuration.ConfigurationManager.AppSettings["MoMo:SecretKey"];
                var endpoint = System.Configuration.ConfigurationManager.AppSettings["MoMo:Endpoint"];
                var returnUrlPath = System.Configuration.ConfigurationManager.AppSettings["MoMo:ReturnUrl"];
                var ipnUrlPath = System.Configuration.ConfigurationManager.AppSettings["MoMo:IpnUrl"];

                var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
                var redirectUrl = baseUrl + returnUrlPath;
                var ipnUrl = baseUrl + ipnUrlPath;

                var requestId = Guid.NewGuid().ToString();
                var amount = ((long)order.TotalAmount).ToString();
                var orderInfo = "Thanh toan don hang " + orderCode;
                var extraData = "";
                var requestType = "captureWallet";

                // Build signature
                var rawSignature = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderCode}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";
                var signature = HmacSHA256(secretKey, rawSignature);

                // Build request body
                var requestBody = new
                {
                    partnerCode = partnerCode,
                    accessKey = accessKey,
                    requestId = requestId,
                    amount = amount,
                    orderId = orderCode,
                    orderInfo = orderInfo,
                    redirectUrl = redirectUrl,
                    ipnUrl = ipnUrl,
                    extraData = extraData,
                    requestType = requestType,
                    signature = signature,
                    lang = "vi"
                };

                var serializer = new JavaScriptSerializer();
                var jsonBody = serializer.Serialize(requestBody);

                // POST to MoMo API
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    var responseJson = client.UploadString(endpoint, jsonBody);
                    var response = serializer.Deserialize<Dictionary<string, object>>(responseJson);

                    if (response.ContainsKey("payUrl") && response["payUrl"] != null)
                    {
                        return Redirect(response["payUrl"].ToString());
                    }
                    else
                    {
                        var message = response.ContainsKey("message") ? response["message"].ToString() : "Không thể tạo thanh toán MoMo";
                        TempData["Error"] = message;
                        return RedirectToAction("ProcessOnlinePayment", new { code = orderCode, method = "MoMo" });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi tạo thanh toán MoMo: " + ex.Message;
                return RedirectToAction("ProcessOnlinePayment", new { code = orderCode, method = "MoMo" });
            }
        }

        /// <summary>
        /// Xử lý kết quả trả về từ MoMo (redirect)
        /// </summary>
        public ActionResult MoMoReturn()
        {
            try
            {
                var orderCode = Request.QueryString["orderId"];
                var resultCode = Request.QueryString["resultCode"];

                if (resultCode == "0")
                {
                    MarkOrderAsPaid(orderCode);

                    TempData["PaymentSuccess"] = true;
                    TempData["PaymentMessage"] = "Thanh toán MoMo thành công!";
                    return RedirectToAction("Confirmation", new { code = orderCode });
                }
                else
                {
                    TempData["Error"] = "Thanh toán MoMo không thành công.";
                    return RedirectToAction("TrackOrder", new { code = orderCode });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi xử lý kết quả MoMo: " + ex.Message;
                return RedirectToAction("Menu", "Public");
            }
        }

        /// <summary>
        /// Xử lý IPN callback từ MoMo (server-to-server)
        /// </summary>
        [HttpPost]
        public JsonResult MoMoIPN()
        {
            try
            {
                Request.InputStream.Position = 0;
                var reader = new System.IO.StreamReader(Request.InputStream);
                var body = reader.ReadToEnd();
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(body);

                var orderCode = data.ContainsKey("orderId") ? data["orderId"]?.ToString() : null;
                var resultCode = data.ContainsKey("resultCode") ? data["resultCode"]?.ToString() : null;
                var receivedSignature = data.ContainsKey("signature") ? data["signature"]?.ToString() : null;

                // Verify signature
                var secretKey = System.Configuration.ConfigurationManager.AppSettings["MoMo:SecretKey"];
                var accessKey = System.Configuration.ConfigurationManager.AppSettings["MoMo:AccessKey"];

                var amount = data.ContainsKey("amount") ? data["amount"]?.ToString() : "";
                var extraData = data.ContainsKey("extraData") ? data["extraData"]?.ToString() : "";
                var message = data.ContainsKey("message") ? data["message"]?.ToString() : "";
                var orderInfo = data.ContainsKey("orderInfo") ? data["orderInfo"]?.ToString() : "";
                var orderType = data.ContainsKey("orderType") ? data["orderType"]?.ToString() : "";
                var partnerCode = data.ContainsKey("partnerCode") ? data["partnerCode"]?.ToString() : "";
                var payType = data.ContainsKey("payType") ? data["payType"]?.ToString() : "";
                var requestId = data.ContainsKey("requestId") ? data["requestId"]?.ToString() : "";
                var responseTime = data.ContainsKey("responseTime") ? data["responseTime"]?.ToString() : "";
                var transId = data.ContainsKey("transId") ? data["transId"]?.ToString() : "";

                var rawSignature = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&message={message}&orderId={orderCode}&orderInfo={orderInfo}&orderType={orderType}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={responseTime}&resultCode={resultCode}&transId={transId}";
                var expectedSignature = HmacSHA256(secretKey, rawSignature);

                if (SecureCompare(expectedSignature, receivedSignature) && resultCode == "0" && !string.IsNullOrEmpty(orderCode))
                {
                    _db.Database.ExecuteSqlCommand(
                        "UPDATE CustomerOrder SET PaymentStatus = 'Paid' WHERE OrderCode = @p0",
                        orderCode);
                }

                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        private void MarkOrderAsPaid(string orderCode)
        {
            _db.Database.ExecuteSqlCommand(
                "UPDATE CustomerOrder SET PaymentStatus = 'Paid' WHERE OrderCode = @p0",
                orderCode);

            Session["PendingPaymentOrderId"] = null;
            Session["PendingPaymentOrderCode"] = null;
            Session["PendingPaymentAmount"] = null;
        }

        private string HmacSHA512(string key, string data)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private string HmacSHA256(string key, string data)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private static bool SecureCompare(string a, string b)
        {
            if (a == null || b == null) return false;
            var aLower = a.ToLowerInvariant();
            var bLower = b.ToLowerInvariant();
            if (aLower.Length != bLower.Length) return false;

            int result = 0;
            for (int i = 0; i < aLower.Length; i++)
            {
                result |= aLower[i] ^ bLower[i];
            }
            return result == 0;
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Lưu địa chỉ giao hàng vào Profile từ trang Checkout
        /// </summary>
        [HttpPost]
        public JsonResult SaveAddressFromCheckout(string deliveryAddress, string ward, string district, string city)
        {
            try
            {
                var customerId = Session["CustomerId"] as int?;
                if (!customerId.HasValue)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });
                }

                if (string.IsNullOrWhiteSpace(deliveryAddress))
                {
                    return Json(new { success = false, message = "Vui lòng nhập địa chỉ!" });
                }

                // Get customer info for receiver name/phone
                var customer = _db.Database.SqlQuery<CustomerBasicInfo>(
                    "SELECT Id, FullName, Email, Phone FROM Customer WHERE Id = @p0",
                    customerId.Value).FirstOrDefault();

                if (customer == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng!" });
                }

                // Check if address already exists
                var existingCount = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM CustomerAddress WHERE CustomerId = @p0 AND AddressLine = @p1 AND ISNULL(District, '') = @p2",
                    customerId.Value, deliveryAddress, district ?? "").FirstOrDefault();

                if (existingCount > 0)
                {
                    return Json(new { success = true, message = "Địa chỉ đã được lưu trước đó!" });
                }

                // Check if this is the first address (make it default)
                var addressCount = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM CustomerAddress WHERE CustomerId = @p0",
                    customerId.Value).FirstOrDefault();

                var isDefault = addressCount == 0;
                var saveCity = !string.IsNullOrWhiteSpace(city) ? city : "TP. Hồ Chí Minh";

                _db.Database.ExecuteSqlCommand(
                    @"INSERT INTO CustomerAddress (CustomerId, ReceiverName, ReceiverPhone, AddressLine, Ward, District, City, AddressType, IsDefault)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 'Home', @p7)",
                    customerId.Value, customer.FullName, customer.Phone, deliveryAddress,
                    ward ?? "", district ?? "", saveCity, isDefault);

                return Json(new { success = true, message = "Đã lưu địa chỉ thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Tính phí ship
        /// </summary>
        [HttpPost]
        public JsonResult CalculateDeliveryFee(string district)
        {
            var cart = GetCart();
            if (cart == null) return Json(new { success = false });
            var defaultFee = GetDeliveryFeeFromSettings();
            var freeMinOrder = GetFreeDeliveryMinOrder();
            decimal fee = (freeMinOrder > 0 && cart.SubTotal >= freeMinOrder) ? 0 : defaultFee;

            return Json(new { success = true, fee = fee });
        }

        /// <summary>
        /// Tính phí ship theo khoảng cách (Haversine - không cần API key)
        /// Tọa độ nhà hàng lấy từ DeliverySettings (RestaurantLat / RestaurantLng)
        /// </summary>
        [HttpPost]
        public JsonResult CalculateDeliveryFeeByDistance(double customerLat, double customerLng)
        {
            try
            {
                // Tọa độ nhà hàng (lấy từ DB hoặc dùng giá trị mặc định)
                double restaurantLat = GetRestaurantCoordinate("RestaurantLat", 10.7769);  // Mặc định: TP.HCM
                double restaurantLng = GetRestaurantCoordinate("RestaurantLng", 106.7009);

                // Tính khoảng cách theo công thức Haversine
                double distanceKm = CalculateHaversineDistance(restaurantLat, restaurantLng, customerLat, customerLng);

                // Lấy phí theo km từ DB
                decimal fee;
                string rangeText;
                if (distanceKm < 10)
                {
                    fee = GetFeeByKey("FeeUnder10Km", 15000);
                    rangeText = "Dưới 10km";
                }
                else if (distanceKm <= 20)
                {
                    fee = GetFeeByKey("Fee10To20Km", 25000);
                    rangeText = "10 - 20km";
                }
                else
                {
                    fee = GetFeeByKey("FeeOver20Km", 40000);
                    rangeText = "Trên 20km";
                }

                // Kiểm tra miễn phí
                var cart = GetCart();
                var freeMinOrder = GetFreeDeliveryMinOrder();
                if (freeMinOrder > 0 && cart != null && cart.SubTotal >= freeMinOrder)
                {
                    fee = 0;
                }

                return Json(new
                {
                    success = true,
                    distanceKm = Math.Round(distanceKm, 1),
                    fee = fee,
                    rangeText = rangeText,
                    message = fee == 0 ? "Miễn phí giao hàng!" : $"Phí giao hàng ({rangeText}): {fee:N0}đ"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private double GetRestaurantCoordinate(string key, double defaultValue)
        {
            try
            {
                var result = _db.Database.SqlQuery<string>(
                    "SELECT SettingValue FROM DeliverySettings WHERE SettingKey = @p0", key
                ).FirstOrDefault();
                if (result != null && double.TryParse(result, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                    return val;
            }
            catch { }
            return defaultValue;
        }

        private decimal GetFeeByKey(string key, decimal defaultValue)
        {
            try
            {
                var result = _db.Database.SqlQuery<string>(
                    "SELECT SettingValue FROM DeliverySettings WHERE SettingKey = @p0", key
                ).FirstOrDefault();
                if (result != null && decimal.TryParse(result, out decimal value))
                    return value;
            }
            catch { }
            return defaultValue;
        }

        /// <summary>
        /// Công thức Haversine tính khoảng cách (km) giữa 2 tọa độ
        /// </summary>
        private double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            var dLat = ToRad(lat2 - lat1);
            var dLng = ToRad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private double ToRad(double deg) => deg * Math.PI / 180;

        #endregion

        #region Cancel Unpaid Order

        /// <summary>
        /// Hủy đơn hàng chưa thanh toán sau 1 giờ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CancelUnpaidOrder(string orderCode)
        {
            try
            {
                if (string.IsNullOrEmpty(orderCode))
                {
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                }

                // Check if order exists and is still unpaid
                var order = _db.Database.SqlQuery<OrderStatusInfo>(
                    @"SELECT Id, OrderCode, PaymentStatus, Status, OrderDate
                      FROM CustomerOrder 
                      WHERE OrderCode = @p0",
                    orderCode).FirstOrDefault();

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                }

                // Only cancel if payment is still pending
                if (order.PaymentStatus != "Pending")
                {
                    return Json(new { success = false, message = "Đơn hàng đã được thanh toán hoặc đã bị hủy!" });
                }

                // Check if order is older than 1 hour
                var oneHourAgo = DateTime.Now.AddHours(-1);
                if (order.OrderDate > oneHourAgo)
                {
                    return Json(new { success = false, message = "Chưa hết thời gian thanh toán!" });
                }

                // Cancel the order
                _db.Database.ExecuteSqlCommand(
                    @"UPDATE CustomerOrder 
                      SET Status = 'Cancelled', 
                          PaymentStatus = 'Cancelled',
                          CancelReason = N'Hủy tự động do quá thời gian thanh toán (1 giờ)',
                          CancelledDate = GETDATE()
                      WHERE OrderCode = @p0",
                    orderCode);

                // Clear pending payment session if this is the current order
                var pendingCode = Session["PendingPaymentOrderCode"] as string;
                if (pendingCode == orderCode)
                {
                    Session["PendingPaymentOrderId"] = null;
                    Session["PendingPaymentOrderCode"] = null;
                    Session["PendingPaymentAmount"] = null;
                }

                return Json(new
                {
                    success = true,
                    message = "Đơn hàng đã được hủy do quá thời gian thanh toán."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private CartViewModel GetCart()
        {
            var customerId = Session["CustomerId"] as int?;

            // Đã đăng nhập: lấy giỏ hàng từ database (giống CartController)
            if (customerId.HasValue)
            {
                try
                {
                    var items = _db.Database.SqlQuery<CartItemDbModel>(
                        @"SELECT ci.Id, ci.MenuItemId, m.Name, m.ImageUrl, m.Category,
                                 ci.Quantity, m.Price as UnitPrice,
                                 (ci.Quantity * m.Price) as Subtotal,
                                 ci.SpecialInstructions
                          FROM CartItem ci
                          JOIN Cart c ON ci.CartId = c.Id
                          JOIN MenuItem m ON ci.MenuItemId = m.Id
                          WHERE c.CustomerId = @p0",
                        customerId.Value).ToList();

                    var cartItems = items.Select(i => new CartItemViewModel
                    {
                        Id = i.Id,
                        MenuItemId = i.MenuItemId,
                        Name = i.Name,
                        ImageUrl = i.ImageUrl,
                        Category = i.Category,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Subtotal = i.Subtotal,
                        SpecialInstructions = i.SpecialInstructions
                    }).ToList();

                    var subTotal = cartItems.Sum(i => i.Subtotal);
                    var defaultFee = GetDeliveryFeeFromSettings();
                    var freeMinOrder = GetFreeDeliveryMinOrder();
                    var deliveryFee = (freeMinOrder > 0 && subTotal >= freeMinOrder) ? 0 : defaultFee;

                    return new CartViewModel
                    {
                        Items = cartItems,
                        SubTotal = subTotal,
                        DeliveryFee = deliveryFee,
                        Discount = 0,
                        TotalAmount = subTotal + deliveryFee,
                        TotalItems = cartItems.Sum(i => i.Quantity)
                    };
                }
                catch
                {
                    // Fallback: thử lấy từ session
                    return Session[CART_SESSION_KEY] as CartViewModel;
                }
            }

            // Chưa đăng nhập: lấy từ Session
            return Session[CART_SESSION_KEY] as CartViewModel;
        }

        private class CartItemDbModel
        {
            public int Id { get; set; }
            public int MenuItemId { get; set; }
            public string Name { get; set; }
            public string ImageUrl { get; set; }
            public string Category { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal { get; set; }
            public string SpecialInstructions { get; set; }
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

        private CustomerInfoViewModel GetCustomerInfo()
        {
            // Check if customer is logged in
            var customerId = Session["CustomerId"];
            if (customerId != null)
            {
                var customer = _db.Database.SqlQuery<CustomerBasicInfo>(
                    "SELECT Id, FullName, Email, Phone FROM Customer WHERE Id = @p0",
                    (int)customerId).FirstOrDefault();

                if (customer != null)
                {
                    return new CustomerInfoViewModel
                    {
                        Id = customer.Id,
                        FullName = customer.FullName,
                        Email = customer.Email,
                        Phone = customer.Phone,
                        IsLoggedIn = true
                    };
                }
            }

            return new CustomerInfoViewModel { IsLoggedIn = false };
        }

        private List<CustomerAddressViewModel> GetSavedAddresses()
        {
            var customerId = Session["CustomerId"];
            if (customerId == null) return new List<CustomerAddressViewModel>();

            try
            {
                var addresses = _db.Database.SqlQuery<AddressInfo>(
                    @"SELECT Id, ReceiverName, ReceiverPhone, AddressLine, Ward, District, City, AddressType, IsDefault 
                      FROM CustomerAddress WHERE CustomerId = @p0 ORDER BY IsDefault DESC",
                    (int)customerId).ToList();

                return addresses.Select(a => new CustomerAddressViewModel
                {
                    Id = a.Id,
                    ReceiverName = a.ReceiverName,
                    ReceiverPhone = a.ReceiverPhone,
                    AddressLine = a.AddressLine,
                    Ward = a.Ward,
                    District = a.District,
                    City = a.City,
                    AddressType = a.AddressType,
                    IsDefault = a.IsDefault,
                    FullAddress = $"{a.AddressLine}, {a.Ward}, {a.District}, {a.City}"
                }).ToList();
            }
            catch
            {
                return new List<CustomerAddressViewModel>();
            }
        }

        private List<VoucherViewModel> GetAvailableVouchers()
        {
            try
            {
                var vouchers = _db.Database.SqlQuery<VoucherInfo>(
                    @"SELECT Id, Code, Name, Description, DiscountType, DiscountValue, MaxDiscountAmount, MinOrderAmount, EndDate
                      FROM Voucher 
                      WHERE IsActive = 1 AND StartDate <= GETDATE() AND EndDate >= GETDATE()
                      AND (UsageLimit IS NULL OR UsedCount < UsageLimit)").ToList();

                var cart = GetCart();

                return vouchers.Select(v => new VoucherViewModel
                {
                    Id = v.Id,
                    Code = v.Code,
                    Name = v.Name,
                    Description = v.Description,
                    DiscountText = v.DiscountType == "Percentage" 
                        ? $"Giảm {v.DiscountValue}%" 
                        : $"Giảm {v.DiscountValue:N0}đ",
                    MinOrderAmount = v.MinOrderAmount,
                    ExpiryDate = v.EndDate,
                    IsApplicable = !v.MinOrderAmount.HasValue || cart.SubTotal >= v.MinOrderAmount.Value,
                    NotApplicableReason = v.MinOrderAmount.HasValue && cart.SubTotal < v.MinOrderAmount.Value
                        ? $"Đơn tối thiểu {v.MinOrderAmount:N0}đ"
                        : null
                }).ToList();
            }
            catch
            {
                return new List<VoucherViewModel>();
            }
        }

        private string GenerateOrderCode()
        {
            return "DH" + DateTime.Now.ToString("yyMMdd") + 
                   new Random().Next(1000, 9999).ToString();
        }

        private int CreateOrderInDatabase(string orderCode, CheckoutFormModel form, CartViewModel cart)
        {
            var customerId = Session["CustomerId"] as int?;

            // Insert order
            var sql = @"
                INSERT INTO CustomerOrder 
                (OrderCode, CustomerId, CustomerName, CustomerPhone, CustomerEmail, OrderType, 
                 DeliveryAddress, Ward, District, City, SubTotal, DeliveryFee, Discount, 
                 VoucherCode, TotalAmount, PaymentMethod, PaymentStatus, Status, Note, OrderDate, EstimatedDeliveryTime)
                VALUES 
                (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, 'Pending', 'Pending', @p16, GETDATE(), DATEADD(HOUR, 1, GETDATE()));
                SELECT SCOPE_IDENTITY();";

            var orderId = _db.Database.SqlQuery<decimal>(sql,
                orderCode,                              // @p0
                customerId,                             // @p1
                form.CustomerName,                      // @p2
                form.CustomerPhone,                     // @p3
                form.CustomerEmail,                     // @p4
                form.OrderType,                         // @p5
                form.DeliveryAddress,                   // @p6
                form.Ward,                              // @p7
                form.District,                          // @p8
                form.City ?? "TP. Hồ Chí Minh",        // @p9
                cart.SubTotal,                          // @p10
                cart.DeliveryFee,                       // @p11
                cart.Discount,                          // @p12
                cart.VoucherCode,                       // @p13
                cart.TotalAmount,                       // @p14
                form.PaymentMethod,                     // @p15
                form.Note                               // @p16
            ).FirstOrDefault();

            var orderIdInt = (int)orderId;

            // Insert order details
            foreach (var item in cart.Items)
            {
                _db.Database.ExecuteSqlCommand(
                    @"INSERT INTO CustomerOrderDetail (CustomerOrderId, MenuItemId, ItemName, Quantity, UnitPrice, Subtotal, SpecialInstructions)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                    orderIdInt, item.MenuItemId, item.Name, item.Quantity, item.UnitPrice, item.Subtotal, item.SpecialInstructions);
            }

            // Update voucher usage if applied
            if (!string.IsNullOrEmpty(cart.VoucherCode))
            {
                _db.Database.ExecuteSqlCommand(
                    @"UPDATE Voucher SET UsedCount = UsedCount + 1 WHERE Code = @p0;
                      INSERT INTO VoucherUsage (VoucherId, CustomerId, OrderId, DiscountAmount)
                      SELECT Id, @p1, @p2, @p3 FROM Voucher WHERE Code = @p0",
                    cart.VoucherCode, customerId, orderIdInt, cart.Discount);
            }

            return orderIdInt;
        }

        private OrderConfirmationViewModel GetOrderByCode(string code)
        {
            try
            {
                var order = _db.Database.SqlQuery<OrderInfo>(
                    @"SELECT Id, OrderCode, CustomerName, CustomerPhone, DeliveryAddress, Ward, District, City,
                             PaymentMethod, PaymentStatus, SubTotal, DeliveryFee, Discount, TotalAmount, 
                             Status, OrderDate, EstimatedDeliveryTime
                      FROM CustomerOrder WHERE OrderCode = @p0", code).FirstOrDefault();

                if (order == null) return null;

                var items = _db.Database.SqlQuery<OrderItemInfo>(
                    @"SELECT od.ItemName as Name, od.Quantity, od.UnitPrice, od.Subtotal, m.ImageUrl
                      FROM CustomerOrderDetail od
                      LEFT JOIN MenuItem m ON od.MenuItemId = m.Id
                      WHERE od.CustomerOrderId = @p0", order.Id).ToList();

                return new OrderConfirmationViewModel
                {
                    OrderCode = order.OrderCode,
                    Status = order.Status,
                    CustomerName = order.CustomerName,
                    CustomerPhone = order.CustomerPhone,
                    DeliveryAddress = $"{order.DeliveryAddress}, {order.Ward}, {order.District}, {order.City}",
                    PaymentMethod = GetPaymentMethodText(order.PaymentMethod),
                    PaymentStatus = order.PaymentStatus,
                    SubTotal = order.SubTotal,
                    DeliveryFee = order.DeliveryFee,
                    Discount = order.Discount,
                    TotalAmount = order.TotalAmount,
                    OrderDate = order.OrderDate,
                    EstimatedDeliveryTime = order.EstimatedDeliveryTime,
                    Items = items.Select(i => new OrderItemViewModel
                    {
                        Name = i.Name,
                        ImageUrl = i.ImageUrl,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Subtotal = i.Subtotal
                    }).ToList()
                };
            }
            catch
            {
                return null;
            }
        }

        private List<OrderTrackingStep> GetOrderTrackingSteps(OrderConfirmationViewModel order)
        {
            var steps = new List<OrderTrackingStep>
            {
                new OrderTrackingStep { Status = "Pending", Title = "Đã đặt hàng", Icon = "shopping_cart", Description = "Đơn hàng đã được tiếp nhận" },
                new OrderTrackingStep { Status = "Confirmed", Title = "Xác nhận", Icon = "check_circle", Description = "Đơn hàng đã được xác nhận" },
                new OrderTrackingStep { Status = "Preparing", Title = "Đang chuẩn bị", Icon = "restaurant", Description = "Đầu bếp đang chuẩn bị món" },
                new OrderTrackingStep { Status = "Ready", Title = "Sẵn sàng", Icon = "takeout_dining", Description = "Món ăn đã sẵn sàng" },
                new OrderTrackingStep { Status = "Delivering", Title = "Đang giao", Icon = "delivery_dining", Description = "Shipper đang giao hàng" },
                new OrderTrackingStep { Status = "Completed", Title = "Hoàn thành", Icon = "check_box", Description = "Đơn hàng hoàn thành" }
            };

            var statusOrder = new[] { "Pending", "Confirmed", "Preparing", "Ready", "Delivering", "Completed" };
            var currentIndex = Array.IndexOf(statusOrder, order.Status);

            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].IsCompleted = i <= currentIndex;
                steps[i].IsCurrent = i == currentIndex;
            }

            return steps;
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
                // Silent fail - cart session already cleared
            }
        }

        /// <summary>
        /// Xóa cả session cart và database cart
        /// </summary>
        private void ClearAllCarts()
        {
            Session[CART_SESSION_KEY] = null;
            var customerId = Session["CustomerId"] as int?;
            if (customerId.HasValue)
            {
                ClearDatabaseCart(customerId.Value);
            }
        }

        private string GetPaymentMethodText(string method)
        {
            switch (method)
            {
                case "COD": return "Thanh toán khi nhận hàng";
                case "VNPay": return "VNPay";
                case "MoMo": return "Ví MoMo";
                case "Card": return "Thẻ tín dụng/ghi nợ";
                default: return method;
            }
        }

        #endregion

        #region Helper Classes

        private class CustomerBasicInfo
        {
            public int Id { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }

        private class AddressInfo
        {
            public int Id { get; set; }
            public string ReceiverName { get; set; }
            public string ReceiverPhone { get; set; }
            public string AddressLine { get; set; }
            public string Ward { get; set; }
            public string District { get; set; }
            public string City { get; set; }
            public string AddressType { get; set; }
            public bool IsDefault { get; set; }
        }

        private class VoucherInfo
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string DiscountType { get; set; }
            public decimal DiscountValue { get; set; }
            public decimal? MaxDiscountAmount { get; set; }
            public decimal? MinOrderAmount { get; set; }
            public DateTime EndDate { get; set; }
        }

        private class OrderInfo
        {
            public int Id { get; set; }
            public string OrderCode { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
            public string DeliveryAddress { get; set; }
            public string Ward { get; set; }
            public string District { get; set; }
            public string City { get; set; }
            public string PaymentMethod { get; set; }
            public string PaymentStatus { get; set; }
            public decimal SubTotal { get; set; }
            public decimal DeliveryFee { get; set; }
            public decimal Discount { get; set; }
            public decimal TotalAmount { get; set; }
            public string Status { get; set; }
            public DateTime OrderDate { get; set; }
            public DateTime? EstimatedDeliveryTime { get; set; }
        }

        private class OrderItemInfo
        {
            public string Name { get; set; }
            public string ImageUrl { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal { get; set; }
        }

        private class OrderStatusInfo
        {
            public int Id { get; set; }
            public string OrderCode { get; set; }
            public string PaymentStatus { get; set; }
            public string Status { get; set; }
            public DateTime OrderDate { get; set; }
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
