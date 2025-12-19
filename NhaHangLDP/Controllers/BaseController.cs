using System.Web.Mvc;
using System.Web.Routing;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Base Controller với authentication check
    /// </summary>
    public class BaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Kiểm tra session có tồn tại không
            if (Session["Username"] == null || Session["UserRole"] == null)
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
            var userRole = Session["UserRole"]?.ToString();
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
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
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
            return Session["Username"]?.ToString();
        }

        /// <summary>
        /// Lấy FullName hiện tại
        /// </summary>
        protected string GetCurrentFullName()
        {
            return Session["FullName"]?.ToString() ?? Session["Username"]?.ToString();
        }
    }
}
