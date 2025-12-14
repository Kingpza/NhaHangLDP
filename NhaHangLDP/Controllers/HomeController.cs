using System.Web.Mvc;

namespace NhaHangLDP.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
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
    }
}