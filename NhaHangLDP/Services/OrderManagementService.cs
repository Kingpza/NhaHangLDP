using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using Newtonsoft.Json;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class OrderManagementService
    {
        private readonly NhaHangLDPEntities db;

        public OrderManagementService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public bool CreateOrder(int tableId, string orderItems, string customerNote, int cashierId, int shiftId, out int orderId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(orderItems))
                    {
                        errorMessage = "Vui lòng chọn ít nhất một món!";
                        orderId = 0;
                        return false;
                    }

                    List<OrderItemDto> items;
                    try
                    {
                        items = JsonConvert.DeserializeObject<List<OrderItemDto>>(orderItems);
                    }
                    catch (JsonException)
                    {
                        errorMessage = "Dữ liệu đơn hàng không hợp lệ!";
                        orderId = 0;
                        return false;
                    }

                    if (items == null || !items.Any())
                    {
                        errorMessage = "Dữ liệu đơn hàng không hợp lệ!";
                        orderId = 0;
                        return false;
                    }

                    var table = db.RestaurantTable.Find(tableId);
                    if (table == null)
                    {
                        errorMessage = "Bàn không tồn tại!";
                        orderId = 0;
                        return false;
                    }

                    var existingOrder = db.Order
                        .Where(o => o.TableId == tableId && (o.Status == "Pending" || o.Status == "Preparing"))
                        .FirstOrDefault();

                    Order order;
                    if (existingOrder != null)
                    {
                        order = existingOrder;
                    }
                    else
                    {
                        order = new Order
                        {
                            TableId = tableId,
                            WaiterId = cashierId,
                            ShiftId = shiftId,
                            OrderTime = DateTime.Now,
                            Status = "Pending"
                        };
                        db.Order.Add(order);
                        db.SaveChanges();
                        table.Status = "Occupied";
                    }

                    var stockErrors = new List<string>();
                    var inventoryUpdates = new List<InventoryUpdate>();

                    foreach (var item in items)
                    {
                        var menuItem = db.MenuItem.Find(item.Id);
                        if (menuItem == null || !menuItem.IsAvailable)
                        {
                            stockErrors.Add($"Món {item.Name} không khả dụng");
                            continue;
                        }

                        var requiredIngredients = db.MenuItemIngredient
                            .Include(mi => mi.Ingredient)
                            .Where(mi => mi.MenuItemId == item.Id)
                            .ToList();

                        foreach (var reqIngredient in requiredIngredients)
                        {
                            var totalRequired = reqIngredient.RequiredQuantity * item.Quantity;
                            if (reqIngredient.Ingredient.AvailableStock < totalRequired)
                            {
                                stockErrors.Add($"Không đủ {reqIngredient.Ingredient.Name} cho món {menuItem.Name} (cần {totalRequired:N2} {reqIngredient.Unit}, còn {reqIngredient.Ingredient.AvailableStock:N2})");
                            }
                            else
                            {
                                inventoryUpdates.Add(new InventoryUpdate
                                {
                                    IngredientId = reqIngredient.Ingredient.Id,
                                    RequiredQuantity = totalRequired
                                });
                            }
                        }

                        if (stockErrors.Any()) continue;

                        var existingDetail = db.OrderDetail
                            .FirstOrDefault(od => od.OrderId == order.Id && od.MenuItemId == item.Id);

                        if (existingDetail != null)
                        {
                            existingDetail.Quantity += item.Quantity;
                        }
                        else
                        {
                            var orderDetail = new OrderDetail
                            {
                                OrderId = order.Id,
                                MenuItemId = item.Id,
                                Quantity = item.Quantity,
                                PriceAtTime = menuItem.Price,
                                Notes = customerNote
                            };
                            db.OrderDetail.Add(orderDetail);
                        }
                    }

                    if (stockErrors.Any())
                    {
                        transaction.Rollback();
                        errorMessage = "Lỗi tồn kho:\n" + string.Join("\n", stockErrors);
                        orderId = 0;
                        return false;
                    }

                    foreach (var update in inventoryUpdates)
                    {
                        var ingredient = db.Ingredient.Find(update.IngredientId);
                        if (ingredient != null)
                        {
                            ingredient.AvailableStock -= update.RequiredQuantity;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    orderId = order.Id;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    orderId = 0;
                    return false;
                }
            }
        }

        public bool UpdateOrderStatus(int orderId, string status, out string errorMessage)
        {
            try
            {
                var allowedStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
                if (!allowedStatuses.Contains(status))
                {
                    errorMessage = "Trạng thái đơn hàng không hợp lệ!";
                    return false;
                }

                var order = db.Order.Include(o => o.Table).FirstOrDefault(o => o.Id == orderId);
                if (order == null)
                {
                    errorMessage = "Không tìm thấy đơn hàng!";
                    return false;
                }

                order.Status = status;

                if (status == "Completed" || status == "Cancelled")
                {
                    order.Table.Status = "Available";
                }

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

        public bool CancelOrder(int orderId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.Order
                        .Include(o => o.Table)
                        .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.MenuItem)
                                .ThenInclude(mi => mi.MenuItemIngredients)
                                    .ThenInclude(mii => mii.Ingredient)
                        .Include(o => o.Bills)
                        .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        errorMessage = "Không tìm thấy đơn hàng!";
                        return false;
                    }

                    if (order.Status == "Completed" || order.Bills.Any(b => b.Status == "Paid"))
                    {
                        errorMessage = "Không thể hủy đơn hàng đã hoàn thành hoặc đã thanh toán!";
                        return false;
                    }

                    foreach (var detail in order.OrderDetails)
                    {
                        var requiredIngredients = detail.MenuItem.MenuItemIngredients;
                        foreach (var reqIngredient in requiredIngredients)
                        {
                            var returnQuantity = reqIngredient.RequiredQuantity * detail.Quantity;
                            reqIngredient.Ingredient.AvailableStock += returnQuantity;
                        }
                    }

                    order.Status = "Cancelled";
                    order.Table.Status = "Available";

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Lỗi khi hủy đơn hàng: " + ex.Message;
                    return false;
                }
            }
        }

        public class OrderItemDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        public class InventoryUpdate
        {
            public int IngredientId { get; set; }
            public decimal RequiredQuantity { get; set; }
        }
    }
}
