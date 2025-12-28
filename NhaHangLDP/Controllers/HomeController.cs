using System;
using NhaHangLDP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;

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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public ActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            
            var model = new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = exceptionHandlerPathFeature?.Error?.Message,
                StackTrace = exceptionHandlerPathFeature?.Error?.StackTrace
            };

            return View(model);
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