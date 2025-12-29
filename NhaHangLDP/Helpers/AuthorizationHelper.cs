using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Helpers
{
    public static class AuthorizationHelper
    {
        public static bool IsAdminOrManager(HttpSessionStateBase session)
        {
            if (session["UserRole"] == null)
                return false;

            var role = session["UserRole"].ToString().ToLower();
            return role == "admin" || role == "manager";
        }

        public static ActionResult RedirectToLoginIfUnauthorized(HttpSessionStateBase session, IUrlHelper url)
        {
            if (!IsAdminOrManager(session))
            {
                return new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" }
                });
            }
            return null;
        }
    }
}
