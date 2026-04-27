using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class ReportService
    {
        private readonly NhaHangLDPEntities db;

        public ReportService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public ShiftRevenueReportViewModel GenerateShiftReport(int shiftId)
        {
            var shift = db.CashierShift
                .Include(s => s.Employee)
                .Include(s => s.ShiftSupportStaff.Select(ss => ss.Employee))
                .FirstOrDefault(s => s.Id == shiftId);

            if (shift == null)
            {
                return null;
            }

            var billQuery = db.Bill.Where(b => b.Order.ShiftId == shiftId && b.Status == "Paid");

            var totalRevenue = billQuery.Sum(b => (decimal?)b.FinalAmount) ?? 0;
            var totalOrders = db.Order.Count(o => o.ShiftId == shiftId);
            var completedOrders = db.Order.Count(o => o.ShiftId == shiftId && o.Status == "Completed");
            var cancelledOrders = db.Order.Count(o => o.ShiftId == shiftId && o.Status == "Cancelled");

            var paymentMethods = billQuery
                .GroupBy(b => b.PaymentMethod)
                .Select(g => new PaymentMethodSummary
                {
                    MethodName = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(b => b.FinalAmount)
                })
                .ToList();

            paymentMethods.ForEach(p =>
                p.Percentage = (totalRevenue > 0) ? (p.TotalAmount / totalRevenue) * 100 : 0
            );

            var topOrders = db.Bill
                .Where(b => b.Order.ShiftId == shiftId && b.Order.Status == "Completed")
                .OrderByDescending(b => b.FinalAmount)
                .Take(10)
                .Select(b => new OrderSummary
                {
                    OrderId = b.OrderId.ToString(),
                    TableName = b.Order.RestaurantTable.TableNumber,
                    OrderTime = b.Order.OrderTime,
                    PaymentMethod = b.PaymentMethod,
                    TotalAmount = b.FinalAmount
                })
                .ToList();

            var supportNames = shift.ShiftSupportStaff
                                    .Select(ss => ss.Employee.FullName)
                                    .ToList();

            var appSettings = db.AppSetting.ToList();

            var viewModel = new ShiftRevenueReportViewModel
            {
                ShiftId = shift.Id.ToString(),
                CashierName = shift.Employee.FullName,
                SupportStaffNames = supportNames,
                ShiftStartTime = shift.StartTime,
                ShiftEndTime = shift.EndTime ?? DateTime.Now,
                OpeningAmount = shift.InitialCash,
                CashInHand = shift.FinalCash ?? (shift.InitialCash + paymentMethods.FirstOrDefault(p => p.MethodName.ToLower() == "cash")?.TotalAmount ?? 0),
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                PaymentMethods = paymentMethods,
                TopOrders = topOrders,
                RestaurantName = appSettings.FirstOrDefault(a => a.SettingKey == "RestaurantName")?.SettingValue ?? "Hỷ Lạc Hotpot POS System",
                RestaurantAddress = appSettings.FirstOrDefault(a => a.SettingKey == "Address")?.SettingValue ?? "N/A",
                RestaurantPhone = appSettings.FirstOrDefault(a => a.SettingKey == "PhoneNumber")?.SettingValue ?? "N/A",
                RestaurantTaxCode = "0123456789"
            };

            return viewModel;
        }

        public InvoiceViewModel GenerateInvoice(int orderId)
        {
            var order = db.Order
                .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                .Include(o => o.RestaurantTable)
                .Include(o => o.Bill)
                .Include(o => o.Employee)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return null;
            }

            var bill = order.Bill.FirstOrDefault(b => b.Status == "Paid");
            var subtotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
            var vat = Math.Round(subtotal * 0.1m);
            var total = subtotal + vat;

            var invoiceViewModel = new InvoiceViewModel
            {
                OrderId = order.Id.ToString("D6"),
                BillId = bill?.Id.ToString() ?? "N/A",
                TableNumber = order.RestaurantTable.TableNumber,
                OrderTime = order.OrderTime,
                BillDate = bill?.BillDate ?? DateTime.Now,
                CashierName = order.Employee?.FullName ?? (bill?.Employee?.FullName ?? "N/A"),
                PaymentMethod = GetPaymentMethodText(bill?.PaymentMethod ?? "Chưa thanh toán"),
                Items = order.OrderDetail.Select(od => new InvoiceItemViewModel
                {
                    ItemName = od.MenuItem.Name,
                    Quantity = od.Quantity,
                    Price = od.PriceAtTime,
                    Total = od.Quantity * od.PriceAtTime
                }).ToList(),
                Subtotal = subtotal,
                VAT = vat,
                TotalAmount = bill?.FinalAmount ?? total
            };

            return invoiceViewModel;
        }

        private string GetPaymentMethodText(string method)
        {
            var methodMap = new Dictionary<string, string>
            {
                { "cash", "Tiền mặt" },
                { "card", "Thẻ tín dụng" },
                { "transfer", "Chuyển khoản" },
                { "ewallet", "Ví điện tử" }
            };
            return methodMap.ContainsKey(method) ? methodMap[method] : method;
        }
    }
}
