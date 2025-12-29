using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class ShiftManagementService
    {
        private readonly MyDbContext db;

        public ShiftManagementService(MyDbContext context)
        {
            db = context;
        }

        public CashierShift GetActiveShift()
        {
            return db.CashierShifts.FirstOrDefault(s => s.Status == "Active");
        }

        public CashierShift GetActiveShiftByCashier(int cashierId)
        {
            return db.CashierShifts.FirstOrDefault(s => s.CashierId == cashierId && s.Status == "Active");
        }

        public bool StartShift(int cashierId, decimal openingAmount, string notes, out string errorMessage)
        {
            try
            {
                if (openingAmount <= 0)
                {
                    errorMessage = "Số tiền mặt đầu ca phải lớn hơn 0!";
                    return false;
                }

                var existingShift = GetActiveShift();
                if (existingShift != null)
                {
                    errorMessage = "Đã có ca làm việc đang hoạt động!";
                    return false;
                }

                var cashierShift = new CashierShift
                {
                    CashierId = cashierId,
                    StartTime = DateTime.Now,
                    InitialCash = openingAmount,
                    Status = "Active",
                    TotalRevenue = 0
                };

                db.CashierShifts.Add(cashierShift);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                return false;
            }
        }

        public bool CloseShift(out object shiftSummary, out string errorMessage)
        {
            try
            {
                var activeShift = db.CashierShifts
                    .Include(s => s.ShiftSupportStaffs)
                    .FirstOrDefault(s => s.Status == "Active");

                if (activeShift == null)
                {
                    errorMessage = "Không có ca làm việc nào đang hoạt động!";
                    shiftSummary = null;
                    return false;
                }

                var shiftRevenue = CalculateShiftRevenue(activeShift.Id);

                activeShift.EndTime = DateTime.Now;
                activeShift.FinalCash = activeShift.InitialCash + shiftRevenue.CashRevenue;
                activeShift.TotalRevenue = shiftRevenue.TotalRevenue;
                activeShift.Status = "Closed";

                db.SaveChanges();

                shiftSummary = new
                {
                    TotalOrders = shiftRevenue.OrderCount,
                    TotalRevenue = shiftRevenue.TotalRevenue,
                    CashInHand = activeShift.FinalCash,
                    ShiftId = activeShift.Id
                };

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                shiftSummary = null;
                return false;
            }
        }

        public ShiftRevenueData CalculateShiftRevenue(int shiftId)
        {
            var orders = db.Orders
                .Include(o => o.Bills)
                .Where(o => o.ShiftId == shiftId)
                .ToList();

            var totalRevenue = orders
                .Where(o => o.Bills.Any(b => b.Status == "Paid"))
                .Sum(o => o.Bills.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount));

            var cashRevenue = orders
                .Where(o => o.Bills.Any(b => b.Status == "Paid" && b.PaymentMethod == "cash"))
                .Sum(o => o.Bills.Where(b => b.Status == "Paid" && b.PaymentMethod == "cash").Sum(b => b.FinalAmount));

            return new ShiftRevenueData
            {
                TotalRevenue = totalRevenue,
                CashRevenue = cashRevenue,
                OrderCount = orders.Count(o => o.Bills.Any(b => b.Status == "Paid"))
            };
        }

        public class ShiftRevenueData
        {
            public decimal TotalRevenue { get; set; }
            public decimal CashRevenue { get; set; }
            public int OrderCount { get; set; }
        }
    }
}
