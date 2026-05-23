using System.Web.Mvc;

namespace VIRENDRA.Controllers
{
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
