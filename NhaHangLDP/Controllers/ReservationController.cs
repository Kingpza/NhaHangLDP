using NhaHangLDP.Models;
using NhaHangLDP.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý đặt bàn
    /// </summary>
    public class ReservationController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();
        private readonly RealTimeNotificationService _notificationService;

        public ReservationController()
        {
            _notificationService = new RealTimeNotificationService();
        }

        #region Public Views

        /// <summary>
        /// Trang đặt bàn
        /// </summary>
        public ActionResult Index()
        {
            ViewBag.TimeSlots = GetTimeSlots();
            return View(new ReservationFormModel
            {
                ReservationDate = DateTime.Today.AddDays(1),
                NumberOfGuests = 2
            });
        }

        /// <summary>
        /// Xử lý đặt bàn
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Book(ReservationFormModel model, int? selectedTableId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin!" });
                }

                // Validate date
                var reservationDate = model.ReservationDate.Date;
                if (reservationDate < DateTime.Today)
                {
                    return Json(new { success = false, message = "Ngày đặt bàn không hợp lệ!" });
                }

                // Parse time
                if (!TimeSpan.TryParse(model.ReservationTime, out TimeSpan reservationTime))
                {
                    return Json(new { success = false, message = "Giờ đặt bàn không hợp lệ!" });
                }

                // Generate reservation code
                var reservationCode = "RES" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999);

                // Sử dụng bàn đã chọn nếu có, nếu không tự động tìm bàn
                int? tableId = selectedTableId;
                if (!tableId.HasValue || tableId == 0)
                {
                    tableId = FindAvailableTable(reservationDate, reservationTime, model.NumberOfGuests, model.TablePreference);
                }

                // Get customer ID if logged in
                var customerId = Session["CustomerId"] as int?;

                // Create reservation
                var sql = @"
                    INSERT INTO Reservation 
                    (ReservationCode, CustomerId, CustomerName, CustomerPhone, CustomerEmail, 
                     ReservationDate, ReservationTime, NumberOfGuests, TableId, TablePreference, 
                     SpecialRequests, Status, CreatedDate)
                    VALUES 
                    (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, 'Pending', GETDATE())";

                _db.Database.ExecuteSqlCommand(sql,
                    reservationCode,           // @p0
                    customerId,                // @p1
                    model.CustomerName,        // @p2
                    model.CustomerPhone,       // @p3
                    model.CustomerEmail,       // @p4
                    reservationDate,           // @p5
                    reservationTime,           // @p6
                    model.NumberOfGuests,      // @p7
                    tableId,                   // @p8
                    model.TablePreference,     // @p9
                    model.SpecialRequests      // @p10
                );

                // Lấy ID của reservation vừa tạo
                var newReservationId = _db.Database.SqlQuery<int>("SELECT SCOPE_IDENTITY()").FirstOrDefault();

                // Gửi thông báo real-time cho quản lý về đặt bàn mới
                _notificationService.NotifyNewReservation(new ReservationNotification
                {
                    ReservationId = newReservationId,
                    ReservationCode = reservationCode,
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    GuestCount = model.NumberOfGuests,
                    ReservationDate = reservationDate.ToString("dd/MM/yyyy"),
                    ReservationTime = reservationTime.ToString(@"hh\:mm"),
                    TablePreference = model.TablePreference,
                    SpecialRequests = model.SpecialRequests
                });

                return Json(new
                {
                    success = true,
                    message = "Đặt bàn thành công!",
                    reservationCode = reservationCode,
                    redirectUrl = Url.Action("Confirmation", new { code = reservationCode })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Trang xác nhận đặt bàn
        /// </summary>
        public ActionResult Confirmation(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return RedirectToAction("Index");
            }

            var reservation = GetReservationByCode(code);
            if (reservation == null)
            {
                TempData["Error"] = "Không tìm thấy đặt bàn!";
                return RedirectToAction("Index");
            }

            return View(reservation);
        }

        /// <summary>
        /// Kiểm tra bàn trống (AJAX)
        /// </summary>
        [HttpPost]
        public JsonResult CheckAvailability(DateTime date, string time, int guests)
        {
            try
            {
                if (!TimeSpan.TryParse(time, out TimeSpan reservationTime))
                {
                    return Json(new { success = false, message = "Giờ không hợp lệ" });
                }

                var availableTables = GetAvailableTablesCount(date, reservationTime, guests);
                
                return Json(new
                {
                    success = true,
                    available = availableTables > 0,
                    count = availableTables,
                    message = availableTables > 0 
                        ? $"Còn {availableTables} bàn trống" 
                        : "Không còn bàn trống cho thời gian này"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy time slots có sẵn cho ngày (AJAX)
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableSlots(DateTime date, int guests)
        {
            var timeSlots = GetTimeSlots();
            var result = new List<object>();

            foreach (var slot in timeSlots)
            {
                if (TimeSpan.TryParse(slot, out TimeSpan time))
                {
                    var available = GetAvailableTablesCount(date, time, guests);
                    result.Add(new
                    {
                        time = slot,
                        available = available > 0,
                        count = available
                    });
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Hủy đặt bàn
        /// </summary>
        [HttpPost]
        public JsonResult Cancel(string code, string reason)
        {
            try
            {
                // Verify ownership if logged in
                var sql = @"
                    UPDATE Reservation 
                    SET Status = 'Cancelled', CancelReason = @p1, CancelledDate = GETDATE()
                    WHERE ReservationCode = @p0 AND Status IN ('Pending', 'Confirmed')";

                var affected = _db.Database.ExecuteSqlCommand(sql, code, reason);

                if (affected > 0)
                {
                    return Json(new { success = true, message = "Đã hủy đặt bàn thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Không thể hủy đặt bàn này!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách bàn trống cho thời gian cụ thể (AJAX)
        /// </summary>
        [HttpPost]
        public JsonResult GetAvailableTables(DateTime date, string time, int guests)
        {
            try
            {
                if (!TimeSpan.TryParse(time, out TimeSpan reservationTime))
                {
                    return Json(new { success = false, message = "Giờ không hợp lệ" });
                }

                var timeMinutes = (int)reservationTime.TotalMinutes;

                // Lấy danh sách bàn phù hợp
                var sql = @"
                    SELECT t.Id, t.TableNumber, t.Capacity, ta.Name as AreaName,
                           CASE 
                               WHEN EXISTS (
                                   SELECT 1 FROM Reservation r 
                                   WHERE r.TableId = t.Id 
                                     AND r.ReservationDate = @p1
                                     AND r.Status IN ('Pending', 'Confirmed')
                                     AND ABS(DATEDIFF(MINUTE, '00:00:00', r.ReservationTime) - @p2) < 120
                               ) THEN 0 
                               ELSE 1 
                           END as IsAvailable
                    FROM RestaurantTable t
                    LEFT JOIN TableArea ta ON t.TableAreaId = ta.Id
                    WHERE t.Capacity >= @p0
                    ORDER BY t.Capacity, t.TableNumber";

                var tables = _db.Database.SqlQuery<AvailableTableInfo>(sql, guests, date, timeMinutes).ToList();

                return Json(new
                {
                    success = true,
                    tables = tables.Select(t => new
                    {
                        id = t.Id,
                        tableNumber = t.TableNumber,
                        capacity = t.Capacity,
                        areaName = t.AreaName ?? "Chính",
                        isAvailable = t.IsAvailable
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private List<string> GetTimeSlots()
        {
            return new List<string>
            {
                "10:00", "10:30", "11:00", "11:30", "12:00", "12:30",
                "13:00", "13:30", "17:00", "17:30", "18:00", "18:30",
                "19:00", "19:30", "20:00", "20:30", "21:00"
            };
        }

        private int? FindAvailableTable(DateTime date, TimeSpan time, int guests, string preference)
        {
            try
            {
                // Convert TimeSpan to total minutes for SQL comparison
                var timeMinutes = (int)time.TotalMinutes;
                
                // Find tables that can accommodate the guests and are not reserved
                // Note: RestaurantTable uses 'Status' column (Available/Occupied/Reserved) instead of 'IsActive'
                var sql = @"
                    SELECT TOP 1 t.Id
                    FROM RestaurantTable t
                    WHERE t.Status = 'Available'
                      AND t.Capacity >= @p0
                      AND NOT EXISTS (
                          SELECT 1 FROM Reservation r 
                          WHERE r.TableId = t.Id 
                            AND r.ReservationDate = @p1
                            AND r.Status IN ('Pending', 'Confirmed')
                            AND ABS(DATEDIFF(MINUTE, '00:00:00', r.ReservationTime) - @p2) < 120
                      )
                    ORDER BY t.Capacity";

                var tableId = _db.Database.SqlQuery<int?>(sql, guests, date, timeMinutes).FirstOrDefault();
                return tableId;
            }
            catch
            {
                return null;
            }
        }

        private int GetAvailableTablesCount(DateTime date, TimeSpan time, int guests)
        {
            try
            {
                // Convert TimeSpan to total minutes for SQL comparison
                var timeMinutes = (int)time.TotalMinutes;
                
                // Note: RestaurantTable uses 'Status' column (Available/Occupied/Reserved) instead of 'IsActive'
                var sql = @"
                    SELECT COUNT(*)
                    FROM RestaurantTable t
                    WHERE t.Status IN ('Available', 'Occupied')
                      AND t.Capacity >= @p0
                      AND NOT EXISTS (
                          SELECT 1 FROM Reservation r 
                          WHERE r.TableId = t.Id 
                            AND r.ReservationDate = @p1
                            AND r.Status IN ('Pending', 'Confirmed')
                            AND ABS(DATEDIFF(MINUTE, '00:00:00', r.ReservationTime) - @p2) < 120
                      )";

                return _db.Database.SqlQuery<int>(sql, guests, date, timeMinutes).FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        private ReservationViewModel GetReservationByCode(string code)
        {
            try
            {
                var reservation = _db.Database.SqlQuery<ReservationInfo>(
                    @"SELECT r.Id, r.ReservationCode, r.CustomerName, r.CustomerPhone, r.CustomerEmail,
                             r.ReservationDate, r.ReservationTime, r.NumberOfGuests, r.TablePreference,
                             r.SpecialRequests, r.Status, r.CreatedDate, t.TableName
                      FROM Reservation r
                      LEFT JOIN [Table] t ON r.TableId = t.Id
                      WHERE r.ReservationCode = @p0", code).FirstOrDefault();

                if (reservation == null) return null;

                return new ReservationViewModel
                {
                    Id = reservation.Id,
                    ReservationCode = reservation.ReservationCode,
                    ReservationDate = reservation.ReservationDate,
                    ReservationTime = reservation.ReservationTime,
                    NumberOfGuests = reservation.NumberOfGuests,
                    TableInfo = reservation.TableName ?? reservation.TablePreference,
                    Status = reservation.Status,
                    StatusClass = GetStatusClass(reservation.Status),
                    StatusText = GetStatusText(reservation.Status),
                    CanCancel = reservation.Status == "Pending" || reservation.Status == "Confirmed",
                    CanModify = reservation.Status == "Pending",
                    CreatedDate = reservation.CreatedDate
                };
            }
            catch
            {
                return null;
            }
        }

        private string GetStatusClass(string status)
        {
            switch (status)
            {
                case "Pending": return "warning";
                case "Confirmed": return "success";
                case "Completed": return "info";
                case "Cancelled": return "danger";
                case "NoShow": return "secondary";
                default: return "secondary";
            }
        }

        private string GetStatusText(string status)
        {
            switch (status)
            {
                case "Pending": return "Chờ xác nhận";
                case "Confirmed": return "Đã xác nhận";
                case "Completed": return "Hoàn thành";
                case "Cancelled": return "Đã hủy";
                case "NoShow": return "Không đến";
                default: return status;
            }
        }

        #endregion

        #region Helper Classes

        private class ReservationInfo
        {
            public int Id { get; set; }
            public string ReservationCode { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
            public string CustomerEmail { get; set; }
            public DateTime ReservationDate { get; set; }
            public TimeSpan ReservationTime { get; set; }
            public int NumberOfGuests { get; set; }
            public string TablePreference { get; set; }
            public string SpecialRequests { get; set; }
            public string Status { get; set; }
            public DateTime CreatedDate { get; set; }
            public string TableName { get; set; }
        }

        private class AvailableTableInfo
        {
            public int Id { get; set; }
            public string TableNumber { get; set; }
            public int Capacity { get; set; }
            public string AreaName { get; set; }
            public int IsAvailable { get; set; } // 1: Có sẵn, 0: Không có sẵn
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
