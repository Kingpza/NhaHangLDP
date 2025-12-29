using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace NhaHangLDP.Helpers
{
    public static class AuthorizationHelper
    {
        public static bool IsAdminOrManager(ISession session)
        {
            var role = session.GetString("UserRole");
            if (string.IsNullOrEmpty(role))
                return false;

            role = role.ToLower();
            return role == "admin" || role == "manager";
        }

        public static ActionResult RedirectToLoginIfUnauthorized(ISession session, IUrlHelper url)
        {
            if (!IsAdminOrManager(session))
            {
                return new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" }
                });
            }
            return null;
        }
    }
}
