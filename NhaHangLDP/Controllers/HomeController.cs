using System;
using NhaHangLDP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    public class HomeController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        public ActionResult Index()
        {
            return View();
        }

        // Chuyển sang dùng Reservation/Index thay cho Home/Booking
        // public ActionResult Booking()
        // {
        //     return RedirectToAction("Index", "Reservation");
        // }

        // Xóa CreateBooking - Dùng ReservationController thay thế
        // [HttpPost]
        // public JsonResult CreateBooking(...)
        // { ... }

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