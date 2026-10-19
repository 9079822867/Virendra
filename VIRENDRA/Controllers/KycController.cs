using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// The logged-in user's KYC: upload Aadhaar front/back, PAN card and selfie.
    /// Always reachable (KycGateAttribute lets this controller through).
    /// Files are stored by KycService (never served by IIS) and streamed by Document()
    /// to the owner or an admin only.
    /// </summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class KycController : Controller
    {
        private readonly KycRepository _kycRepo = new KycRepository();
        private readonly KycService    _kycSvc;

        public KycController()
        {
            _kycSvc = new KycService(_kycRepo);
        }

        private int  CurrentUserId => (int)Session["UserId"];
        private bool IsAdmin => Session["RoleId"] is byte r && (r == RoleConstants.SuperAdmin || r == RoleConstants.Admin);

        // ── KYC status / upload page ────────────────────────────────────────

        [HttpGet]
        public ActionResult Index()
        {
            var model = _kycRepo.GetStatus(CurrentUserId);
            if (!IsAdmin) Session["KycApproved"] = model.IsApproved;
            ViewBag.PanApiEnabled = KycIdService.ApiEnabled;
            return View(model);
        }

        // ── Aadhaar / PAN numbers ───────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitAadhaar(string aadhaarNumber)
        {
            return Done(new KycIdService(_kycRepo).SubmitAadhaar(CurrentUserId, aadhaarNumber));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SubmitPan(string panNumber, string panName, string panDob, bool consent = false)
        {
            return Done(await new KycIdService(_kycRepo).SubmitPanAsync(CurrentUserId, panNumber, panName, panDob, consent));
        }

        private ActionResult Done(KycIdResult r)
        {
            TempData[r.Ok ? "SuccessMessage" : "ErrorMessage"] = r.Message;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(string category, HttpPostedFileBase file)
        {
            byte[] data = null;
            if (file != null && file.ContentLength > 0 && file.ContentLength <= KycConstants.MaxFileBytes)
            {
                using (var ms = new MemoryStream())
                {
                    file.InputStream.CopyTo(ms);
                    data = ms.ToArray();
                }
            }
            else if (file != null && file.ContentLength > KycConstants.MaxFileBytes)
            {
                TempData["ErrorMessage"] = KycConstants.CategoryTitle(category) + ": file must be 2 MB or smaller.";
                return RedirectToAction("Index");
            }

            string error = _kycSvc.Upload(CurrentUserId, category, file?.FileName, data);
            if (error != null)
                TempData["ErrorMessage"] = error;
            else
                TempData["SuccessMessage"] = KycConstants.CategoryTitle(category) + " uploaded. It will be reviewed by the admin.";

            return RedirectToAction("Index");
        }

        // ── Secure file view (owner or admin) ───────────────────────────────

        [HttpGet]
        public ActionResult Document(int? userId, string category)
        {
            if (!IsAdmin && userId.HasValue && userId.Value != CurrentUserId)
                return new HttpStatusCodeResult(403);

            int ownerId = IsAdmin && userId.HasValue ? userId.Value : CurrentUserId;
            string path = _kycSvc.GetFilePath(ownerId, category);
            if (path == null)
                return HttpNotFound();

            Response.Cache.SetCacheability(HttpCacheability.Private);
            Response.Cache.SetNoStore();
            return File(path, KycService.MimeType(path));
        }
    }
}
