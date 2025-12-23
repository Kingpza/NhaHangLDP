using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class OrderQueryService
    {
        private readonly NhaHangLDPEntities db;

        public OrderQueryService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public List<object> GetOrdersData(int shiftId, string status = "all")
        {
            var query = db.Order
                .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                .Include(o => o.RestaurantTable)
                .Include(o => o.Bill)
                .Where(o => o.ShiftId == shiftId);

            if (status != "all")
            {
                switch (status.ToLower())
                {
                    case "incomplete":
                        query = query.Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready");
                        break;
                    case "completed":
                        query = query.Where(o => o.Status == "Completed" || o.Bill.Any(b => b.Status == "Paid"));
                        break;
                    case "paid":
                        query = query.Where(o => o.Bill.Any(b => b.Status == "Paid"));
                        break;
                    default:
                        query = query.Where(o => o.Status == status);
                        break;
                }
            }

            var orders = query
                .OrderByDescending(o => o.OrderTime)
                .ToList()
                .Select(order => new
                {
                    id = order.Id.ToString(),
                    table = order.RestaurantTable.TableNumber,
                    status = order.Status.ToLower(),
                    statusText = GetStatusTextVietnamese(order.Status),
                    time = order.OrderTime.ToString("HH:mm"),
                    paymentStatus = order.Bill.Any(b => b.Status == "Paid") ? "paid" : "unpaid",
                    paymentMethod = order.Bill.FirstOrDefault(b => b.Status == "Paid")?.PaymentMethod ?? "",
                    items = order.OrderDetail.Select(od => new
                    {
                        name = od.MenuItem.Name,
                        quantity = od.Quantity,
                        price = od.PriceAtTime,
                        description = od.MenuItem.Description ?? ""
                    }).ToList(),
                    total = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime)
                })
                .ToList<object>();

            return orders;
        }

        public object GetOrderDetail(int orderId)
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

            var bill = order.Bill.FirstOrDefault();
            var subtotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
            var vat = Math.Round(subtotal * 0.1m);
            var total = subtotal + vat;

            var orderDetail = new
            {
                id = order.Id.ToString(),
                table = order.RestaurantTable.TableNumber,
                status = order.Status.ToLower(),
                statusText = GetStatusTextVietnamese(order.Status),
                time = order.OrderTime.ToString("HH:mm"),
                date = order.OrderTime.ToString("dd/MM/yyyy"),
                paymentStatus = bill?.Status == "Paid" ? "paid" : "unpaid",
                paymentMethod = bill?.PaymentMethod ?? "",
                waiter = order.Employee?.FullName ?? "N/A",
                items = order.OrderDetail.Select(od => new
                {
                    name = od.MenuItem.Name,
                    quantity = od.Quantity,
                    price = od.PriceAtTime,
                    description = od.MenuItem.Description ?? "",
                    total = od.Quantity * od.PriceAtTime
                }).ToList(),
                subtotal = subtotal,
                vat = vat,
                total = total,
                finalAmount = bill?.FinalAmount ?? total
            };

            return orderDetail;
        }

        public object GetTableStatus(int tableId)
        {
            var table = db.RestaurantTable
                .Include(t => t.Order.Select(o => o.OrderDetail))
                .Include(t => t.TableArea)
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return null;
            }

            var activeOrder = table.Order
                .Where(o => o.Status == "Pending" || o.Status == "Preparing")
                .OrderByDescending(o => o.OrderTime)
                .FirstOrDefault();

            var tableStatus = new
            {
                TableId = table.Id,
                TableNumber = table.TableNumber,
                Status = table.Status,
                Capacity = table.Capacity,
                OrderId = activeOrder?.Id,
                OrderTime = activeOrder?.OrderTime.ToString("HH:mm"),
                ItemCount = activeOrder?.OrderDetail.Count ?? 0,
                LastUpdate = DateTime.Now
            };

            return tableStatus;
        }

        public List<object> GetTablesData()
        {
            var tablesData = db.RestaurantTable
                .Include(t => t.TableArea)
                .Include(t => t.Order.Select(o => o.OrderDetail))
                .OrderBy(t => t.TableArea.Name)
                .ThenBy(t => t.TableNumber)
                .ToList()
                .Select(table =>
                {
                    var activeOrder = table.Order
                        .Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                        .OrderByDescending(o => o.OrderTime)
                        .FirstOrDefault();

                    return new
                    {
                        id = table.Id,
                        tableNumber = table.TableNumber,
                        capacity = table.Capacity,
                        status = table.Status?.ToLower() ?? "available",
                        statusText = table.Status == "Available" ? "Trống" :
                                   table.Status == "Occupied" ? "Có khách" :
                                   table.Status == "Reserved" ? "Đã đặt" : "Không xác định",
                        areaName = table.TableArea?.Name ?? "Không xác định",
                        activeOrderId = activeOrder?.Id,
                        orderTime = activeOrder?.OrderTime.ToString("HH:mm"),
                        itemCount = activeOrder?.OrderDetail?.Count ?? 0
                    };
                })
                .ToList<object>();

            return tablesData;
        }

        public List<object> GetTableOrderItems(int tableId)
        {
            var table = db.RestaurantTable
                .Include(t => t.Order.Select(o => o.OrderDetail.Select(od => od.MenuItem)))
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return null;
            }

            var order = table.Order.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
            if (order == null)
            {
                return null;
            }

            var items = order.OrderDetail.Select(d => new
            {
                orderDetailId = d.Id,
                name = d.MenuItem.Name,
                quantity = d.Quantity,
                price = d.PriceAtTime
            }).ToList<object>();

            return items;
        }

        public int? GetLastPaidOrderId(int shiftId)
        {
            var lastBill = db.Bill
                .Where(b => b.Order.ShiftId == shiftId && b.Status == "Paid")
                .OrderByDescending(b => b.BillDate)
                .FirstOrDefault();

            return lastBill?.OrderId;
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
