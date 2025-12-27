using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Base Controller với authentication check
    /// </summary>
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Kiểm tra session có tồn tại không
            var username = HttpContext.Session.GetString("Username");
            var userRole = HttpContext.Session.GetString("UserRole");
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userRole))
            {
                // Nếu không có session, redirect về trang đăng nhập
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" }
                    });
            }

            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// Kiểm tra role có quyền truy cập không
        /// </summary>
        protected bool IsInRole(params string[] roles)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole))
                return false;

            userRole = userRole.ToLower();
            foreach (var role in roles)
            {
                if (userRole == role.ToLower())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra có phải Admin/Manager không
        /// </summary>
        protected bool IsAdminOrManager()
        {
            return IsInRole("Admin", "Manager");
        }

        /// <summary>
        /// Kiểm tra có phải Cashier không
        /// </summary>
        protected bool IsCashier()
        {
            return IsInRole("Cashier", "Thu ngân", "Thu_ngan");
        }

        /// <summary>
        /// Lấy UserId hiện tại
        /// </summary>
        protected int? GetCurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// Lấy Username hiện tại
        /// </summary>
        protected string GetCurrentUsername()
        {
            return HttpContext.Session.GetString("Username");
        }

        /// <summary>
        /// Lấy FullName hiện tại
        /// </summary>
        protected string GetCurrentFullName()
        {
            return HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username");
        }
    }
}
