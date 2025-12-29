using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class TableManagementService
    {
        private readonly MyDbContext db;

        public TableManagementService(MyDbContext context)
        {
            db = context;
        }

        public bool UpdateTableStatus(int tableId, string status, out string errorMessage)
        {
            try
            {
                var allowedStatuses = new[] { "Available", "Occupied", "Reserved" };
                if (!allowedStatuses.Contains(status))
                {
                    errorMessage = "Trạng thái bàn không hợp lệ!";
                    return false;
                }

                var table = db.RestaurantTables.Find(tableId);
                if (table == null)
                {
                    errorMessage = "Không tìm thấy bàn!";
                    return false;
                }

                table.Status = status;
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

        public bool TransferTable(int sourceTableId, int targetTableId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var sourceTable = db.RestaurantTables.Include("Orders").FirstOrDefault(t => t.Id == sourceTableId);
                    var targetTable = db.RestaurantTables.Include("Orders").FirstOrDefault(t => t.Id == targetTableId);

                    if (sourceTable == null || targetTable == null)
                    {
                        errorMessage = "Bàn không tồn tại!";
                        return false;
                    }

                    var activeOrder = sourceTable.Orders
                        .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
                        .OrderByDescending(o => o.OrderTime)
                        .FirstOrDefault();

                    if (activeOrder == null)
                    {
                        errorMessage = "Bàn nguồn không có đơn hàng!";
                        return false;
                    }

                    if (targetTable.Status == "Occupied")
                    {
                        errorMessage = "Bàn đích đang có khách. Vui lòng dùng tính năng Gộp bàn!";
                        return false;
                    }

                    activeOrder.TableId = targetTableId;
                    sourceTable.Status = "Available";
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

        public bool MergeTables(int mainTableId, int secondaryTableId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var mainTable = db.RestaurantTables.Include("Order.OrderDetails").FirstOrDefault(t => t.Id == mainTableId);
                    var secTable = db.RestaurantTables.Include("Order.OrderDetails").FirstOrDefault(t => t.Id == secondaryTableId);

                    if (mainTable == null || secTable == null)
                    {
                        errorMessage = "Bàn không tồn tại!";
                        return false;
                    }

                    var mainOrder = mainTable.Orders.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
                    var secOrder = secTable.Orders.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");

                    if (mainOrder == null || secOrder == null)
                    {
                        errorMessage = "Cả hai bàn phải đang có khách mới gộp được!";
                        return false;
                    }

                    foreach (var detail in secOrder.OrderDetails.ToList())
                    {
                        detail.OrderId = mainOrder.Id;
                    }

                    secOrder.Status = "Cancelled";
                    secTable.Status = "Available";

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

        public bool AssignTable(int tableId, int customers, string customerName, string customerPhone, string notes, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var table = db.RestaurantTables.Find(tableId);
                    if (table == null)
                    {
                        errorMessage = "Không tìm thấy bàn!";
                        return false;
                    }

                    if (customers > table.Capacity)
                    {
                        errorMessage = $"Số khách ({customers}) vượt quá sức chứa của bàn ({table.Capacity})!";
                        return false;
                    }

                    if (table.Status != "Available")
                    {
                        errorMessage = "Bàn không ở trạng thái trống!";
                        return false;
                    }

                    var booking = new Booking
                    {
                        TableId = tableId,
                        CustomerName = customerName,
                        CustomerPhone = customerPhone,
                        NumberOfGuests = customers,
                        BookingDateTime = DateTime.Now,
                        Notes = notes,
                        Status = "Pending",
                        CreatedDate = DateTime.Now
                    };

                    db.Bookings.Add(booking);
                    table.Status = "Occupied";

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    return false;
                }
            }
        }

        public bool ReserveTable(int tableId, int customers, string customerName, string customerPhone, string notes, string time, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var table = db.RestaurantTables.Find(tableId);
                    if (table == null)
                    {
                        errorMessage = "Không tìm thấy bàn!";
                        return false;
                    }

                    if (customers > table.Capacity)
                    {
                        errorMessage = $"Số khách ({customers}) vượt quá sức chứa của bàn ({table.Capacity})!";
                        return false;
                    }

                    if (table.Status != "Available")
                    {
                        errorMessage = "Bàn không ở trạng thái trống!";
                        return false;
                    }

                    DateTime bookingDateTime;
                    if (!DateTime.TryParseExact($"{DateTime.Today:yyyy-MM-dd} {time}", "yyyy-MM-dd HH:mm",
                        null, System.Globalization.DateTimeStyles.None, out bookingDateTime))
                    {
                        errorMessage = "Thời gian đặt bàn không hợp lệ!";
                        return false;
                    }

                    if (bookingDateTime <= DateTime.Now)
                    {
                        errorMessage = "Thời gian đặt bàn phải trong tương lai!";
                        return false;
                    }

                    var booking = new Booking
                    {
                        TableId = tableId,
                        CustomerName = customerName,
                        CustomerPhone = customerPhone,
                        NumberOfGuests = customers,
                        BookingDateTime = bookingDateTime,
                        Notes = notes,
                        Status = "Pending",
                        CreatedDate = DateTime.Now
                    };

                    db.Bookings.Add(booking);
                    table.Status = "Reserved";

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    return false;
                }
            }
        }

        public bool CheckoutTable(int tableId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var table = db.RestaurantTables
                        .Include(t => t.Orders).ThenInclude(o => o.Bills)
                        .FirstOrDefault(t => t.Id == tableId);

                    if (table == null)
                    {
                        errorMessage = "Không tìm thấy bàn!";
                        return false;
                    }

                    if (table.Status != "Occupied")
                    {
                        errorMessage = "Bàn không có khách để checkout!";
                        return false;
                    }

                    var unpaidOrder = table.Orders
                        .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
                        .FirstOrDefault();

                    if (unpaidOrder != null)
                    {
                        errorMessage = "Bàn còn đơn hàng chưa hoàn thành, vui lòng thanh toán trước!";
                        return false;
                    }

                    var activeBooking = db.Bookings
                        .Where(b => b.TableId == tableId && (b.Status == "Confirmed" || b.Status == "Pending"))
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Completed";
                    }

                    table.Status = "Available";
                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    return false;
                }
            }
        }

        public bool ConfirmReservation(int tableId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var table = db.RestaurantTables.Find(tableId);
                    if (table == null)
                    {
                        errorMessage = "Không tìm thấy bàn!";
                        return false;
                    }

                    if (table.Status != "Reserved")
                    {
                        errorMessage = "Bàn không ở trạng thái đã đặt!";
                        return false;
                    }

                    var activeBooking = db.Bookings
                        .Where(b => b.TableId == tableId && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Confirmed";
                    }

                    table.Status = "Occupied";
                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    return false;
                }
            }
        }

        public bool CancelReservation(int tableId, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var table = db.RestaurantTables.Find(tableId);
                    if (table == null)
                    {
                        errorMessage = "Không tìm thấy bàn!";
                        return false;
                    }

                    if (table.Status != "Reserved")
                    {
                        errorMessage = "Bàn không ở trạng thái đã đặt!";
                        return false;
                    }

                    var activeBooking = db.Bookings
                        .Where(b => b.TableId == tableId && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Cancelled";
                    }

                    table.Status = "Available";
                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra: " + ex.Message;
                    return false;
                }
            }
        }
    }
}
