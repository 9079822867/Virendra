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
            var vendors = _vendorRepo.GetAllVendors();
            return View(vendors);
        }

        [HttpGet]
        public ActionResult Create()
        {
            var model = new Vendor { IsActive = true, CheckTime = 0 };
            model.VendorUrls = _vendorRepo.GetVendorUrls(0); // blank URL rows
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Vendor model, string[] urlType, string[] url,
            string[] method, string[] responseType, string[] postParameter)
        {
            if (!ModelState.IsValid)
                return View(model);

            int newId = _vendorRepo.CreateVendor(model);

            var urls = BuildVendorUrls(newId, urlType, url, method, responseType, postParameter);
            _vendorRepo.SaveVendorUrls(newId, urls);

            TempData["SuccessMessage"] = "Vendor created successfully.";
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
        public ActionResult Edit(Vendor model, string[] urlType, string[] url,
            string[] method, string[] responseType, string[] postParameter)
        {
            if (!ModelState.IsValid)
            {
                model.VendorUrls = _vendorRepo.GetVendorUrls(model.Id);
                return View(model);
            }

            _vendorRepo.UpdateVendor(model);

            var urls = BuildVendorUrls(model.Id, urlType, url, method, responseType, postParameter);
            _vendorRepo.SaveVendorUrls(model.Id, urls);

            TempData["SuccessMessage"] = "Vendor updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            _vendorRepo.DeleteVendor(id);
            TempData["SuccessMessage"] = "Vendor deleted.";
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

        private static List<VendorUrl> BuildVendorUrls(int vendorId, string[] urlType,
            string[] url, string[] method, string[] responseType, string[] postParameter)
        {
            var list = new List<VendorUrl>();
            if (urlType == null) return list;

            for (int i = 0; i < urlType.Length; i++)
            {
                list.Add(new VendorUrl
                {
                    VendorId      = vendorId,
                    UrlType       = urlType[i],
                    Url           = url != null && i < url.Length ? url[i] : null,
                    Method        = method != null && i < method.Length ? method[i] : "GET",
                    ResponseType  = responseType != null && i < responseType.Length ? responseType[i] : "JSON (application/json)",
                    PostParameter = postParameter != null && i < postParameter.Length ? postParameter[i] : null,
                });
            }
            return list;
        }
    }
}
