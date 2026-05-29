using System;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class CommonRoutingController : Controller
    {
        private readonly ICommonRoutingRepository _repo;
        private readonly IUserRepository          _userRepo;
        private readonly IVendorRepository        _vendorRepo;

        public CommonRoutingController(
            ICommonRoutingRepository repo,
            IUserRepository          userRepo,
            IVendorRepository        vendorRepo)
        {
            _repo       = repo;
            _userRepo   = userRepo;
            _vendorRepo = vendorRepo;
        }

        // ── Shared helpers ──────────────────────────────────────────

        private void PopulateDropdowns(int? selOp = null, int? selApi = null,
                                       int? selFType = 1, string selCircle = "All")
        {
            ViewBag.OperatorList = new SelectList(
                _userRepo.GetAllOperators(), "Id", "Name", selOp);

            ViewBag.VendorList = new SelectList(
                _vendorRepo.GetAllVendors(), "Id", "ApiName", selApi);

            ViewBag.FilterTypeList = new SelectList(new[]
            {
                new { Value = 1, Text = "Range"   },
                new { Value = 2, Text = "Amounts" },
            }, "Value", "Text", selFType);

            ViewBag.CircleList = new SelectList(
                _userRepo.GetAllCircles(), "CircleName", "CircleName", selCircle);
        }

        // ── Index ────────────────────────────────────────────────────

        public ActionResult Index()
        {
            var list = _repo.GetAll();
            return View(list);
        }

        // ── Create ───────────────────────────────────────────────────

        [HttpGet]
        public ActionResult Create()
        {
            PopulateDropdowns();
            return View(new CommonRouting { IsActive = true, FTypeId = 1,
                                            CircleFilter = "All", UserFilter = "All" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CommonRouting model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model.OpId, model.ApiId, model.FTypeId, model.CircleFilter);
                return View(model);
            }

            model.AddedById = (int?)Session["UserId"];

            try
            {
                _repo.Create(model);
                TempData["SuccessMessage"] = "Route created successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                PopulateDropdowns(model.OpId, model.ApiId, model.FTypeId, model.CircleFilter);
                return View(model);
            }
        }

        // ── Edit ─────────────────────────────────────────────────────

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var model = _repo.GetById(id);
            if (model == null) return HttpNotFound();

            PopulateDropdowns(model.OpId, model.ApiId, model.FTypeId, model.CircleFilter);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CommonRouting model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model.OpId, model.ApiId, model.FTypeId, model.CircleFilter);
                return View(model);
            }

            model.UpdatedById = (int?)Session["UserId"];

            try
            {
                _repo.Update(model);
                TempData["SuccessMessage"] = "Route updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                PopulateDropdowns(model.OpId, model.ApiId, model.FTypeId, model.CircleFilter);
                return View(model);
            }
        }

        // ── Delete ───────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                _repo.Delete(id);
                TempData["SuccessMessage"] = "Route deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Cannot delete: " + ex.Message;
            }
            return RedirectToAction("Index");
        }

        // ── Toggle Active (AJAX) ─────────────────────────────────────

        [HttpPost]
        public JsonResult ToggleActive(int id, bool isActive)
        {
            try
            {
                _repo.ToggleActive(id, isActive);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
