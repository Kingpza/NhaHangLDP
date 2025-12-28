using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
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
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuItem)
                .Include(o => o.Table)
                .Include(o => o.Bills)
                .Where(o => o.ShiftId == shiftId);

            if (status != "all")
            {
                switch (status.ToLower())
                {
                    case "incomplete":
                        query = query.Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready");
                        break;
                    case "completed":
                        query = query.Where(o => o.Status == "Completed" || o.Bills.Any(b => b.Status == "Paid"));
                        break;
                    case "paid":
                        query = query.Where(o => o.Bills.Any(b => b.Status == "Paid"));
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
                    table = order.Table.TableNumber,
                    status = order.Status.ToLower(),
                    statusText = GetStatusTextVietnamese(order.Status),
                    time = order.OrderTime.ToString("HH:mm"),
                    paymentStatus = order.Bills.Any(b => b.Status == "Paid") ? "paid" : "unpaid",
                    paymentMethod = order.Bills.FirstOrDefault(b => b.Status == "Paid")?.PaymentMethod ?? "",
                    items = order.OrderDetails.Select(od => new
                    {
                        name = od.MenuItem.Name,
                        quantity = od.Quantity,
                        price = od.PriceAtTime,
                        description = od.MenuItem.Description ?? ""
                    }).ToList(),
                    total = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime)
                })
                .ToList<object>();

            return orders;
        }

        public object GetOrderDetail(int orderId)
        {
            var order = db.Order
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuItem)
                .Include(o => o.Table)
                .Include(o => o.Bills)
                .Include(o => o.Waiter)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return null;
            }

            var bill = order.Bills.FirstOrDefault();
            var subtotal = order.OrderDetails.Sum(od => od.Quantity * od.PriceAtTime);
            var vat = Math.Round(subtotal * 0.1m);
            var total = subtotal + vat;

            var orderDetail = new
            {
                id = order.Id.ToString(),
                table = order.Table.TableNumber,
                status = order.Status.ToLower(),
                statusText = GetStatusTextVietnamese(order.Status),
                time = order.OrderTime.ToString("HH:mm"),
                date = order.OrderTime.ToString("dd/MM/yyyy"),
                paymentStatus = bill?.Status == "Paid" ? "paid" : "unpaid",
                paymentMethod = bill?.PaymentMethod ?? "",
                waiter = order.Waiter?.FullName ?? "N/A",
                items = order.OrderDetails.Select(od => new
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
                .Include(t => t.Orders)
                    .ThenInclude(o => o.OrderDetails)
                .Include(t => t.TableArea)
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return null;
            }

            var activeOrder = table.Orders
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
                ItemCount = activeOrder?.OrderDetails.Count ?? 0,
                LastUpdate = DateTime.Now
            };

            return tableStatus;
        }

        public List<object> GetTablesData()
        {
            var tablesData = db.RestaurantTable
                .Include(t => t.TableArea)
                .Include(t => t.Orders)
                    .ThenInclude(o => o.OrderDetails)
                .OrderBy(t => t.TableArea.Name)
                .ThenBy(t => t.TableNumber)
                .ToList()
                .Select(table =>
                {
                    var activeOrder = table.Orders
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
                        itemCount = activeOrder?.OrderDetails?.Count ?? 0
                    };
                })
                .ToList<object>();

            return tablesData;
        }

        public List<object> GetTableOrderItems(int tableId)
        {
            var table = db.RestaurantTable
                .Include(t => t.Orders)
                    .ThenInclude(o => o.OrderDetails)
                        .ThenInclude(od => od.MenuItem)
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return null;
            }

            var order = table.Orders.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
            if (order == null)
            {
                return null;
            }

            var items = order.OrderDetails.Select(d => new
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

        #region Online Orders (CustomerOrder)

        /// <summary>
        /// Lấy danh sách đơn hàng online cho thu ngân
        /// </summary>
        public List<OnlineOrderViewModel> GetOnlineOrders(string status = "all", string orderType = "all")
        {
            var query = db.CustomerOrder
                .Include(o => o.CustomerOrderDetails)
                .AsQueryable();

            // Filter by status
            if (status != "all")
            {
                if (status == "pending")
                {
                    query = query.Where(o => o.Status == "Pending");
                }
                else if (status == "processing")
                {
                    query = query.Where(o => o.Status == "Confirmed" || o.Status == "Preparing");
                }
                else if (status == "ready")
                {
                    query = query.Where(o => o.Status == "Ready");
                }
                else if (status == "delivering")
                {
                    query = query.Where(o => o.Status == "Delivering");
                }
                else if (status == "completed")
                {
                    query = query.Where(o => o.Status == "Completed");
                }
                else
                {
                    query = query.Where(o => o.Status == status);
                }
            }

            // Filter by order type
            if (orderType != "all" && !string.IsNullOrEmpty(orderType))
            {
                if (orderType.Equals("Delivery", StringComparison.OrdinalIgnoreCase))
                {
                    // Đơn giao hàng
                    query = query.Where(o => o.OrderType == "Delivery");
                }
                else if (orderType.Equals("Pickup", StringComparison.OrdinalIgnoreCase))
                {
                    // Đơn tự đến lấy (Pickup hoặc TakeAway)
                    query = query.Where(o => o.OrderType == "Pickup" || o.OrderType == "TakeAway");
                }
                else if (orderType.Equals("DineIn", StringComparison.OrdinalIgnoreCase))
                {
                    // Đơn ăn tại quán
                    query = query.Where(o => o.OrderType == "DineIn");
                }
                else
                {
                    // Match chính xác
                    query = query.Where(o => o.OrderType == orderType);
                }
            }

            var orders = query
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToList()
                .Select(o => new OnlineOrderViewModel
                {
                    Id = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    CustomerEmail = o.CustomerEmail,
                    OrderType = o.OrderType,
                    OrderTypeText = GetOrderTypeText(o.OrderType),
                    DeliveryAddress = o.DeliveryAddress,
                    Ward = o.Ward,
                    District = o.District,
                    City = o.City,
                    FullAddress = string.IsNullOrEmpty(o.DeliveryAddress) ? "" :
                        $"{o.DeliveryAddress}, {o.Ward}, {o.District}, {o.City}",
                    SubTotal = o.SubTotal,
                    DeliveryFee = o.DeliveryFee,
                    Discount = o.Discount,
                    TotalAmount = o.TotalAmount,
                    PaymentMethod = o.PaymentMethod,
                    PaymentMethodText = GetPaymentMethodText(o.PaymentMethod),
                    PaymentStatus = o.PaymentStatus,
                    PaymentStatusText = o.PaymentStatus == "Paid" ? "Đã thanh toán" : "Chưa thanh toán",
                    Status = o.Status,
                    StatusText = GetOnlineOrderStatusText(o.Status),
                    StatusClass = GetOnlineOrderStatusClass(o.Status),
                    Note = o.Note,
                    OrderDate = o.OrderDate,
                    OrderDateText = o.OrderDate.ToString("HH:mm dd/MM/yyyy"),
                    EstimatedDeliveryTime = o.EstimatedDeliveryTime,
                    ItemCount = o.CustomerOrderDetails?.Count ?? 0,
                    Items = o.CustomerOrderDetails?.Select(d => new OnlineOrderItemViewModel
                    {
                        Id = d.Id,
                        ItemName = d.ItemName,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        Subtotal = d.Subtotal,
                        SpecialInstructions = d.SpecialInstructions
                    }).ToList() ?? new List<OnlineOrderItemViewModel>()
                })
                .ToList();

            return orders;
        }

        /// <summary>
        /// Lấy chi tiết đơn hàng online
        /// </summary>
        public OnlineOrderViewModel GetOnlineOrderDetail(int orderId)
        {
            var order = db.CustomerOrder
                .Include(o => o.CustomerOrderDetails)
                    .ThenInclude(d => d.MenuItem)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) return null;

            // Lấy thông tin shipper nếu có
            var assignment = db.DeliveryAssignment
                .Include(a => a.Shipper)
                .FirstOrDefault(a => a.OrderId == orderId && a.Status != "Cancelled" && a.Status != "Rejected");

            return new OnlineOrderViewModel
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                CustomerEmail = order.CustomerEmail,
                OrderType = order.OrderType,
                OrderTypeText = GetOrderTypeText(order.OrderType),
                DeliveryAddress = order.DeliveryAddress,
                Ward = order.Ward,
                District = order.District,
                City = order.City,
                FullAddress = string.IsNullOrEmpty(order.DeliveryAddress) ? "" :
                    $"{order.DeliveryAddress}, {order.Ward}, {order.District}, {order.City}",
                SubTotal = order.SubTotal,
                DeliveryFee = order.DeliveryFee,
                Discount = order.Discount,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                PaymentMethodText = GetPaymentMethodText(order.PaymentMethod),
                PaymentStatus = order.PaymentStatus,
                PaymentStatusText = order.PaymentStatus == "Paid" ? "Đã thanh toán" : "Chưa thanh toán",
                Status = order.Status,
                StatusText = GetOnlineOrderStatusText(order.Status),
                StatusClass = GetOnlineOrderStatusClass(order.Status),
                Note = order.Note,
                OrderDate = order.OrderDate,
                OrderDateText = order.OrderDate.ToString("HH:mm dd/MM/yyyy"),
                EstimatedDeliveryTime = order.EstimatedDeliveryTime,
                ItemCount = order.CustomerOrderDetails?.Count ?? 0,
                Items = order.CustomerOrderDetails?.Select(d => new OnlineOrderItemViewModel
                {
                    Id = d.Id,
                    MenuItemId = d.MenuItemId,
                    ItemName = d.ItemName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    Subtotal = d.Subtotal,
                    SpecialInstructions = d.SpecialInstructions,
                    ImageUrl = d.MenuItem?.ImageUrl
                }).ToList() ?? new List<OnlineOrderItemViewModel>(),
                // Thông tin shipper
                ShipperId = assignment?.ShipperId,
                ShipperName = assignment?.Shipper?.FullName,
                ShipperPhone = assignment?.Shipper?.Phone,
                DeliveryStatus = assignment?.Status
            };
        }

        /// <summary>
        /// Đếm số đơn hàng online theo trạng thái
        /// </summary>
        public Dictionary<string, int> GetOnlineOrderCounts()
        {
            var counts = new Dictionary<string, int>
            {
                { "all", db.CustomerOrder.Count() },
                { "pending", db.CustomerOrder.Count(o => o.Status == "Pending") },
                { "confirmed", db.CustomerOrder.Count(o => o.Status == "Confirmed") },
                { "preparing", db.CustomerOrder.Count(o => o.Status == "Preparing") },
                { "ready", db.CustomerOrder.Count(o => o.Status == "Ready") },
                { "delivering", db.CustomerOrder.Count(o => o.Status == "Delivering") },
                { "completed", db.CustomerOrder.Count(o => o.Status == "Completed") },
                { "cancelled", db.CustomerOrder.Count(o => o.Status == "Cancelled") }
            };

            return counts;
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng online
        /// </summary>
        public bool UpdateOnlineOrderStatus(int orderId, string newStatus, out string errorMessage)
        {
            try
            {
                var order = db.CustomerOrder.Find(orderId);
                if (order == null)
                {
                    errorMessage = "Không tìm thấy đơn hàng!";
                    return false;
                }

                var oldStatus = order.Status;
                order.Status = newStatus;

                // Cập nhật timestamp theo trạng thái
                switch (newStatus)
                {
                    case "Confirmed":
                        order.ConfirmedDate = DateTime.Now;
                        break;
                    case "Preparing":
                        order.PreparingDate = DateTime.Now;
                        break;
                    case "Ready":
                        order.ReadyDate = DateTime.Now;
                        break;
                    case "Delivering":
                        order.DeliveringDate = DateTime.Now;
                        break;
                    case "Completed":
                        order.CompletedDate = DateTime.Now;
                        break;
                    case "Cancelled":
                        order.CancelledDate = DateTime.Now;
                        break;
                }

                db.SaveChanges();
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi: " + ex.Message;
                return false;
            }
        }

        private string GetOnlineOrderStatusText(string status)
        {
            switch (status)
            {
                case "Pending": return "Chờ xác nhận";
                case "Confirmed": return "Đã xác nhận";
                case "Preparing": return "Đang chuẩn bị";
                case "Ready": return "Sẵn sàng";
                case "Delivering": return "Đang giao";
                case "Completed": return "Hoàn thành";
                case "Cancelled": return "Đã hủy";
                default: return status;
            }
        }

        private string GetOnlineOrderStatusClass(string status)
        {
            switch (status)
            {
                case "Pending": return "warning";
                case "Confirmed": return "info";
                case "Preparing": return "primary";
                case "Ready": return "success";
                case "Delivering": return "info";
                case "Completed": return "success";
                case "Cancelled": return "danger";
                default: return "secondary";
            }
        }

        private string GetPaymentMethodText(string method)
        {
            switch (method)
            {
                case "COD": return "Thanh toán khi nhận";
                case "VNPay": return "VNPay";
                case "MoMo": return "Ví MoMo";
                case "Card": return "Thẻ tín dụng";
                case "Cash": return "Tiền mặt";
                default: return method ?? "N/A";
            }
        }

        private string GetOrderTypeText(string orderType)
        {
            if (string.IsNullOrEmpty(orderType))
                return "Không xác định";
                
            switch (orderType.ToLower())
            {
                case "delivery": return "Giao hàng";
                case "pickup": return "Tự đến lấy";
                case "takeaway": return "Mang đi";
                case "dinein": return "Ăn tại quán";
                default: return orderType;
            }
        }

        #endregion
    }

    #region Online Order ViewModels

    public class OnlineOrderViewModel
    {
        public int Id { get; set; }
        public string OrderCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public string OrderType { get; set; }
        public string OrderTypeText { get; set; }
        public string DeliveryAddress { get; set; }
        public string Ward { get; set; }
        public string District { get; set; }
        public string City { get; set; }
        public string FullAddress { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentMethodText { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentStatusText { get; set; }
        public string Status { get; set; }
        public string StatusText { get; set; }
        public string StatusClass { get; set; }
        public string Note { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderDateText { get; set; }
        public DateTime? EstimatedDeliveryTime { get; set; }
        public int ItemCount { get; set; }
        public List<OnlineOrderItemViewModel> Items { get; set; }

        // Shipper info
        public int? ShipperId { get; set; }
        public string ShipperName { get; set; }
        public string ShipperPhone { get; set; }
        public string DeliveryStatus { get; set; }
    }

    public class OnlineOrderItemViewModel
    {
        public int Id { get; set; }
        public int? MenuItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string SpecialInstructions { get; set; }
        public string ImageUrl { get; set; }
    }

    #endregion
}
