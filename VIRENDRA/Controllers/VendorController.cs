using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
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

        // ── Response setting: how to read status / txn ids out of one URL's response ──

        [HttpGet]
        public ActionResult ResponseSetting(int id)
        {
            var vm = _vendorRepo.GetResponseSetting(id);
            if (vm == null) return HttpNotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResponseSetting(int urlId, string rowsJson)
        {
            var vm = _vendorRepo.GetResponseSetting(urlId);
            if (vm == null) return HttpNotFound();

            List<TagValueRow> rows;
            try { rows = JsonConvert.DeserializeObject<List<TagValueRow>>(rowsJson ?? "[]") ?? new List<TagValueRow>(); }
            catch (JsonException)
            {
                TempData["ErrorMessage"] = "Could not read the rows. Please try again.";
                return RedirectToAction("ResponseSetting", new { id = urlId });
            }

            var validTags = new HashSet<int>(vm.Tags.Select(t => t.Id));
            if (rows.Any(r => !validTags.Contains(r.TagId)))
            {
                TempData["ErrorMessage"] = "Every row needs a TagName.";
                return RedirectToAction("ResponseSetting", new { id = urlId });
            }

            _vendorRepo.SaveTagValues(urlId, vm.ApiId, rows, Session["UserId"] as int? ?? 0);

            TempData["SuccessMessage"] = "Response setting saved (" + rows.Count + " rows).";
            return RedirectToAction("ResponseSetting", new { id = urlId });
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

        private List<ApiUrl> BuildApiUrls(int apiSourceId, string[] urlType,
            string[] url, string[] method, string[] responseType, string[] postParameter)
        {
            var list = new List<ApiUrl>();
            if (urlType == null) return list;

            for (int i = 0; i < urlType.Length; i++)
            {
                var apiUrl = new ApiUrl
                {
                    ApiId    = apiSourceId,
                    UrlType  = urlType[i],
                    URL      = url     != null && i < url.Length     ? url[i]          : null,
                    Method   = method  != null && i < method.Length   ? method[i]       : "GET",
                    ResType  = responseType != null && i < responseType.Length ? responseType[i] : "JSON (application/json)",
                    PostData = postParameter != null && i < postParameter.Length ? postParameter[i] : null,
                };

                // CallBack URL (ApiUrlType 4) left blank → auto-generate our own callback endpoint
                if (urlType[i] == "CallBack" && string.IsNullOrWhiteSpace(apiUrl.URL))
                {
                    string callbackId = _vendorRepo.GetCallbackId(apiSourceId);
                    if (string.IsNullOrWhiteSpace(callbackId))
                    {
                        callbackId = GetUniqueAlphaNumeric(12);
                        _vendorRepo.SetCallbackId(apiSourceId, callbackId);
                    }
                    apiUrl.URL = GetApiDomainName() + "service/reccallback/" + callbackId;
                }

                list.Add(apiUrl);
            }
            return list;
        }

        /// <summary>Site root ending in "/" — appSetting "ApiDomainName" if set, else the current request host.</summary>
        private string GetApiDomainName()
        {
            string domain = ConfigurationManager.AppSettings["ApiDomainName"];
            if (string.IsNullOrWhiteSpace(domain))
                domain = Request.Url.GetLeftPart(UriPartial.Authority) + Url.Content("~/");
            return domain.EndsWith("/") ? domain : domain + "/";
        }

        private static string GetUniqueAlphaNumeric(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            var sb = new StringBuilder(length);
            foreach (byte b in bytes)
                sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }
    }
}
