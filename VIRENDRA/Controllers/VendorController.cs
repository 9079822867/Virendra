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
