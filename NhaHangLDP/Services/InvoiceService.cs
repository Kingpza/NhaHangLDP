using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Text;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service quản lý hóa đơn
    /// </summary>
    public class InvoiceService
    {
        private readonly MyDbContext db;

        public InvoiceService(MyDbContext context)
        {
            db = context;
        }

        #region Invoice Generation

        /// <summary>
        /// Tạo hóa đơn từ đơn hàng
        /// </summary>
        public InvoiceViewModel GenerateInvoice(int orderId)
        {
            var order = db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.MenuItem)
                .Include(o => o.Table)
                .Include(o => o.Bills).ThenInclude(b => b.Cashier)
                .Include(o => o.Waiter)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) return null;

            var bill = order.Bills.FirstOrDefault(b => b.Status == "Paid");
            var subtotal = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
            var vatPercent = GetVATPercent();
            var vat = Math.Round(subtotal * vatPercent / 100, 0);
            var total = subtotal + vat;

            var settings = GetRestaurantSettings();

            return new InvoiceViewModel
            {
                OrderId = order.Id.ToString("D6"),
                BillId = bill?.Id.ToString() ?? "N/A",
                TableNumber = order.Table?.TableNumber ?? "N/A",
                OrderTime = order.OrderTime,
                BillDate = bill?.BillDate ?? DateTime.Now,
                CashierName = bill?.Cashier?.FullName ?? order.Waiter?.FullName ?? "N/A",
                PaymentMethod = GetPaymentMethodText(bill?.PaymentMethod ?? "Chưa thanh toán"),
                Items = order.OrderDetails.Select(od => new InvoiceItemViewModel
                {
                    ItemName = od.MenuItem?.Name ?? "N/A",
                    Quantity = od.Quantity,
                    Price = od.PriceAtTime,
                    Total = od.Quantity * od.PriceAtTime
                }).ToList(),
                Subtotal = subtotal,
                VAT = vat,
                TotalAmount = bill?.FinalAmount ?? total,
                RestaurantName = settings.RestaurantName,
                RestaurantAddress = settings.RestaurantAddress,
                RestaurantPhone = settings.RestaurantPhone
            };
        }

        /// <summary>
        /// Tạo hóa đơn chi tiết (đầy đủ thông tin)
        /// </summary>
        public BillDetailViewModel GenerateDetailedInvoice(int billId)
        {
            var bill = db.Bills
                .Include(b => b.Order).ThenInclude(o => o.OrderDetails).ThenInclude(od => od.MenuItem)
                .Include(b => b.Order.Table).ThenInclude(t => t.TableArea)
                .Include(b => b.Order.Waiter)
                .Include(b => b.Cashier)
                .Include(b => b.PromotionUsages).ThenInclude(pu => pu.Promotion)
                .FirstOrDefault(b => b.Id == billId);

            if (bill == null) return null;

            var settings = GetRestaurantSettings();
            var subtotal = bill.Order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
            var vatAmount = bill.TotalAmount - subtotal;
            var promotionUsage = bill.PromotionUsages.FirstOrDefault();

            return new BillDetailViewModel
            {
                BillId = bill.Id,
                InvoiceNumber = GenerateInvoiceNumber(bill.Id, bill.BillDate),
                BillDate = bill.BillDate,
                Status = bill.Status,

                OrderId = bill.OrderId,
                OrderCode = $"DH{bill.OrderId:D6}",
                OrderTime = bill.Order.OrderTime,

                TableNumber = bill.Order.Table?.TableNumber ?? "N/A",
                TableArea = bill.Order.Table?.TableArea?.Name ?? "",

                CashierName = bill.Cashier?.FullName ?? "N/A",
                WaiterName = bill.Order.Waiter?.FullName ?? "N/A",

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

                SubTotal = subtotal,
                VATPercent = GetVATPercent(),
                VATAmount = vatAmount,
                DiscountAmount = bill.DiscountAmount,
                DiscountCode = promotionUsage?.Promotion?.Code,
                DiscountDescription = promotionUsage?.Promotion?.Name,
                TotalAmount = bill.TotalAmount,
                FinalAmount = bill.FinalAmount,

                PaymentMethod = bill.PaymentMethod,
                PaymentMethodDisplay = GetPaymentMethodText(bill.PaymentMethod),

                RestaurantName = settings.RestaurantName,
                RestaurantAddress = settings.RestaurantAddress,
                RestaurantPhone = settings.RestaurantPhone,
                RestaurantTaxCode = settings.TaxCode,

                QRCodeData = GenerateQRCodeData(bill)
            };
        }

        #endregion

        #region Invoice History

        /// <summary>
        /// Lấy lịch sử hóa đơn của khách hàng
        /// </summary>
        public List<BillSummaryItem> GetCustomerInvoiceHistory(string customerPhone, int take = 10)
        {
            // Tìm các hóa đơn từ QROrder có phone matching
            var qrOrders = db.QROrders
                .Include(q => q.Table)
                .Where(q => q.CustomerPhone == customerPhone && q.LinkedOrderId.HasValue)
                .Select(q => q.LinkedOrderId.Value)
                .ToList();

            var bills = db.Bills
                .Include(b => b.Order.OrderDetails)
                .Include(b => b.Order.Table)
                .Include(b => b.Cashier)
                .Where(b => qrOrders.Contains(b.OrderId))
                .OrderByDescending(b => b.BillDate)
                .Take(take)
                .ToList();

            return bills.Select(b => new BillSummaryItem
            {
                Id = b.Id,
                InvoiceNumber = GenerateInvoiceNumber(b.Id, b.BillDate),
                OrderId = b.OrderId,
                OrderCode = $"DH{b.OrderId:D6}",
                TableNumber = b.Order.Table?.TableNumber ?? "N/A",
                BillDate = b.BillDate,
                TotalAmount = b.TotalAmount,
                FinalAmount = b.FinalAmount,
                DiscountAmount = b.DiscountAmount,
                PaymentMethod = GetPaymentMethodText(b.PaymentMethod),
                Status = b.Status,
                CashierName = b.Cashier?.FullName ?? "N/A",
                ItemCount = b.Order.OrderDetails.Sum(od => od.Quantity)
            }).ToList();
        }

        /// <summary>
        /// Lấy hóa đơn theo ca làm việc
        /// </summary>
        public List<BillSummaryItem> GetShiftInvoices(int shiftId)
        {
            var bills = db.Bills
                .Include(b => b.Order.OrderDetails)
                .Include(b => b.Order.Table)
                .Include(b => b.Cashier)
                .Where(b => b.Order.ShiftId == shiftId)
                .OrderByDescending(b => b.BillDate)
                .ToList();

            return bills.Select(b => new BillSummaryItem
            {
                Id = b.Id,
                InvoiceNumber = GenerateInvoiceNumber(b.Id, b.BillDate),
                OrderId = b.OrderId,
                OrderCode = $"DH{b.OrderId:D6}",
                TableNumber = b.Order.Table?.TableNumber ?? "N/A",
                BillDate = b.BillDate,
                TotalAmount = b.TotalAmount,
                FinalAmount = b.FinalAmount,
                DiscountAmount = b.DiscountAmount,
                PaymentMethod = GetPaymentMethodText(b.PaymentMethod),
                Status = b.Status,
                CashierName = b.Cashier?.FullName ?? "N/A",
                ItemCount = b.Order.OrderDetails.Sum(od => od.Quantity)
            }).ToList();
        }

        /// <summary>
        /// Lấy hóa đơn theo ngày
        /// </summary>
        public List<BillSummaryItem> GetDailyInvoices(DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            var bills = db.Bills
                .Include(b => b.Order.OrderDetails)
                .Include(b => b.Order.Table)
                .Include(b => b.Cashier)
                .Where(b => b.BillDate >= startDate && b.BillDate < endDate)
                .OrderByDescending(b => b.BillDate)
                .ToList();

            return bills.Select(b => new BillSummaryItem
            {
                Id = b.Id,
                InvoiceNumber = GenerateInvoiceNumber(b.Id, b.BillDate),
                OrderId = b.OrderId,
                OrderCode = $"DH{b.OrderId:D6}",
                TableNumber = b.Order.Table?.TableNumber ?? "N/A",
                BillDate = b.BillDate,
                TotalAmount = b.TotalAmount,
                FinalAmount = b.FinalAmount,
                DiscountAmount = b.DiscountAmount,
                PaymentMethod = GetPaymentMethodText(b.PaymentMethod),
                Status = b.Status,
                CashierName = b.Cashier?.FullName ?? "N/A",
                ItemCount = b.Order.OrderDetails.Sum(od => od.Quantity)
            }).ToList();
        }

        #endregion

        #region Invoice Search

        /// <summary>
        /// Tìm kiếm hóa đơn
        /// </summary>
        public List<BillSummaryItem> SearchInvoices(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<BillSummaryItem>();

            keyword = keyword.Trim().ToLower();

            // Tìm theo số hóa đơn
            if (keyword.StartsWith("hd") && int.TryParse(keyword.Replace("hd", ""), out int billId))
            {
                var bill = db.Bills
                    .Include(b => b.Order.OrderDetails)
                    .Include(b => b.Order.Table)
                    .Include(b => b.Cashier)
                    .FirstOrDefault(b => b.Id == billId);

                if (bill != null)
                {
                    return new List<BillSummaryItem>
                    {
                        MapToBillSummary(bill)
                    };
                }
            }

            // Tìm theo mã đơn hàng
            if (keyword.StartsWith("dh") && int.TryParse(keyword.Replace("dh", ""), out int orderId))
            {
                var bills = db.Bills
                    .Include(b => b.Order.OrderDetails)
                    .Include(b => b.Order.Table)
                    .Include(b => b.Cashier)
                    .Where(b => b.OrderId == orderId)
                    .ToList();

                return bills.Select(MapToBillSummary).ToList();
            }

            // Tìm theo số bàn hoặc tên nhân viên
            var results = db.Bills
                .Include(b => b.Order.OrderDetails)
                .Include(b => b.Order.Table)
                .Include(b => b.Cashier)
                .Where(b => 
                    b.Order.Table.TableNumber.ToLower().Contains(keyword) ||
                    (b.Cashier != null && b.Cashier.FullName.ToLower().Contains(keyword)))
                .OrderByDescending(b => b.BillDate)
                .Take(20)
                .ToList();

            return results.Select(MapToBillSummary).ToList();
        }

        #endregion

        #region Invoice Statistics

        /// <summary>
        /// Thống kê hóa đơn theo khoảng thời gian
        /// </summary>
        public BillStatistics GetInvoiceStatistics(DateTime fromDate, DateTime toDate)
        {
            var endDate = toDate.AddDays(1);

            var bills = db.Bills
                .Where(b => b.BillDate >= fromDate && b.BillDate < endDate)
                .ToList();

            var paidBills = bills.Where(b => b.Status == "Paid").ToList();

            return new BillStatistics
            {
                TotalBills = bills.Count,
                PaidBills = paidBills.Count,
                RefundedBills = bills.Count(b => b.Status == "Refunded" || b.Status == "PartialRefund"),
                TotalRevenue = paidBills.Sum(b => b.FinalAmount),
                TotalDiscount = bills.Sum(b => b.DiscountAmount),
                TotalVAT = paidBills.Sum(b => b.TotalAmount - (b.Order?.OrderDetails?.Sum(od => od.Quantity * od.PriceAtTime) ?? 0)),
                AverageOrderValue = paidBills.Count > 0 ? paidBills.Sum(b => b.FinalAmount) / paidBills.Count : 0,
                RevenueByPaymentMethod = paidBills
                    .GroupBy(b => b.PaymentMethod ?? "Unknown")
                    .ToDictionary(
                        g => GetPaymentMethodText(g.Key),
                        g => g.Sum(b => b.FinalAmount)
                    )
            };
        }

        /// <summary>
        /// Top món bán chạy từ hóa đơn
        /// </summary>
        public List<TopSellingItem> GetTopSellingItems(DateTime fromDate, DateTime toDate, int top = 10)
        {
            var endDate = toDate.AddDays(1);

            return db.Bills
                .Where(b => b.BillDate >= fromDate && b.BillDate < endDate && b.Status == "Paid")
                .SelectMany(b => b.Order.OrderDetails)
                .GroupBy(od => new { od.MenuItemId, od.MenuItem.Name, od.MenuItem.Category })
                .Select(g => new TopSellingItem
                {
                    MenuItemId = g.Key.MenuItemId,
                    Name = g.Key.Name,
                    Category = g.Key.Category,
                    TotalQuantity = g.Sum(od => od.Quantity),
                    TotalRevenue = g.Sum(od => od.Quantity * od.PriceAtTime)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(top)
                .ToList();
        }

        #endregion

        #region Print/Export

        /// <summary>
        /// Tạo HTML cho in hóa đơn
        /// </summary>
        public string GeneratePrintableInvoiceHtml(int billId)
        {
            var invoice = GenerateDetailedInvoice(billId);
            if (invoice == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'/>");
            sb.AppendLine($"<title>Hóa đơn {invoice.InvoiceNumber}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; font-size: 10pt; width: 76mm; margin: 0 auto; }");
            sb.AppendLine(".header { text-align: center; margin-bottom: 10px; }");
            sb.AppendLine(".header h3 { margin: 0; font-size: 14pt; }");
            sb.AppendLine(".title { text-align: center; font-size: 12pt; font-weight: bold; border-top: 1px dashed #000; border-bottom: 1px dashed #000; padding: 5px 0; margin: 10px 0; }");
            sb.AppendLine(".info { font-size: 9pt; margin-bottom: 10px; }");
            sb.AppendLine(".info-row { display: flex; justify-content: space-between; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; font-size: 9pt; }");
            sb.AppendLine("th, td { padding: 4px; border-bottom: 1px dashed #ccc; }");
            sb.AppendLine("th { text-align: left; border-top: 1px dashed #000; }");
            sb.AppendLine(".text-right { text-align: right; }");
            sb.AppendLine(".total-section { margin-top: 10px; }");
            sb.AppendLine(".total-row { display: flex; justify-content: space-between; padding: 3px 0; }");
            sb.AppendLine(".grand-total { font-size: 12pt; font-weight: bold; border-top: 1px solid #000; padding-top: 5px; }");
            sb.AppendLine(".footer { text-align: center; margin-top: 15px; border-top: 1px dashed #000; padding-top: 10px; }");
            sb.AppendLine("</style></head><body>");

            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<h3>{invoice.RestaurantName}</h3>");
            sb.AppendLine($"<p>{invoice.RestaurantAddress}</p>");
            sb.AppendLine($"<p>ĐT: {invoice.RestaurantPhone}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='title'>HÓA ĐƠN THANH TOÁN</div>");

            // Info
            sb.AppendLine("<div class='info'>");
            sb.AppendLine($"<div class='info-row'><span>Số HĐ: {invoice.InvoiceNumber}</span><span>Ngày: {invoice.BillDate:dd/MM/yy}</span></div>");
            sb.AppendLine($"<div class='info-row'><span>Mã Đơn: {invoice.OrderCode}</span><span>Giờ: {invoice.BillDate:HH:mm}</span></div>");
            sb.AppendLine($"<p>Thu ngân: {invoice.CashierName}</p>");
            sb.AppendLine($"<p>Bàn: {invoice.TableNumber}</p>");
            sb.AppendLine("</div>");

            // Items
            sb.AppendLine("<table><thead><tr><th>Tên món</th><th class='text-center'>SL</th><th class='text-right'>Đ.Giá</th><th class='text-right'>T.Tiền</th></tr></thead><tbody>");
            foreach (var item in invoice.Items)
            {
                sb.AppendLine($"<tr><td colspan='4'>{item.Name}</td></tr>");
                sb.AppendLine($"<tr><td></td><td class='text-center'>{item.Quantity}</td><td class='text-right'>{item.UnitPrice:N0}</td><td class='text-right'>{item.Total:N0}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");

            // Totals
            sb.AppendLine("<div class='total-section'>");
            sb.AppendLine($"<div class='total-row'><span>Tạm tính:</span><span>{invoice.SubTotal:N0} VNĐ</span></div>");
            sb.AppendLine($"<div class='total-row'><span>Thuế VAT ({invoice.VATPercent}%):</span><span>{invoice.VATAmount:N0} VNĐ</span></div>");
            if (invoice.DiscountAmount > 0)
            {
                sb.AppendLine($"<div class='total-row'><span>Giảm giá:</span><span>-{invoice.DiscountAmount:N0} VNĐ</span></div>");
            }
            sb.AppendLine($"<div class='total-row grand-total'><span>TỔNG CỘNG:</span><span>{invoice.FinalAmount:N0} VNĐ</span></div>");
            sb.AppendLine($"<div class='total-row'><span>Hình thức:</span><span>{invoice.PaymentMethodDisplay}</span></div>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>Cảm ơn quý khách!</p>");
            sb.AppendLine("<p>Hẹn gặp lại!</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body></html>");

            return sb.ToString();
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
            return 10m;
        }

        private string GenerateInvoiceNumber(int billId, DateTime billDate)
        {
            return $"HD{billDate:yyyyMMdd}{billId:D4}";
        }

        private string GenerateQRCodeData(Bill bill)
        {
            // Format theo chuẩn thanh toán QR (có thể mở rộng sau)
            return $"HD{bill.Id:D8}|{bill.FinalAmount:F0}|{bill.BillDate:yyyyMMddHHmmss}";
        }

        private string GetPaymentMethodText(string method)
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

        private BillSummaryItem MapToBillSummary(Bill b)
        {
            return new BillSummaryItem
            {
                Id = b.Id,
                InvoiceNumber = GenerateInvoiceNumber(b.Id, b.BillDate),
                OrderId = b.OrderId,
                OrderCode = $"DH{b.OrderId:D6}",
                TableNumber = b.Order?.Table?.TableNumber ?? "N/A",
                BillDate = b.BillDate,
                TotalAmount = b.TotalAmount,
                FinalAmount = b.FinalAmount,
                DiscountAmount = b.DiscountAmount,
                PaymentMethod = GetPaymentMethodText(b.PaymentMethod),
                Status = b.Status,
                CashierName = b.Cashier?.FullName ?? "N/A",
                ItemCount = b.Order?.OrderDetails?.Sum(od => od.Quantity) ?? 0
            };
        }

        #endregion
    }

    /// <summary>
    /// Top món bán chạy
    /// </summary>
    public class TopSellingItem
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
