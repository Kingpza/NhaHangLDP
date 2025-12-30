using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Filters;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý thanh toán và hóa đơn
    /// </summary>
    public class PaymentController : Controller
    {
        private readonly NhaHangLDPEntities db = new NhaHangLDPEntities();
        private readonly PaymentService paymentService;
        private readonly InvoiceService invoiceService;
        private readonly RealTimeNotificationService _notificationService;

        public PaymentController()
        {
            paymentService = new PaymentService(db);
            invoiceService = new InvoiceService(db);
            _notificationService = new RealTimeNotificationService();
        }

        #region Payment Processing

        /// <summary>
        /// Trang thanh toán đơn hàng
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult Process(int orderId)
        {
            var viewModel = paymentService.GetPaymentPageData(orderId);
            if (viewModel == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Dashboard", "Cashier");
            }

            return View(viewModel);
        }

        /// <summary>
        /// API xử lý thanh toán
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult ProcessPayment(ProcessPaymentRequest request)
        {
            try
            {
                var cashierId = GetCurrentCashierId();
                var shiftId = GetCurrentShiftId();

                if (!shiftId.HasValue)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });
                }

                var result = paymentService.ProcessPaymentWithPromotion(request, cashierId, shiftId.Value);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Thanh toán nhanh (không cần trang riêng)
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult QuickPayment(int orderId, string paymentMethod, decimal receivedAmount)
        {
            try
            {
                var cashierId = GetCurrentCashierId();
                var shiftId = GetCurrentShiftId();

                if (!shiftId.HasValue)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });
                }

                int billId;
                decimal changeAmount;
                string errorMessage;

                if (paymentService.ProcessPayment(orderId, paymentMethod, receivedAmount, 
                    cashierId, shiftId.Value, out billId, out changeAmount, out errorMessage))
                {
                    // Lấy thông tin để gửi thông báo real-time
                    var order = db.Order
                        .Include(o => o.RestaurantTable)
                        .FirstOrDefault(o => o.Id == orderId);

                    var bill = db.Bill.Find(billId);
                    
                    if (bill != null && order != null)
                    {
                        // Tính tổng doanh thu hôm nay
                        var today = DateTime.Today;
                        var tomorrow = today.AddDays(1);
                        var todayRevenue = db.Bill
                            .Where(b => b.Status == "Paid" && b.BillDate >= today && b.BillDate < tomorrow)
                            .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                        // Gửi thông báo cập nhật doanh thu real-time
                        _notificationService.UpdateRevenue(
                            bill.FinalAmount,
                            todayRevenue,
                            paymentMethod,
                            order.RestaurantTable?.TableNumber
                        );

                        // Cập nhật trạng thái bàn real-time
                        if (order.RestaurantTable != null)
                        {
                            _notificationService.UpdateTableStatus(
                                order.TableId,
                                order.RestaurantTable.TableNumber,
                                "Available",
                                orderId
                            );
                        }
                    }

                    return Json(new
                    {
                        success = true,
                        message = "Thanh toán thành công!",
                        billId,
                        changeAmount,
                        printUrl = Url.Action("PrintBill", "Cashier", new { orderId = orderId })
                    });
                }

                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion

        #region Promotion Validation

        /// <summary>
        /// Validate mã khuyến mãi
        /// </summary>
        [HttpPost]
        public JsonResult ValidatePromotion(ValidatePromotionRequest request)
        {
            try
            {
                var result = paymentService.ValidateAndCalculatePromotion(
                    request.Code, 
                    request.OrderTotal, 
                    request.CustomerPhone);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new PromotionValidationResult 
                { 
                    IsValid = false, 
                    Message = "Lỗi: " + ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách khuyến mãi khả dụng
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailablePromotions(decimal orderTotal)
        {
            try
            {
                var now = DateTime.Now;
                var promotions = db.Promotion
                    .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                    .OrderByDescending(p => p.DiscountValue)
                    .ToList()
                    .Select(p => new AvailablePromotion
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Description = p.Description,
                        DiscountType = p.DiscountType,
                        DiscountValue = p.DiscountValue,
                        MaxDiscount = p.MaxDiscountAmount,
                        MinOrderValue = p.MinOrderValue,
                        EndDate = p.EndDate,
                        IsApplicable = orderTotal >= p.MinOrderValue
                    })
                    .ToList();

                return Json(new { success = true, promotions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Bill/Invoice Management

        /// <summary>
        /// Danh sách hóa đơn
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult Bills()
        {
            var filter = new BillFilterModel
            {
                FromDate = DateTime.Today,
                ToDate = DateTime.Today,
                Page = 1,
                PageSize = 20
            };

            var viewModel = paymentService.GetBillList(filter);
            return View(viewModel);
        }

        /// <summary>
        /// API lấy danh sách hóa đơn
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult GetBills(DateTime? fromDate, DateTime? toDate, string status, 
            string paymentMethod, string search, int page = 1, int pageSize = 20)
        {
            try
            {
                var filter = new BillFilterModel
                {
                    FromDate = fromDate ?? DateTime.Today.AddDays(-7),
                    ToDate = toDate ?? DateTime.Today,
                    Status = status,
                    PaymentMethod = paymentMethod,
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                };

                var result = paymentService.GetBillList(filter);
                return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Chi tiết hóa đơn
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult BillDetail(int id)
        {
            var viewModel = paymentService.GetBillDetail(id);
            if (viewModel == null)
            {
                TempData["Error"] = "Không tìm thấy hóa đơn!";
                return RedirectToAction("Bills");
            }

            return View(viewModel);
        }

        /// <summary>
        /// API lấy chi tiết hóa đơn
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult GetBillDetail(int billId)
        {
            try
            {
                var bill = paymentService.GetBillDetail(billId);
                if (bill == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn!" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, bill }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// In hóa đơn
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult PrintInvoice(int billId)
        {
            var viewModel = invoiceService.GenerateDetailedInvoice(billId);
            if (viewModel == null)
            {
                return HttpNotFound("Không tìm thấy hóa đơn");
            }

            return View(viewModel);
        }

        /// <summary>
        /// Tìm kiếm hóa đơn
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult SearchBills(string keyword)
        {
            try
            {
                var results = invoiceService.SearchInvoices(keyword);
                return Json(new { success = true, bills = results }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Refund

        /// <summary>
        /// Trang hoàn tiền
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Refund(int billId)
        {
            var bill = paymentService.GetBillDetail(billId);
            if (bill == null)
            {
                TempData["Error"] = "Không tìm thấy hóa đơn!";
                return RedirectToAction("Bills");
            }

            return View(bill);
        }

        /// <summary>
        /// API xử lý hoàn tiền
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult ProcessRefund(RefundRequest request)
        {
            try
            {
                var processedBy = GetCurrentCashierId();
                var result = paymentService.ProcessRefund(request, processedBy);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new RefundResult { Success = false, Message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lịch sử hoàn tiền
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult GetRefundHistory(int billId)
        {
            try
            {
                var refunds = db.ReturnBill
                    .Include(r => r.Employee)
                    .Where(r => r.OriginalBillID == billId)
                    .OrderByDescending(r => r.ReturnDate)
                    .ToList()
                    .Select(r => new RefundViewModel
                    {
                        Id = r.ReturnBillID,
                        BillId = r.OriginalBillID,
                        RefundAmount = r.TotalRefundAmount,
                        Reason = r.Reason,
                        RefundDate = r.ReturnDate,
                        ProcessedBy = r.Employee?.FullName ?? "N/A"
                    })
                    .ToList();

                return Json(new { success = true, refunds }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Split Bill

        /// <summary>
        /// Trang tách bill
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult SplitBill(int orderId)
        {
            var order = db.Order
                .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                .Include(o => o.RestaurantTable)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Dashboard", "Cashier");
            }

            var viewModel = new SplitBillViewModel
            {
                OrderId = order.Id,
                OrderCode = $"DH{order.Id:D6}",
                TableNumber = order.RestaurantTable?.TableNumber ?? "N/A",
                Items = order.OrderDetail.Select(od => new SplitBillItem
                {
                    Id = od.Id,
                    Name = od.MenuItem?.Name ?? "N/A",
                    Quantity = od.Quantity,
                    RemainingQuantity = od.Quantity,
                    UnitPrice = od.PriceAtTime,
                    IsAssigned = false
                }).ToList(),
                TotalAmount = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime)
            };

            return View(viewModel);
        }

        /// <summary>
        /// API xử lý tách bill - Tạo nhiều hóa đơn từ 1 đơn hàng
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult ProcessSplitBill(SplitBillRequest request)
        {
            // Nếu request null, thử đọc từ Request.InputStream (JSON)
            if (request == null || request.OrderId == 0)
            {
                try
                {
                    Request.InputStream.Position = 0;
                    using (var reader = new System.IO.StreamReader(Request.InputStream))
                    {
                        var json = reader.ReadToEnd();
                        if (!string.IsNullOrEmpty(json))
                        {
                            request = Newtonsoft.Json.JsonConvert.DeserializeObject<SplitBillRequest>(json);
                        }
                    }
                }
                catch { }
            }

            if (request == null || request.OrderId == 0)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lấy thông tin đơn hàng
                    var order = db.Order
                        .Include(o => o.OrderDetail)
                        .Include(o => o.RestaurantTable)
                        .FirstOrDefault(o => o.Id == request.OrderId);

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                    }

                    if (order.Status == "Completed")
                    {
                        return Json(new { success = false, message = "Đơn hàng đã được thanh toán!" });
                    }

                    // Lấy cashier ID hợp lệ
                    var cashierId = GetCurrentCashierId();
                    
                    // Kiểm tra cashier có tồn tại không
                    var cashierExists = db.Employee.Any(e => e.Id == cashierId);
                    if (!cashierExists)
                    {
                        // Lấy bất kỳ employee nào có role Cashier hoặc Admin
                        var defaultCashier = db.Employee
                            .Include(e => e.Role)
                            .FirstOrDefault(e => e.Role.RoleName == "Cashier" || e.Role.RoleName == "Admin");
                        if (defaultCashier != null)
                        {
                            cashierId = defaultCashier.Id;
                        }
                        else
                        {
                            // Lấy employee đầu tiên có IsActive = true
                            var anyEmployee = db.Employee.FirstOrDefault(e => e.IsActive);
                            if (anyEmployee != null)
                            {
                                cashierId = anyEmployee.Id;
                            }
                            else
                            {
                                // Lấy employee bất kỳ
                                var firstEmployee = db.Employee.FirstOrDefault();
                                if (firstEmployee != null)
                                {
                                    cashierId = firstEmployee.Id;
                                }
                                else
                                {
                                    return Json(new { success = false, message = "Không tìm thấy nhân viên thu ngân!" });
                                }
                            }
                        }
                    }

                    var shiftId = GetCurrentShiftId();
                    var now = DateTime.Now;
                    var createdBillIds = new List<int>();
                    decimal totalPaid = 0;
                    var totalOrderAmount = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);

                    // Xử lý từng phần bill
                    if (request.Parts != null && request.Parts.Count > 0)
                    {
                        int partIndex = 0;
                        foreach (var part in request.Parts)
                        {
                            decimal partTotal = 0;

                            // Tính tổng tiền cho phần này
                            if (request.SplitType == "equal")
                            {
                                // Chia đều: tổng / số phần
                                partTotal = Math.Round(totalOrderAmount / request.Parts.Count, 0);
                                
                                // Phần cuối cùng lấy phần còn lại để tránh sai số làm tròn
                                if (partIndex == request.Parts.Count - 1)
                                {
                                    partTotal = totalOrderAmount - totalPaid;
                                }
                            }
                            else if (part.FixedAmount.HasValue && part.FixedAmount.Value > 0)
                            {
                                // Sử dụng FixedAmount nếu có
                                partTotal = part.FixedAmount.Value;
                            }
                            else
                            {
                                // Theo món: tính dựa trên items
                                if (part.Items != null && part.Items.Count > 0)
                                {
                                    foreach (var item in part.Items)
                                    {
                                        var orderDetail = order.OrderDetail.FirstOrDefault(od => od.Id == item.OrderDetailId);
                                        if (orderDetail != null)
                                        {
                                            partTotal += orderDetail.PriceAtTime * item.Quantity;
                                        }
                                    }
                                }
                                else
                                {
                                    // Nếu không có items, chia đều
                                    partTotal = Math.Round(totalOrderAmount / request.Parts.Count, 0);
                                    if (partIndex == request.Parts.Count - 1)
                                    {
                                        partTotal = totalOrderAmount - totalPaid;
                                    }
                                }
                            }

                            if (partTotal <= 0)
                            {
                                partIndex++;
                                continue; // Bỏ qua phần không có giá trị
                            }

                            // Tạo Bill cho phần này
                            var bill = new Bill
                            {
                                OrderId = order.Id,
                                CashierId = cashierId,
                                BillDate = now,
                                TotalAmount = partTotal,
                                DiscountAmount = 0,
                                FinalAmount = partTotal,
                                PaymentMethod = "cash", // Default
                                Status = "Paid"
                            };

                            db.Bill.Add(bill);
                            db.SaveChanges();
                            createdBillIds.Add(bill.Id);
                            totalPaid += partTotal;

                            // Cập nhật doanh thu ca
                            if (shiftId.HasValue)
                            {
                                var activeShift = db.CashierShift.FirstOrDefault(s => s.Id == shiftId.Value);
                                if (activeShift != null)
                                {
                                    activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + partTotal;
                                }
                            }

                            partIndex++;
                        }
                    }
                    else
                    {
                        // Nếu không có parts, tạo 1 bill duy nhất
                        var bill = new Bill
                        {
                            OrderId = order.Id,
                            CashierId = cashierId,
                            BillDate = now,
                            TotalAmount = totalOrderAmount,
                            DiscountAmount = 0,
                            FinalAmount = totalOrderAmount,
                            PaymentMethod = "cash",
                            Status = "Paid"
                        };

                        db.Bill.Add(bill);
                        db.SaveChanges();
                        createdBillIds.Add(bill.Id);
                        totalPaid = totalOrderAmount;
                    }

                    // Cập nhật trạng thái đơn hàng
                    order.Status = "Completed";

                    // Giải phóng bàn
                    if (order.RestaurantTable != null)
                    {
                        order.RestaurantTable.Status = "Available";
                    }

                    // Cập nhật số lượng bán của món ăn
                    foreach (var detail in order.OrderDetail)
                    {
                        var menuItem = db.MenuItem.Find(detail.MenuItemId);
                        if (menuItem != null)
                        {
                            menuItem.SoldCount = menuItem.SoldCount + detail.Quantity;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = $"Tách bill thành công! Đã tạo {createdBillIds.Count} hóa đơn với tổng {totalPaid:N0}đ.",
                        billIds = createdBillIds,
                        totalPaid = totalPaid
                    });
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {
                    transaction.Rollback();
                    var errors = ex.EntityValidationErrors
                        .SelectMany(e => e.ValidationErrors)
                        .Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = "Lỗi validation: " + string.Join(", ", errors) });
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
                {
                    transaction.Rollback();
                    var innerMessage = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                    return Json(new { success = false, message = "Lỗi cập nhật DB: " + innerMessage });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
        }

        #endregion

        #region Reports & Statistics

        /// <summary>
        /// Tổng kết thanh toán theo ngày
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult DailySummary(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;
            var viewModel = paymentService.GetDailyPaymentSummary(targetDate);
            return View(viewModel);
        }

        /// <summary>
        /// API lấy tổng kết thanh toán
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult GetDailySummary(DateTime? date)
        {
            try
            {
                var targetDate = date ?? DateTime.Today;
                var summary = paymentService.GetDailyPaymentSummary(targetDate);
                return Json(new { success = true, data = summary }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Thống kê hóa đơn
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult GetInvoiceStatistics(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var stats = invoiceService.GetInvoiceStatistics(fromDate, toDate);
                return Json(new { success = true, data = stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Top món bán chạy
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult GetTopSellingItems(DateTime fromDate, DateTime toDate, int top = 10)
        {
            try
            {
                var items = invoiceService.GetTopSellingItems(fromDate, toDate, top);
                return Json(new { success = true, data = items }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Payment History

        /// <summary>
        /// Lịch sử thanh toán của ca
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult GetShiftPaymentHistory(int? shiftId)
        {
            try
            {
                var targetShiftId = shiftId ?? GetCurrentShiftId();
                if (!targetShiftId.HasValue)
                {
                    return Json(new { success = false, message = "Không có ca làm việc!" }, JsonRequestBehavior.AllowGet);
                }

                var invoices = invoiceService.GetShiftInvoices(targetShiftId.Value);
                return Json(new { success = true, invoices }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Lịch sử thanh toán theo ngày
        /// </summary>
        [HttpGet]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult GetDailyPaymentHistory(DateTime? date)
        {
            try
            {
                var targetDate = date ?? DateTime.Today;
                var invoices = invoiceService.GetDailyInvoices(targetDate);
                return Json(new { success = true, invoices }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Calculation APIs

        /// <summary>
        /// Tính tổng tiền đơn hàng
        /// </summary>
        [HttpGet]
        public JsonResult CalculateOrderTotal(int orderId, string promotionCode = null)
        {
            try
            {
                var order = db.Order
                    .Include(o => o.OrderDetail)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);
                }

                var subTotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                var vatPercent = 10m;
                var vatSetting = db.AppSetting.FirstOrDefault(s => s.SettingKey == "DefaultVAT");
                if (vatSetting != null && decimal.TryParse(vatSetting.SettingValue, out decimal vat))
                {
                    vatPercent = vat;
                }

                var vatAmount = Math.Round(subTotal * vatPercent / 100, 0);
                decimal discountAmount = 0;
                string discountMessage = null;

                if (!string.IsNullOrEmpty(promotionCode))
                {
                    var promoResult = paymentService.ValidateAndCalculatePromotion(promotionCode, subTotal, null);
                    if (promoResult.IsValid)
                    {
                        discountAmount = promoResult.CalculatedDiscount;
                        discountMessage = promoResult.Message;
                    }
                    else
                    {
                        discountMessage = promoResult.Message;
                    }
                }

                var totalAmount = subTotal + vatAmount - discountAmount;

                return Json(new
                {
                    success = true,
                    subTotal,
                    vatPercent,
                    vatAmount,
                    discountAmount,
                    discountMessage,
                    totalAmount
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Tính tiền thừa
        /// </summary>
        [HttpGet]
        public JsonResult CalculateChange(decimal totalAmount, decimal receivedAmount)
        {
            var changeAmount = Math.Max(0, receivedAmount - totalAmount);
            return Json(new
            {
                success = true,
                totalAmount,
                receivedAmount,
                changeAmount,
                isEnough = receivedAmount >= totalAmount
            }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Helper Methods

        private int GetCurrentCashierId()
        {
            return Session["CashierId"] as int? ?? Session["EmployeeId"] as int? ?? 1;
        }

        private int? GetCurrentShiftId()
        {
            var shiftId = Session["ActiveShiftId"] as int?;
            if (!shiftId.HasValue)
            {
                var activeShift = db.CashierShift.FirstOrDefault(s => s.Status == "Active");
                shiftId = activeShift?.Id;
            }
            return shiftId;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
