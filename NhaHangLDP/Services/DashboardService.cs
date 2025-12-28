using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class DashboardService
    {
        private readonly NhaHangLDPEntities db;

        public DashboardService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public CashierDashboardViewModel GetDashboardData(int shiftId)
        {
            var activeShift = db.CashierShift
                .Include(cs => cs.Employee)
                .Include(s => s.ShiftSupportStaff.Select(ss => ss.Employee))
                .FirstOrDefault(cs => cs.Id == shiftId && cs.Status == "Active");

            if (activeShift == null)
            {
                return null;
            }

            var viewModel = new CashierDashboardViewModel
            {
                ActiveOrderCount = db.Order.Count(o =>
                    (o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                    && o.ShiftId == activeShift.Id
                ),
                PendingOrderCount = db.Order.Count(o =>
                    o.Status == "Pending"
                    && o.ShiftId == activeShift.Id
                ),
                TotalOrdersToday = db.Order.Count(o => o.ShiftId == activeShift.Id),
                TodayRevenue = db.Bill
                    .Where(b => b.Order.ShiftId == activeShift.Id && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,
                OccupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied"),
                TotalTables = db.RestaurantTable.Count(),
                ReservedTables = db.RestaurantTable.Count(t => t.Status == "Reserved"),
                AvailableTables = db.RestaurantTable.Count(t => t.Status == "Available"),
                ActiveShift = activeShift,
                ShiftStartTime = activeShift.StartTime,
                HasActiveShift = true,
                CashierName = activeShift.Employee?.FullName ?? "Thu Ngân"
            };

            return viewModel;
        }

        public object GetDashboardStats(int shiftId)
        {
            var stats = new
            {
                ActiveOrderCount = db.Order.Count(o => 
                    (o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready") 
                    && o.ShiftId == shiftId),
                PendingOrderCount = db.Order.Count(o => 
                    o.Status == "Pending" && o.ShiftId == shiftId),
                TotalOrdersToday = db.Order.Count(o => o.ShiftId == shiftId),
                TodayRevenue = db.Bill
                    .Where(b => b.Order.ShiftId == shiftId && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,
                OccupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied"),
                TotalTables = db.RestaurantTable.Count()
            };

            return stats;
        }

        public TableAreasViewModel GetTableAreasData()
        {
            var tableAreasWithTables = db.TableArea
                .Include(a => a.RestaurantTable.Select(t => t.Order.Select(o => o.OrderDetail)))
                .OrderBy(a => a.Name)
                .ToList();

            var viewModel = new TableAreasViewModel
            {
                LastUpdated = DateTime.Now
            };

            foreach (var area in tableAreasWithTables)
            {
                var areaViewModel = new TableAreaViewModel
                {
                    Id = area.Id,
                    Name = area.Name
                };

                foreach (var table in area.RestaurantTable.OrderBy(t => t.TableNumber))
                {
                    var activeOrder = table.Order
                        .Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                        .OrderByDescending(o => o.OrderTime)
                        .FirstOrDefault();

                    var tableViewModel = new TableViewModel
                    {
                        Id = table.Id,
                        TableNumber = table.TableNumber,
                        Capacity = table.Capacity,
                        Status = table.Status,
                        ActiveOrderId = activeOrder?.Id,
                        OrderTime = activeOrder?.OrderTime,
                        OrderTimeDisplay = activeOrder?.OrderTime.ToString("HH:mm"),
                        ItemCount = activeOrder?.OrderDetail.Count ?? 0,
                        CustomerCount = 0,
                        StatusDisplay = table.Status == "Available" ? "Trống" :
                                       table.Status == "Occupied" ? "Có khách" :
                                       table.Status == "Reserved" ? "Đã đặt" : "Không xác định",
                        StatusClass = table.Status?.ToLower() ?? "unavailable"
                    };

                    areaViewModel.Tables.Add(tableViewModel);
                }

                viewModel.TableAreas.Add(areaViewModel);
            }

            var allTables = viewModel.TableAreas.SelectMany(a => a.Tables).ToList();
            viewModel.TotalTables = allTables.Count;
            viewModel.AvailableTables = allTables.Count(t => t.Status == "Available");
            viewModel.OccupiedTables = allTables.Count(t => t.Status == "Occupied");
            viewModel.ReservedTables = allTables.Count(t => t.Status == "Reserved");

            return viewModel;
        }

        public ShiftDetailsViewModel GetShiftDetails(int shiftId)
        {
            var shift = db.CashierShift
                .Include(s => s.Employee)
                .FirstOrDefault(s => s.Id == shiftId);

            if (shift == null)
            {
                return null;
            }

            var ordersInShift = db.Order
                .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                .Include(o => o.RestaurantTable)
                .Include(o => o.Bill)
                .Include(o => o.Employee)
                .Where(o => o.ShiftId == shift.Id)
                .OrderByDescending(o => o.OrderTime)
                .ToList();

            var viewModel = new ShiftDetailsViewModel
            {
                ShiftInfo = shift,
                TotalOrders = ordersInShift.Count,
                CompletedOrders = ordersInShift.Count(o => o.Status == "Completed"),
                CancelledOrders = ordersInShift.Count(o => o.Status == "Cancelled"),
                TotalRevenue = ordersInShift
                    .Where(o => o.Bill.Any(b => b.Status == "Paid"))
                    .Sum(o => o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount)),
                Orders = ordersInShift.Select(order =>
                {
                    var paidBill = order.Bill.FirstOrDefault(b => b.Status == "Paid");

                    return new ShiftOrderViewModel
                    {
                        Id = order.Id,
                        TableNumber = order.RestaurantTable.TableNumber,
                        OrderTime = order.OrderTime,
                        Status = order.Status.ToLower(),
                        StatusDisplay = GetStatusTextVietnamese(order.Status),
                        TotalAmount = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime),
                        PaymentMethod = paidBill?.PaymentMethod ?? "",
                        IsPaid = (paidBill != null),
                        ItemCount = order.OrderDetail.Count,
                        CashierName = order.Employee?.FullName ?? "N/A",
                        BillId = paidBill?.Id,
                        Items = order.OrderDetail.Select(od => new ShiftOrderItemViewModel
                        {
                            Name = od.MenuItem.Name,
                            Quantity = od.Quantity,
                            Price = od.PriceAtTime,
                            Description = od.MenuItem.Description
                        }).ToList()
                    };
                }).ToList()
            };

            return viewModel;
        }

        private string GetStatusTextVietnamese(string status)
        {
            var statusMap = new Dictionary<string, string>
            {
                { "Pending", "Chờ xử lý" },
                { "Preparing", "Đang chuẩn bị" },
                { "Ready", "Sẵn sàng" },
                { "Completed", "Hoàn thành" },
                { "Cancelled", "Đã hủy" }
            };
            return statusMap.ContainsKey(status) ? statusMap[status] : status;
        }
    }
}
