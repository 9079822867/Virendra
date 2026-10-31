using System.Configuration;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Reseller API documentation (v3: /transaction, /user/api, /report/api + webhooks).
    /// One page with a section per endpoint; operator and circle codes come from the database.
    /// </summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.ApiUser)]
    public class ApiDocumentController : Controller
    {
        public ActionResult Index()
        {
            var rc = new RechargeRepository();
            ViewBag.Operators = rc.GetActiveOperators();
            ViewBag.Circles   = rc.GetCircles();
            ViewBag.BaseUrl   = (Request.Url.GetLeftPart(System.UriPartial.Authority) + Url.Content("~/")).TrimEnd('/');

            string conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString;
            if (Session["UserId"] is int uid && !string.IsNullOrEmpty(conn))
                ViewBag.Me = new UserRepository(conn).GetUserById(uid);

            return View();
        }

        // Older one-topic pages now open the matching section of the single docs page
        public ActionResult RechargeAPI()       => Section("recharge");
        public ActionResult OperatorCode()      => Section("operators");
        public ActionResult CircleCode()        => Section("circles");
        public ActionResult Balance()           => Section("wallets");
        public ActionResult Status()            => Section("status");
        public ActionResult Complaint()         => Section("dispute-raise");
        public ActionResult FilesCallbackInfo() => Section("webhook-recharge");

        private ActionResult Section(string anchor) => Redirect(Url.Action("Index") + "#" + anchor);
    }
}
