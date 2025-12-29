using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class TableOperationService
    {
        private readonly MyDbContext db;

        public TableOperationService(MyDbContext context)
        {
            db = context;
        }

        public bool SplitTable(int sourceTableId, int targetTableId, List<SplitItemModel> items, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (items == null || !items.Any())
                    {
                        errorMessage = "Chưa chọn món để tách!";
                        return false;
                    }

                    var sourceTable = db.RestaurantTables.Include("Order").FirstOrDefault(t => t.Id == sourceTableId);
                    var targetTable = db.RestaurantTables.Include("Order").FirstOrDefault(t => t.Id == targetTableId);

                    if (sourceTable == null || targetTable == null)
                    {
                        errorMessage = "Bàn không tồn tại!";
                        return false;
                    }

                    var sourceOrder = sourceTable.Orders.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
                    if (sourceOrder == null)
                    {
                        errorMessage = "Bàn nguồn không có đơn!";
                        return false;
                    }

                    var newOrder = new Order
                    {
                        TableId = targetTableId,
                        OrderTime = DateTime.Now,
                        Status = "Pending",
                        WaiterId = sourceOrder.WaiterId,
                        ShiftId = sourceOrder.ShiftId
                    };
                    db.Orders.Add(newOrder);
                    db.SaveChanges();

                    foreach (var item in items)
                    {
                        var originalDetail = db.OrderDetails.Find(item.OrderDetailId);
                        if (originalDetail != null)
                        {
                            if (originalDetail.Quantity == item.Quantity)
                            {
                                originalDetail.OrderId = newOrder.Id;
                            }
                            else if (originalDetail.Quantity > item.Quantity)
                            {
                                originalDetail.Quantity -= item.Quantity;

                                var newDetail = new OrderDetail
                                {
                                    OrderId = newOrder.Id,
                                    MenuItemId = originalDetail.MenuItemId,
                                    Quantity = item.Quantity,
                                    PriceAtTime = originalDetail.PriceAtTime,
                                    Notes = originalDetail.Notes
                                };
                                db.OrderDetails.Add(newDetail);
                            }
                        }
                    }

                    targetTable.Status = "Occupied";

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Lỗi: " + ex.Message;
                    return false;
                }
            }
        }

        public class SplitItemModel
        {
            public int OrderDetailId { get; set; }
            public int Quantity { get; set; }
        }
    }
}
