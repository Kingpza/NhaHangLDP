using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace NhaHangLDP.Filters
{
    /// <summary>
    /// Custom Authorization Attribute để kiểm tra quyền truy cập (ASP.NET Core)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CustomAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        public CustomAuthorizeAttribute(params string[] roles)
        {
            _allowedRoles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Kiểm tra session có tồn tại không
            var session = context.HttpContext.Session;
            var username = session.GetString("Username");
            var userRole = session.GetString("UserRole");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userRole))
            {
                // Nếu chưa đăng nhập, redirect về trang đăng nhập
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" },
                        { "returnUrl", returnUrl }
                    });
                return;
            }

            // Nếu không có role nào được chỉ định, chỉ cần đăng nhập
            if (_allowedRoles == null || _allowedRoles.Length == 0)
            {
                return;
            }

            // Kiểm tra role
            var userRoleLower = userRole.ToLower();
            foreach (var role in _allowedRoles)
            {
                if (userRoleLower == role.ToLower())
                {
                    return;
                }
            }

            // Nếu đã đăng nhập nhưng không đủ quyền
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/AccessDenied.cshtml",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                {
                    ["RequiredRoles"] = string.Join(", ", _allowedRoles),
                    ["UserRole"] = userRole
                }
            };
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
