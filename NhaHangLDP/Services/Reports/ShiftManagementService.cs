using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Reports
{
    public class ReportShiftService
    {
        private readonly NhaHangLDPEntities db;

        public ReportShiftService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public List<object> GetActiveShifts()
        {
            var activeShifts = db.CashierShift
                .Include(s => s.Employee)
                .Where(s => s.Status == "Active")
                .OrderBy(s => s.StartTime)
                .ToList();

            var shiftsData = new List<object>();

            foreach (var shift in activeShifts)
            {
                var orders = db.Order.Where(o => o.ShiftId == shift.Id).ToList();
                var revenue = 0m;
                var lastOrderTime = "Chưa có đơn";

                if (orders.Any())
                {
                    foreach (var order in orders)
                    {
                        var bills = db.Bill.Where(b => b.OrderId == order.Id && b.Status == "Paid").ToList();
                        revenue += bills.Sum(b => (decimal?)b.FinalAmount) ?? 0;
                    }
                    lastOrderTime = orders.Max(o => o.OrderTime).ToString("HH:mm");
                }

                shiftsData.Add(new
                {
                    ShiftId = shift.Id,
                    CashierName = shift.Employee?.FullName ?? "N/A",
                    StartTime = shift.StartTime.ToString("HH:mm"),
                    Duration = $"{(int)(DateTime.Now - shift.StartTime).TotalHours}h {(DateTime.Now - shift.StartTime).Minutes % 60}m",
                    OrdersCount = orders.Count,
                    Revenue = revenue,
                    LastOrderTime = lastOrderTime
                });
            }

            return shiftsData;
        }

        public async Task<DeleteShiftResult> DeleteShiftAsync(int shiftId)
        {
            try
            {
                var shift = await db.CashierShift.FirstOrDefaultAsync(s => s.Id == shiftId);

                if (shift == null)
                {
                    return new DeleteShiftResult { Success = false, ErrorMessage = "Không tìm thấy ca làm việc." };
                }

                if (shift.Status == "Active")
                {
                    return new DeleteShiftResult { Success = false, ErrorMessage = "Không thể xóa ca đang hoạt động." };
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var orders = db.Order.Where(o => o.ShiftId == shiftId).ToList();
                        foreach (var order in orders)
                        {
                            var bills = db.Bill.Where(b => b.OrderId == order.Id).ToList();
                            db.Bill.RemoveRange(bills);

                            var orderDetails = db.OrderDetail.Where(od => od.OrderId == order.Id).ToList();
                            db.OrderDetail.RemoveRange(orderDetails);

                            db.Order.Remove(order);
                        }

                        db.CashierShift.Remove(shift);

                        await db.SaveChangesAsync();
                        transaction.Commit();

                        return new DeleteShiftResult { Success = true };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new DeleteShiftResult { Success = false, ErrorMessage = "Lỗi khi xóa dữ liệu: " + ex.Message };
                    }
                }
            }
            catch (Exception ex)
            {
                return new DeleteShiftResult { Success = false, ErrorMessage = "Lỗi hệ thống: " + ex.Message };
            }
        }

        public object GetShiftPerformanceComparison(DateTime compareDate)
        {
            var startDate = compareDate.Date;
            var endDate = startDate.AddDays(1);

            var shifts = db.CashierShift
                .Include(s => s.Employee)
                .Where(s => s.StartTime >= startDate && s.StartTime < endDate)
                .ToList();

            var comparison = new List<object>();

            foreach (var shift in shifts)
            {
                var orders = db.Order.Where(o => o.ShiftId == shift.Id).ToList();
                var revenue = 0m;
                foreach (var order in orders)
                {
                    revenue += db.Bill.Where(b => b.OrderId == order.Id && b.Status == "Paid").Sum(b => (decimal?)b.FinalAmount) ?? 0;
                }

                comparison.Add(new
                {
                    ShiftName = GetShiftDisplayName(shift.StartTime),
                    CashierName = shift.Employee?.FullName ?? "N/A",
                    Revenue = revenue,
                    OrdersCount = orders.Count,
                    AverageOrderValue = orders.Count > 0 ? revenue / orders.Count : 0,
                    Duration = shift.EndTime.HasValue ?
                        (int)(shift.EndTime.Value - shift.StartTime).TotalMinutes :
                        (int)(DateTime.Now - shift.StartTime).TotalMinutes
                });
            }

            return comparison;
        }

        public object SendCashierAlert(int shiftId, string alertType, string message)
        {
            var shift = db.CashierShift.Include(s => s.Employee).FirstOrDefault(s => s.Id == shiftId);
            if (shift == null)
                return null;

            return new
            {
                ShiftId = shiftId,
                CashierName = shift.Employee?.FullName,
                AlertType = alertType,
                Message = message,
                Timestamp = DateTime.Now,
                Status = "Sent"
            };
        }

        public object GetShiftCashierLink(int shiftId)
        {
            var shift = db.CashierShift.Include(s => s.Employee).FirstOrDefault(s => s.Id == shiftId);
            if (shift == null)
                return null;

            return new
            {
                Id = shift.Id,
                CashierName = shift.Employee?.FullName,
                StartTime = shift.StartTime.ToString("dd/MM/yyyy HH:mm"),
                Status = shift.Status
            };
        }

        public List<string> GetCashierNames()
        {
            return db.Employee
                .Where(e => e.Role.RoleName == "Cashier" || e.Role.RoleName == "Manager")
                .Select(e => e.FullName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        private string GetShiftDisplayName(DateTime startTime)
        {
            var hour = startTime.Hour;
            if (hour >= 6 && hour < 14) return "Ca Sáng";
            if (hour >= 14 && hour < 22) return "Ca Chiều";
            return "Ca Tối";
        }
    }

    public class DeleteShiftResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
