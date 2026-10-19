using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    /// <summary>KYC approval for SuperAdmin / Admin: list, review, approve / reject documents.</summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class KycAdminController : Controller
    {
        private readonly KycRepository _kycRepo = new KycRepository();

        private int CurrentUserId => (int)Session["UserId"];

        private static readonly string[] Filters = { "Pending", "Rejected", "Approved", "Incomplete", "All" };

        [HttpGet]
        public ActionResult Index(string status = "Pending")
        {
            if (!Filters.Contains(status)) status = "Pending";

            var all = _kycRepo.GetUserSummaries();
            var model = new KycAdminIndexViewModel
            {
                Status        = status,
                PendingTotal  = all.Count(u => u.OverallStatus == "Pending"),
                RejectedTotal = all.Count(u => u.OverallStatus == "Rejected"),
                ApprovedTotal = all.Count(u => u.OverallStatus == "Approved"),
                Users         = status == "All" ? all : all.Where(u => u.OverallStatus == status).ToList()
            };
            return View(model);
        }

        [HttpGet]
        public ActionResult Review(int id)
        {
            var user = _kycRepo.GetUserSummaries(id).FirstOrDefault();
            if (user == null)
            {
                TempData["ErrorMessage"] = "This user has not uploaded any KYC documents.";
                return RedirectToAction("Index");
            }

            ViewBag.PanApiEnabled = KycIdService.ApiEnabled;
            return View(new KycAdminReviewViewModel
            {
                User = user,
                Kyc  = _kycRepo.GetStatus(id)
            });
        }

        // ── Aadhaar / PAN numbers ───────────────────────────────────────────

        /// <param name="type">"Aadhaar" or "Pan"</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyNumber(int userId, string type)
        {
            bool aadhaar = type == "Aadhaar";
            if (!aadhaar && type != "Pan") return Done(userId, null, "Invalid request.");

            var d = _kycRepo.GetDetail(userId);
            if ((aadhaar ? d?.AadhaarNumber : d?.PanNumber) == null)
                return Done(userId, null, (aadhaar ? "Aadhaar number" : "PAN") + " has not been submitted.");

            _kycRepo.SetNumberStatus(userId, aadhaar, KycConstants.Approved, null, CurrentUserId);
            return Done(userId, (aadhaar ? "Aadhaar number" : "PAN") + " verified.", null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectNumber(int userId, string type, string remark)
        {
            bool aadhaar = type == "Aadhaar";
            if (!aadhaar && type != "Pan") return Done(userId, null, "Invalid request.");

            remark = (remark ?? "").Trim();
            if (remark.Length == 0) return Done(userId, null, "Please enter a reason for rejection.");
            if (remark.Length > 250) remark = remark.Substring(0, 250);

            _kycRepo.SetNumberStatus(userId, aadhaar, KycConstants.Rejected, remark, CurrentUserId);
            return Done(userId, (aadhaar ? "Aadhaar number" : "PAN") + " rejected.", null);
        }

        /// <summary>Manual update: admin enters / corrects the numbers, saved as verified.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateNumbers(int userId, string aadhaarNumber, string panNumber, string panName, string panDob)
        {
            var r = new KycIdService(_kycRepo).AdminUpdate(userId, aadhaarNumber, panNumber, panName, panDob, CurrentUserId);
            return r.Ok ? Done(userId, r.Message, null) : Done(userId, null, r.Message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int userId, string category)
        {
            if (!KycConstants.IsValidCategory(category) || _kycRepo.GetDocument(userId, category) == null)
                return Done(userId, null, "Document not found.");

            _kycRepo.SetStatus(userId, category, KycConstants.Approved, null, CurrentUserId);
            return Done(userId, KycConstants.CategoryTitle(category) + " approved.", null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int userId, string category, string remark)
        {
            remark = (remark ?? "").Trim();
            if (remark.Length == 0)
                return Done(userId, null, "Please enter a reason for rejection.");
            if (remark.Length > 250) remark = remark.Substring(0, 250);

            if (!KycConstants.IsValidCategory(category) || _kycRepo.GetDocument(userId, category) == null)
                return Done(userId, null, "Document not found.");

            _kycRepo.SetStatus(userId, category, KycConstants.Rejected, remark, CurrentUserId);
            return Done(userId, KycConstants.CategoryTitle(category) + " rejected.", null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveAll(int userId)
        {
            var kyc = _kycRepo.GetStatus(userId);
            if (!kyc.AllSubmitted)
                return Done(userId, null, "All 4 documents and the Aadhaar / PAN numbers must be submitted before approving the KYC.");

            _kycRepo.SetStatus(userId, null, KycConstants.Approved, null, CurrentUserId);
            if (!kyc.AadhaarVerified) _kycRepo.SetNumberStatus(userId, true,  KycConstants.Approved, null, CurrentUserId);
            if (!kyc.PanVerified)     _kycRepo.SetNumberStatus(userId, false, KycConstants.Approved, null, CurrentUserId);
            return Done(userId, "KYC approved. The user now has full portal access.", null);
        }

        private ActionResult Done(int userId, string success, string error)
        {
            if (success != null) TempData["SuccessMessage"] = success;
            if (error != null)   TempData["ErrorMessage"]   = error;
            return RedirectToAction("Review", new { id = userId });
        }
    }
}
