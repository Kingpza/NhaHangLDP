using System;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class PaymentService
    {
        private readonly NhaHangLDPEntities db;

        public PaymentService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public bool ProcessPayment(int orderId, string paymentMethod, decimal receivedAmount, int cashierId, int shiftId, out int billId, out decimal changeAmount, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.Order
                        .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                        .Include(o => o.RestaurantTable)
                        .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        errorMessage = "Không tìm thấy đơn hàng!";
                        billId = 0;
                        changeAmount = 0;
                        return false;
                    }

                    var calculatedTotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                    var vat = calculatedTotal * 0.1m;
                    var finalAmount = calculatedTotal + vat;

                    if (paymentMethod == "cash" && receivedAmount < finalAmount)
                    {
                        errorMessage = "Số tiền nhận không đủ để thanh toán!";
                        billId = 0;
                        changeAmount = 0;
                        return false;
                    }

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

                    db.Bill.Add(bill);
                    order.Status = "Completed";
                    order.RestaurantTable.Status = "Available";

                    var activeShift = db.CashierShift.FirstOrDefault(s => s.Id == shiftId);
                    if (activeShift != null)
                    {
                        activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + finalAmount;
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
    }
}
