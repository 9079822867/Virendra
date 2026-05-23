using System.Web.Mvc;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class ServicesController : Controller
    {
        // GET: Services
        public ActionResult Index()
        {
            return View();
        }

        // GET: Services/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FormCollection collection)
        {
            TempData["SuccessMessage"] = "Service saved successfully.";
            return RedirectToAction("Index");
        }

        // GET: Services/Edit/5
        public ActionResult Edit(int? id)
        {
            ViewBag.ServiceId = id;
            return View();
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, FormCollection collection)
        {
            TempData["SuccessMessage"] = "Service updated successfully.";
            return RedirectToAction("Index");
        }
    }
}
