using System.Web.Mvc;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class RechargeController : Controller
    {
        // GET: Recharge
        public ActionResult Index()
        {
            return View();
        }

        // POST: Recharge
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(FormCollection collection)
        {
            TempData["SuccessMessage"] = "Recharge request submitted successfully.";
            return RedirectToAction("Index");
        }
    }
}
