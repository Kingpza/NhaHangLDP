using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý thanh toán
    /// </summary>
    public class PaymentService
    {
        private readonly MyDbContext db;
        private const decimal DEFAULT_VAT_PERCENT = 10m;

        public PaymentService(MyDbContext context)
        {
            db = context;
        }

        #region Payment Processing

        /// <summary>
        /// Xử lý thanh toán đơn hàng
        /// </summary>
        public bool ProcessPayment(int orderId, string paymentMethod, decimal receivedAmount, 
            int cashierId, int shiftId, out int billId, out decimal changeAmount, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.Orders
                        .Include(o => o.OrderDetails).ThenInclude(od => od.MenuItem)
                        .Include(o => o.Table)
                        .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        errorMessage = "Không tìm thấy đơn hàng!";
                        billId = 0;
                        changeAmount = 0;
                        return false;
                    }

                    if (order.Status == "Completed")
                    {
                        errorMessage = "Đơn hàng đã được thanh toán!";
                        billId = 0;
                        changeAmount = 0;
                        return false;
                    }

                    // Tính toán
                    var calculatedTotal = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
                    var vatPercent = GetVATPercent();
                    var vat = Math.Round(calculatedTotal * vatPercent / 100, 0);
                    var finalAmount = calculatedTotal + vat;

                    if (paymentMethod.ToLower() == "cash" && receivedAmount < finalAmount)
                    {
                        errorMessage = "Số tiền nhận không đủ để thanh toán!";
                        billId = 0;
                        changeAmount = 0;
                        return false;
                    }

                    // Tạo hóa đơn
                    var bill = new Bill
                    {
                        OrderId = order.Id,
                        CashierId = cashierId,
                        BillDate = DateTime.Now,
                        TotalAmount = calculatedTotal,
                        DiscountAmount = 0,
                        FinalAmount = finalAmount,
                        PaymentMethod = paymentMethod,
                        Status = "Paid"
                    };

                    db.Bills.Add(bill);
                    
                    // Cập nhật trạng thái đơn hàng
                    order.Status = "Completed";
                    
                    // Giải phóng bàn
                    if (order.Table != null)
                    {
                        order.Table.Status = "Available";
                    }

                    // Cập nhật doanh thu ca
                    var activeShift = db.CashierShifts.FirstOrDefault(s => s.Id == shiftId);
                    if (activeShift != null)
                    {
                        activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + finalAmount;
                    }

                    // Cập nhật số lượng bán của món ăn
                    foreach (var detail in order.OrderDetails)
                    {
                        var menuItem = db.MenuItems.Find(detail.MenuItemId);
                        if (menuItem != null)
                        {
                            menuItem.SoldCount = menuItem.SoldCount + detail.Quantity;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    billId = bill.Id;
                    changeAmount = Math.Max(0, receivedAmount - finalAmount);
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    billId = 0;
                    changeAmount = 0;
                    return false;
                }
            }
        }

        /// <summary>
        /// Xử lý thanh toán với khuyến mãi
        /// </summary>
        public PaymentResult ProcessPaymentWithPromotion(ProcessPaymentRequest request, int cashierId, int shiftId)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.Orders
                        .Include(o => o.OrderDetails).ThenInclude(od => od.MenuItem)
                        .Include(o => o.Table)
                        .Include(o => o.Bills)
                        .FirstOrDefault(o => o.Id == request.OrderId);

                    if (order == null)
                    {
                        return new PaymentResult { Success = false, Message = "Không tìm thấy đơn hàng!" };
                    }

                    if (order.Status == "Completed" || order.Bills.Any(b => b.Status == "Paid"))
                    {
                        return new PaymentResult { Success = false, Message = "Đơn hàng đã được thanh toán!" };
                    }

                    // Tính toán
                    var subTotal = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
                    var vatPercent = GetVATPercent();
                    var vatAmount = Math.Round(subTotal * vatPercent / 100, 0);
                    decimal discountAmount = 0;
                    int? promotionId = null;

                    // Áp dụng khuyến mãi
                    if (!string.IsNullOrEmpty(request.PromotionCode))
                    {
                        var promoResult = ValidateAndCalculatePromotion(request.PromotionCode, subTotal, request.CustomerPhone);
                        if (promoResult.IsValid)
                        {
                            discountAmount = promoResult.CalculatedDiscount;
                            promotionId = promoResult.PromotionId;
                        }
                    }

                    var finalAmount = subTotal + vatAmount - discountAmount;

                    // Validate số tiền nhận
                    if (request.PaymentMethod.ToLower() == "cash" && request.ReceivedAmount < finalAmount)
                    {
                        return new PaymentResult { Success = false, Message = "Số tiền nhận không đủ để thanh toán!" };
                    }

                    // Tạo mã hóa đơn
                    var invoiceNumber = GenerateInvoiceNumber();

                    // Tạo hóa đơn
                    var bill = new Bill
                    {
                        OrderId = order.Id,
                        CashierId = cashierId,
                        BillDate = DateTime.Now,
                        TotalAmount = subTotal + vatAmount,
                        DiscountAmount = discountAmount,
                        FinalAmount = finalAmount,
                        PaymentMethod = request.PaymentMethod,
                        Status = "Paid"
                    };

                    db.Bills.Add(bill);
                    db.SaveChanges();

                    // Lưu sử dụng khuyến mãi
                    if (promotionId.HasValue)
                    {
                        var promotionUsage = new PromotionUsage
                        {
                            PromotionId = promotionId.Value,
                            BillId = bill.Id,
                            DiscountApplied = discountAmount,
                            UsedDate = DateTime.Now,
                            CustomerPhone = request.CustomerPhone
                        };
                        db.PromotionUsages.Add(promotionUsage);

                        // Cập nhật số lần sử dụng promotion
                        var promotion = db.Promotions.Find(promotionId.Value);
                        if (promotion != null)
                        {
                            promotion.UsedCount = promotion.UsedCount + 1;
                        }
                    }

                    // Cập nhật trạng thái đơn hàng
                    order.Status = "Completed";

                    // Giải phóng bàn
                    if (order.Table != null)
                    {
                        order.Table.Status = "Available";
                    }

                    // Cập nhật doanh thu ca
                    var activeShift = db.CashierShifts.FirstOrDefault(s => s.Id == shiftId);
                    if (activeShift != null)
                    {
                        activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + finalAmount;
                    }

                    // Cập nhật số lượng bán
                    foreach (var detail in order.OrderDetails)
                    {
                        var menuItem = db.MenuItems.Find(detail.MenuItemId);
                        if (menuItem != null)
                        {
                            menuItem.SoldCount = menuItem.SoldCount + detail.Quantity;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    var changeAmount = Math.Max(0, request.ReceivedAmount - finalAmount);

                    return new PaymentResult
                    {
                        Success = true,
                        Message = "Thanh toán thành công!",
                        BillId = bill.Id,
                        InvoiceNumber = invoiceNumber,
                        TotalPaid = finalAmount,
                        ChangeAmount = changeAmount,
                        PaymentTime = DateTime.Now,
                        PrintUrl = $"/Cashier/PrintBill?orderId={order.Id}"
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new PaymentResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
                }
            }
        }

        #endregion

        #region Promotion Validation

        /// <summary>
        /// Validate và tính toán khuyến mãi
        /// </summary>
        public PromotionValidationResult ValidateAndCalculatePromotion(string code, decimal orderTotal, string customerPhone = null)
        {
            try
            {
                var promotion = db.Promotions.FirstOrDefault(p => p.Code == code && p.IsActive);
                
                if (promotion == null)
                {
                    return new PromotionValidationResult 
                    { 
                        IsValid = false, 
                        Message = "Mã khuyến mãi không tồn tại hoặc đã hết hiệu lực!" 
                    };
                }

                var now = DateTime.Now;

                // Kiểm tra thời gian
                if (now < promotion.StartDate)
                {
                    return new PromotionValidationResult 
                    { 
                        IsValid = false, 
                        Message = $"Mã khuyến mãi có hiệu lực từ {promotion.StartDate:dd/MM/yyyy}!" 
                    };
                }

                if (now > promotion.EndDate)
                {
                    return new PromotionValidationResult 
                    { 
                        IsValid = false, 
                        Message = "Mã khuyến mãi đã hết hạn!" 
                    };
                }

                // Kiểm tra giá trị đơn tối thiểu
                if (orderTotal < promotion.MinOrderValue)
                {
                    return new PromotionValidationResult 
                    { 
                        IsValid = false, 
                        Message = $"Đơn hàng tối thiểu {promotion.MinOrderValue:N0}đ để áp dụng mã này!" 
                    };
                }

                // Kiểm tra số lần sử dụng
                if (promotion.MaxUsageCount.HasValue && 
                    promotion.UsedCount >= promotion.MaxUsageCount.Value)
                {
                    return new PromotionValidationResult 
                    { 
                        IsValid = false, 
                        Message = "Mã khuyến mãi đã hết lượt sử dụng!" 
                    };
                }

                // Kiểm tra số lần sử dụng của khách hàng
                if (!string.IsNullOrEmpty(customerPhone) && promotion.MaxUsagePerCustomer.HasValue)
                {
                    var customerUsageCount = db.PromotionUsages
                        .Count(pu => pu.PromotionId == promotion.Id && pu.CustomerPhone == customerPhone);

                    if (customerUsageCount >= promotion.MaxUsagePerCustomer.Value)
                    {
                        return new PromotionValidationResult 
                        { 
                            IsValid = false, 
                            Message = "Bạn đã sử dụng hết số lần cho phép của mã này!" 
                        };
                    }
                }

                // Tính toán giảm giá
                decimal discount = 0;
                if (promotion.DiscountType == "Percentage")
                {
                    discount = Math.Round(orderTotal * promotion.DiscountValue / 100, 0);
                    if (promotion.MaxDiscountAmount.HasValue && discount > promotion.MaxDiscountAmount.Value)
                    {
                        discount = promotion.MaxDiscountAmount.Value;
                    }
                }
                else // FixedAmount
                {
                    discount = promotion.DiscountValue;
                }

                // Đảm bảo không giảm quá tổng đơn
                if (discount > orderTotal)
                {
                    discount = orderTotal;
                }

                return new PromotionValidationResult
                {
                    IsValid = true,
                    Message = $"Áp dụng thành công: Giảm {discount:N0}đ",
                    PromotionId = promotion.Id,
                    PromotionName = promotion.Name,
                    CalculatedDiscount = discount,
                    NewTotal = orderTotal - discount
                };
            }
            catch (Exception ex)
            {
                return new PromotionValidationResult 
                { 
                    IsValid = false, 
                    Message = "Lỗi kiểm tra mã: " + ex.Message 
                };
            }
        }

        #endregion

        #region Bill Management

        /// <summary>
        /// Lấy chi tiết hóa đơn
        /// </summary>
        public BillDetailViewModel GetBillDetail(int billId)
        {
            var bill = db.Bills
                .Include(b => b.Order.OrderDetails.Select(od => od.MenuItem))
                .Include(b => b.Order.Table.TableArea)
                .Include(b => b.Order.Employee)
                .Include(b => b.Employee)
                .Include(b => b.PromotionUsages).ThenInclude(pu => pu.Promotion)
                .FirstOrDefault(b => b.Id == billId);

            if (bill == null) return null;

            var settings = GetRestaurantSettings();
            var promotionUsage = bill.PromotionUsages.FirstOrDefault();

            return new BillDetailViewModel
            {
                BillId = bill.Id,
                InvoiceNumber = $"HD{bill.Id:D8}",
                BillDate = bill.BillDate,
                Status = bill.Status,

                OrderId = bill.OrderId,
                OrderCode = $"DH{bill.OrderId:D6}",
                OrderTime = bill.Order.OrderTime,

                TableNumber = bill.Order.Table?.TableNumber ?? "N/A",
                TableArea = bill.Order.Table?.TableArea?.Name ?? "",

                CashierName = bill.Cashier?.FullName ?? "N/A",
                WaiterName = bill.Order.Employee?.FullName ?? "N/A",

                Items = bill.Order.OrderDetails.Select(od => new BillItemViewModel
                {
                    Id = od.Id,
                    Name = od.MenuItem?.Name ?? "N/A",
                    Category = od.MenuItem?.Category ?? "",
                    Quantity = od.Quantity,
                    UnitPrice = od.PriceAtTime,
                    Total = od.Quantity * od.PriceAtTime,
                    Notes = od.Notes
                }).ToList(),

                SubTotal = bill.Order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime),
                VATPercent = GetVATPercent(),
                VATAmount = bill.TotalAmount - bill.Order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime),
                DiscountAmount = bill.DiscountAmount,
                DiscountCode = promotionUsage?.Promotion?.Code,
                DiscountDescription = promotionUsage?.Promotion?.Name,
                TotalAmount = bill.TotalAmount,
                FinalAmount = bill.FinalAmount,

                PaymentMethod = bill.PaymentMethod,
                PaymentMethodDisplay = GetPaymentMethodDisplay(bill.PaymentMethod),

                RestaurantName = settings.RestaurantName,
                RestaurantAddress = settings.RestaurantAddress,
                RestaurantPhone = settings.RestaurantPhone,
                RestaurantTaxCode = settings.TaxCode
            };
        }

        /// <summary>
        /// Lấy danh sách hóa đơn với filter
        /// </summary>
        public BillListViewModel GetBillList(BillFilterModel filter)
        {
            var query = db.Bills
                .Include(b => b.Order.OrderDetails)
                .Include(b => b.Order.Table)
                .Include(b => b.Employee)
                .AsQueryable();

            // Apply filters
            if (filter.FromDate.HasValue)
            {
                query = query.Where(b => b.BillDate >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateEnd = filter.ToDate.Value.AddDays(1);
                query = query.Where(b => b.BillDate < toDateEnd);
            }

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(b => b.Status == filter.Status);
            }

            if (!string.IsNullOrEmpty(filter.PaymentMethod))
            {
                query = query.Where(b => b.PaymentMethod == filter.PaymentMethod);
            }

            if (filter.CashierId.HasValue)
            {
                query = query.Where(b => b.CashierId == filter.CashierId.Value);
            }

            if (filter.ShiftId.HasValue)
            {
                query = query.Where(b => b.Order.ShiftId == filter.ShiftId.Value);
            }

            if (filter.MinAmount.HasValue)
            {
                query = query.Where(b => b.FinalAmount >= filter.MinAmount.Value);
            }

            if (filter.MaxAmount.HasValue)
            {
                query = query.Where(b => b.FinalAmount <= filter.MaxAmount.Value);
            }

            if (!string.IsNullOrEmpty(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(b => 
                    b.Order.Table.TableNumber.ToLower().Contains(search) ||
                    b.ReportedByEmployee?.FullName.ToLower().Contains(search));
            }

            // Statistics
            var statsQuery = query;
            var statistics = new BillStatistics
            {
                TotalBills = statsQuery.Count(),
                PaidBills = statsQuery.Count(b => b.Status == "Paid"),
                RefundedBills = statsQuery.Count(b => b.Status == "Refunded"),
                TotalRevenue = statsQuery.Where(b => b.Status == "Paid").Sum(b => (decimal?)b.FinalAmount) ?? 0,
                TotalDiscount = statsQuery.Sum(b => (decimal?)b.DiscountAmount) ?? 0
            };

            if (statistics.PaidBills > 0)
            {
                statistics.AverageOrderValue = statistics.TotalRevenue / statistics.PaidBills;
            }

            // Sorting
            switch (filter.SortBy?.ToLower())
            {
                case "amount":
                    query = filter.SortOrder == "asc" 
                        ? query.OrderBy(b => b.FinalAmount) 
                        : query.OrderByDescending(b => b.FinalAmount);
                    break;
                default:
                    query = filter.SortOrder == "asc" 
                        ? query.OrderBy(b => b.BillDate) 
                        : query.OrderByDescending(b => b.BillDate);
                    break;
            }

            // Pagination
            var totalRecords = query.Count();
            var totalPages = (int)Math.Ceiling((double)totalRecords / filter.PageSize);

            var bills = query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList()
                .Select(b => new BillSummaryItem
                {
                    Id = b.Id,
                    InvoiceNumber = $"HD{b.Id:D8}",
                    OrderId = b.OrderId,
                    OrderCode = $"DH{b.OrderId:D6}",
                    TableNumber = b.Order.Table?.TableNumber ?? "N/A",
                    BillDate = b.BillDate,
                    TotalAmount = b.TotalAmount,
                    FinalAmount = b.FinalAmount,
                    DiscountAmount = b.DiscountAmount,
                    PaymentMethod = GetPaymentMethodDisplay(b.PaymentMethod),
                    Status = b.Status,
                    CashierName = b.Cashier?.FullName ?? "N/A",
                    ItemCount = b.Order.OrderDetails.Sum(od => od.Quantity)
                }).ToList();

            return new BillListViewModel
            {
                Bills = bills,
                Statistics = statistics,
                Filter = filter,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                CurrentPage = filter.Page
            };
        }

        #endregion

        #region Refund

        /// <summary>
        /// Xử lý hoàn tiền
        /// </summary>
        public RefundResult ProcessRefund(RefundRequest request, int processedBy)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var bill = db.Bills
                        .Include(b => b.Order.OrderDetails)
                        .Include(b => b.ReturnBills)
                        .FirstOrDefault(b => b.Id == request.BillId);

                    if (bill == null)
                    {
                        return new RefundResult { Success = false, Message = "Không tìm thấy hóa đơn!" };
                    }

                    if (bill.Status == "Refunded")
                    {
                        return new RefundResult { Success = false, Message = "Hóa đơn đã được hoàn tiền trước đó!" };
                    }

                    decimal refundAmount;
                    if (request.IsFullRefund)
                    {
                        refundAmount = bill.FinalAmount;
                    }
                    else if (request.RefundAmount.HasValue)
                    {
                        refundAmount = request.RefundAmount.Value;
                    }
                    else
                    {
                        return new RefundResult { Success = false, Message = "Vui lòng nhập số tiền hoàn!" };
                    }

                    if (refundAmount > bill.FinalAmount)
                    {
                        return new RefundResult { Success = false, Message = "Số tiền hoàn không thể lớn hơn tổng hóa đơn!" };
                    }

                    // Tạo return bill
                    var returnBill = new ReturnBill
                    {
                        OriginalBillID = bill.Id,
                        ReturnDate = DateTime.Now,
                        TotalRefundAmount = refundAmount,
                        Reason = request.Reason,
                        EmployeeID = processedBy
                    };

                    db.ReturnBills.Add(returnBill);

                    // Cập nhật trạng thái bill
                    if (request.IsFullRefund)
                    {
                        bill.Status = "Refunded";
                    }
                    else
                    {
                        bill.Status = "PartialRefund";
                        bill.FinalAmount -= refundAmount;
                    }

                    // Cập nhật doanh thu ca (nếu cùng ca)
                    var activeShift = db.CashierShifts.FirstOrDefault(s => s.Status == "Active");
                    if (activeShift != null)
                    {
                        activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) - refundAmount;
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return new RefundResult
                    {
                        Success = true,
                        Message = "Hoàn tiền thành công!",
                        RefundId = returnBill.ReturnBillID,
                        RefundAmount = refundAmount,
                        RefundTime = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new RefundResult { Success = false, Message = "Có lỗi xảy ra: " + ex.Message };
                }
            }
        }

        #endregion

        #region Payment Page Data

        /// <summary>
        /// Lấy thông tin trang thanh toán
        /// </summary>
        public PaymentPageViewModel GetPaymentPageData(int orderId)
        {
            var order = db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.MenuItem)
                .Include(o => o.Table.TableArea)
                .Include(o => o.Employee)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) return null;

            var subTotal = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
            var vatPercent = GetVATPercent();
            var vatAmount = Math.Round(subTotal * vatPercent / 100, 0);
            var settings = GetRestaurantSettings();

            return new PaymentPageViewModel
            {
                Order = new OrderPaymentInfo
                {
                    OrderId = order.Id,
                    OrderCode = $"DH{order.Id:D6}",
                    TableNumber = order.Table?.TableNumber ?? "N/A",
                    TableArea = order.Table?.TableArea?.Name ?? "",
                    OrderTime = order.OrderTime,
                    WaiterName = order.Waiter?.FullName ?? "N/A",
                    Status = order.Status,
                    Items = order.OrderDetails.Select(od => new PaymentOrderItem
                    {
                        Id = od.Id,
                        Name = od.MenuItem?.Name ?? "N/A",
                        ImageUrl = od.MenuItem?.ImageUrl,
                        Quantity = od.Quantity,
                        UnitPrice = od.PriceAtTime,
                        Total = od.Quantity * od.PriceAtTime,
                        Notes = od.Notes
                    }).ToList(),
                    TotalItems = order.OrderDetails.Sum(od => od.Quantity),
                    SubTotal = subTotal
                },
                PaymentMethods = GetPaymentMethods(),
                AvailablePromotions = GetAvailablePromotions(subTotal),
                Summary = new PaymentSummary
                {
                    SubTotal = subTotal,
                    VATPercent = vatPercent,
                    VATAmount = vatAmount,
                    DiscountAmount = 0,
                    TotalAmount = subTotal + vatAmount
                },
                RestaurantName = settings.RestaurantName,
                RestaurantPhone = settings.RestaurantPhone
            };
        }

        #endregion

        #region Daily Summary

        /// <summary>
        /// Lấy tổng kết thanh toán theo ngày
        /// </summary>
        public DailyPaymentSummary GetDailyPaymentSummary(DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            var bills = db.Bills
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate && b.Status == "Paid")
                .ToList();

            var hourlyBreakdown = bills
                .GroupBy(b => b.BillDate.Hour)
                .Select(g => new HourlyRevenue
                {
                    Hour = g.Key,
                    HourDisplay = $"{g.Key:D2}:00",
                    Revenue = g.Sum(b => b.FinalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(h => h.Hour)
                .ToList();

            var paymentBreakdown = bills
                .GroupBy(b => b.PaymentMethod)
                .Select(g => new PaymentMethodSummary
                {
                    MethodName = GetPaymentMethodDisplay(g.Key),
                    TotalAmount = g.Sum(b => b.FinalAmount),
                    OrderCount = g.Count()
                })
                .ToList();

            var totalRevenue = bills.Sum(b => b.FinalAmount);
            foreach (var pm in paymentBreakdown)
            {
                pm.Percentage = totalRevenue > 0 ? (pm.TotalAmount / totalRevenue) * 100 : 0;
            }

            return new DailyPaymentSummary
            {
                Date = date,
                TotalBills = bills.Count,
                TotalRevenue = totalRevenue,
                TotalVAT = bills.Sum(b => b.TotalAmount - (b.TotalAmount / 1.1m)),
                TotalDiscount = bills.Sum(b => b.DiscountAmount),
                NetRevenue = totalRevenue - bills.Sum(b => b.DiscountAmount),
                AverageOrderValue = bills.Count > 0 ? totalRevenue / bills.Count : 0,
                PaymentBreakdown = paymentBreakdown,
                HourlyBreakdown = hourlyBreakdown
            };
        }

        #endregion

        #region Helper Methods

        private decimal GetVATPercent()
        {
            var vatSetting = db.AppSettings.FirstOrDefault(s => s.SettingKey == "DefaultVAT");
            if (vatSetting != null && decimal.TryParse(vatSetting.SettingValue, out decimal vat))
            {
                return vat;
            }
            return DEFAULT_VAT_PERCENT;
        }

        private string GenerateInvoiceNumber()
        {
            return $"HD{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
        }

        private string GetPaymentMethodDisplay(string method)
        {
            if (string.IsNullOrEmpty(method)) return "Chưa thanh toán";

            var methodMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cash", "Tiền mặt" },
                { "card", "Thẻ tín dụng" },
                { "transfer", "Chuyển khoản" },
                { "ewallet", "Ví điện tử" },
                { "momo", "MoMo" },
                { "vnpay", "VNPay" },
                { "zalopay", "ZaloPay" }
            };

            return methodMap.ContainsKey(method) ? methodMap[method] : method;
        }

        private List<PaymentMethodOption> GetPaymentMethods()
        {
            return new List<PaymentMethodOption>
            {
                new PaymentMethodOption
                {
                    Code = "cash",
                    Name = "Tiền mặt",
                    Icon = "fas fa-money-bill-wave",
                    Description = "Thanh toán bằng tiền mặt",
                    IsDefault = true,
                    IsEnabled = true
                },
                new PaymentMethodOption
                {
                    Code = "card",
                    Name = "Thẻ tín dụng/ghi nợ",
                    Icon = "fas fa-credit-card",
                    Description = "Visa, Mastercard, JCB",
                    IsEnabled = true
                },
                new PaymentMethodOption
                {
                    Code = "transfer",
                    Name = "Chuyển khoản",
                    Icon = "fas fa-university",
                    Description = "Chuyển khoản ngân hàng",
                    IsEnabled = true
                },
                new PaymentMethodOption
                {
                    Code = "momo",
                    Name = "MoMo",
                    Icon = "fas fa-mobile-alt",
                    Description = "Ví điện tử MoMo",
                    IsEnabled = true
                },
                new PaymentMethodOption
                {
                    Code = "vnpay",
                    Name = "VNPay",
                    Icon = "fas fa-qrcode",
                    Description = "Quét mã VNPay QR",
                    IsEnabled = true
                }
            };
        }

        private List<AvailablePromotion> GetAvailablePromotions(decimal orderTotal)
        {
            var now = DateTime.Now;

            return db.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
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
                    IsApplicable = orderTotal >= p.MinOrderValue &&
                                   (!p.MaxUsageCount.HasValue || p.UsedCount < p.MaxUsageCount.Value),
                    NotApplicableReason = orderTotal < p.MinOrderValue 
                        ? $"Đơn tối thiểu {p.MinOrderValue:N0}đ" 
                        : null
                })
                .ToList();
        }

        private (string RestaurantName, string RestaurantAddress, string RestaurantPhone, string TaxCode) GetRestaurantSettings()
        {
            var settings = db.AppSettings.ToList();
            return (
                settings.FirstOrDefault(s => s.SettingKey == "RestaurantName")?.SettingValue ?? "LDP Restaurant",
                settings.FirstOrDefault(s => s.SettingKey == "Address")?.SettingValue ?? "123 Đường ABC, Quận 1, TP.HCM",
                settings.FirstOrDefault(s => s.SettingKey == "PhoneNumber")?.SettingValue ?? "0123 456 789",
                settings.FirstOrDefault(s => s.SettingKey == "TaxCode")?.SettingValue ?? "0123456789"
            );
        }

        #endregion
    }
}
