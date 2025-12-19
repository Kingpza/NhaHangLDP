using System;
using System.Web.Mvc;
using NhaHangLDP.Models;

namespace NhaHangLDP.Controllers
{
    public class HomeController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        public ActionResult Index()
        {
            return View();
        }
        
        public ActionResult Booking()
        {
            return View();
        }

        [HttpPost]
        public JsonResult CreateBooking(string customerName, string customerPhone, string customerEmail, 
                                       string bookingDate, string bookingTime, int numberOfGuests, string notes)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerPhone))
                {
                    return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin!" });
                }

                if (numberOfGuests <= 0 || numberOfGuests > 50)
                {
                    return Json(new { success = false, message = "Số lượng khách không hợp lệ!" });
                }

                // Parse date and time
                DateTime bookingDateTime;
                try
                {
                    var dateStr = bookingDate + " " + bookingTime;
                    bookingDateTime = DateTime.ParseExact(dateStr, "yyyy-MM-dd HH:mm", null);
                }
                catch
                {
                    return Json(new { success = false, message = "Ngày giờ không hợp lệ!" });
                }

                // Check if booking time is in the past
                if (bookingDateTime < DateTime.Now)
                {
                    return Json(new { success = false, message = "Không thể đặt bàn cho thời gian trong quá khứ!" });
                }

                // Create booking record
                var booking = new Booking
                {
                    CustomerName = customerName.Trim(),
                    CustomerPhone = customerPhone.Trim(),
                    BookingDateTime = bookingDateTime,
                    NumberOfGuests = numberOfGuests,
                    Status = "Pending",
                    Notes = notes?.Trim(),
                    CreatedDate = DateTime.Now,
                    TableId = null // Will be assigned by staff later
                };

                db.Booking.Add(booking);
                db.SaveChanges();

                // Generate booking code
                string bookingCode = "BK" + booking.Id.ToString("D6");

                return Json(new 
                { 
                    success = true, 
                    message = "Đặt bàn thành công!",
                    bookingCode = bookingCode,
                    bookingId = booking.Id
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Booking Error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi đặt bàn. Vui lòng thử lại!" });
            }
        }

        // GET: Demo tính năng đồng bộ đặt bàn
        public ActionResult BookingDemo()
        {
            return View();
        }
        
        // de test git pull
        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}