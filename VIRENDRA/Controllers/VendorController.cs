using System;
using System.Collections.Generic;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class VendorController : Controller
    {
        private readonly IVendorRepository _vendorRepo;

        public VendorController(IVendorRepository vendorRepo)
        {
            _vendorRepo = vendorRepo;
        }

        public ActionResult Index()
        {
            return View(_vendorRepo.GetAllVendors());
        }

        [HttpGet]
        public ActionResult Create()
        {
            var model = new ApiSource { IsActive = true, StatusCheckTime = 0 };
            model.ApiUrls = _vendorRepo.GetVendorUrls(0);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ApiSource model, string[] urlType, string[] url,
            string[] method, string[] responseType, string[] postParameter)
        {
            if (!ModelState.IsValid)
                return View(model);

            int newId = _vendorRepo.CreateVendor(model);

            var urls = BuildApiUrls(newId, urlType, url, method, responseType, postParameter);
            _vendorRepo.SaveVendorUrls(newId, urls);

            TempData["SuccessMessage"] = "API source created successfully.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var vendor = _vendorRepo.GetVendorById(id);
            if (vendor == null) return HttpNotFound();
            return View(vendor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ApiSource model, string[] urlType, string[] url,
            string[] method, string[] responseType, string[] postParameter)
        {
            if (!ModelState.IsValid)
            {
                model.ApiUrls = _vendorRepo.GetVendorUrls(model.Id);
                return View(model);
            }

            _vendorRepo.UpdateVendor(model);

            var urls = BuildApiUrls(model.Id, urlType, url, method, responseType, postParameter);
            _vendorRepo.SaveVendorUrls(model.Id, urls);

            TempData["SuccessMessage"] = "API source updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            _vendorRepo.DeleteVendor(id);
            TempData["SuccessMessage"] = "API source deleted.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public JsonResult ToggleActive(int id, bool isActive)
        {
            try
            {
                _vendorRepo.ToggleActive(id, isActive);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ── Operator-code margin (vendor op-codes + commission) ──────────────

        [HttpGet]
        public ActionResult ApiOpcode(int id)
        {
            var model = _vendorRepo.GetVendorMargins(id);
            if (model == null) return HttpNotFound();

            ViewBag.AmtTypes  = AmtTypeOptions();
            ViewBag.CommTypes = CommTypeOptions();
            return View(model);
        }

        [HttpPost]
        public JsonResult ApiOpcode(List<OperatorMarginDto> data)
        {
            try
            {
                if (data == null || data.Count == 0)
                    return Json(new { success = false, message = "No data received." });

                int apiId  = data[0].ApiId;
                int userId = Session["UserId"] is int uid ? uid : 0;

                _vendorRepo.SaveVendorMargins(apiId, userId, data);
                return Json(new { success = true, message = "Operator codes and commission saved." });
            }
            catch (Exception ex)
            {
                string msg = ex.GetBaseException().Message;
                return Json(new { success = false, message = msg });
            }
        }

        // Commission-relevant amount types (see AmtType table: 5=Discount, 4=Surcharge)
        private static List<SelectListItem> AmtTypeOptions() => new List<SelectListItem>
        {
            new SelectListItem { Value = "5", Text = "Discount"  },
            new SelectListItem { Value = "4", Text = "Surcharge" }
        };

        // Commission types (see CommType table: 1=Flat, 2=Percent, 3=Range)
        private static List<SelectListItem> CommTypeOptions() => new List<SelectListItem>
        {
            new SelectListItem { Value = "2", Text = "Percentage" },
            new SelectListItem { Value = "1", Text = "Flat"       },
            new SelectListItem { Value = "3", Text = "Range"      }
        };

        private static List<ApiUrl> BuildApiUrls(int apiSourceId, string[] urlType,
            string[] url, string[] method, string[] responseType, string[] postParameter)
        {
            var list = new List<ApiUrl>();
            if (urlType == null) return list;

            for (int i = 0; i < urlType.Length; i++)
            {
                list.Add(new ApiUrl
                {
                    ApiId    = apiSourceId,
                    UrlType  = urlType[i],
                    URL      = url     != null && i < url.Length     ? url[i]          : null,
                    Method   = method  != null && i < method.Length   ? method[i]       : "GET",
                    ResType  = responseType != null && i < responseType.Length ? responseType[i] : "JSON (application/json)",
                    PostData = postParameter != null && i < postParameter.Length ? postParameter[i] : null,
                });
            }
            return list;
        }
    }
}
