using System;
using System.Net;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Infrastructure
{
    /// <summary>
    /// Global filter: a logged-in non-admin user (API User / Retailer) whose 4 KYC
    /// documents are not all approved can only reach the KYC page (KycController)
    /// and Auth (logout). Everything else redirects to Kyc/Index.
    /// Checked on every request, so access opens on the next request after an admin
    /// approves (and closes again if a document is later rejected).
    /// Session["KycApproved"] mirrors the result for the layout menu.
    /// </summary>
    public class KycGateAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.IsChildAction) return;

            var session = filterContext.HttpContext.Session;
            if (session == null || session["UserId"] == null) return;

            byte roleId = session["RoleId"] is byte r ? r : RoleConstants.Retailer;
            if (roleId == RoleConstants.SuperAdmin || roleId == RoleConstants.Admin)
            {
                session["KycApproved"] = true;
                return;
            }

            string controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (string.Equals(controller, "Kyc",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase))
                return;

            bool approved;
            try
            {
                approved = new KycRepository().IsApproved(Convert.ToInt32(session["UserId"]));
            }
            catch
            {
                approved = false;   // fail closed
            }

            session["KycApproved"] = approved;
            if (approved) return;

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden, "KYC approval pending");
                return;
            }

            filterContext.Controller.TempData["ErrorMessage"] =
                "Please complete your KYC. The portal opens once an admin approves all documents.";
            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary(new { controller = "Kyc", action = "Index" }));
        }
    }
}
