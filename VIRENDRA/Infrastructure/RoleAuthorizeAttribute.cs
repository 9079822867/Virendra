using System.Web;
using System.Web.Mvc;

namespace VIRENDRA.Infrastructure
{
    /// <summary>
    /// Restricts access to controllers/actions based on the RoleId stored in Session.
    /// Usage: [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    /// </summary>
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly byte[] _allowedRoles;

        public RoleAuthorizeAttribute(params byte[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (!base.AuthorizeCore(httpContext))
                return false;

            var session = httpContext.Session;
            if (session == null || session["RoleId"] == null)
                return false;

            byte roleId = (byte)session["RoleId"];
            foreach (byte allowed in _allowedRoles)
            {
                if (roleId == allowed)
                    return true;
            }
            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (filterContext.HttpContext.User.Identity.IsAuthenticated &&
                (session == null || session["RoleId"] == null))
            {
                // Session expired but the forms cookie survived (Remember me): sign in again
                // instead of showing "Unauthorized" forever.
                System.Web.Security.FormsAuthentication.SignOut();
                base.HandleUnauthorizedRequest(filterContext);
                return;
            }

            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // Logged in but wrong role → 403 page
                filterContext.Result = new ViewResult { ViewName = "Unauthorized" };
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}
