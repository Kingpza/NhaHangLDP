using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace NhaHangLDP.Filters
{
    /// <summary>
    /// Custom Authorization Attribute để kiểm tra quyền truy cập
    /// </summary>
    public class CustomAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string[] _allowedRoles;

        public CustomAuthorizeAttribute(params string[] roles)
        {
            _allowedRoles = roles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Kiểm tra session có tồn tại không
            var session = httpContext.Session;
            if (session == null || session["Username"] == null || session["UserRole"] == null)
            {
                return false;
            }

            // Nếu không có role nào được chỉ định, chỉ cần đăng nhập
            if (_allowedRoles == null || _allowedRoles.Length == 0)
            {
                return true;
            }

            // Kiểm tra role
            var userRole = session["UserRole"].ToString().ToLower();
            foreach (var role in _allowedRoles)
            {
                if (userRole == role.ToLower())
                {
                    return true;
                }
            }

            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            
            // Nếu chưa đăng nhập
            if (session == null || session["Username"] == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "returnUrl", filterContext.HttpContext.Request.RawUrl }
                    });
            }
            else
            {
                // Nếu đã đăng nhập nhưng không đủ quyền
                filterContext.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml",
                    ViewData = new ViewDataDictionary
                    {
                        ["RequiredRoles"] = string.Join(", ", _allowedRoles),
                        ["UserRole"] = session["UserRole"]?.ToString()
                    }
                };
            }
        }
    }

    /// <summary>
    /// Attribute để yêu cầu role Admin hoặc Manager
    /// </summary>
    public class AdminOrManagerAttribute : CustomAuthorizeAttribute
    {
        public AdminOrManagerAttribute() : base("Admin", "Manager") { }
    }

    /// <summary>
    /// Attribute để yêu cầu role Cashier
    /// </summary>
    public class CashierAttribute : CustomAuthorizeAttribute
    {
        public CashierAttribute() : base("Cashier", "Thu ngân", "Thu_ngan") { }
    }

    /// <summary>
    /// Attribute để yêu cầu đăng nhập (bất kỳ role nào)
    /// </summary>
    public class RequireLoginAttribute : CustomAuthorizeAttribute
    {
        public RequireLoginAttribute() : base() { }
    }
}
