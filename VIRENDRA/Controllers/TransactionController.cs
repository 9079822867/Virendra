using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class TransactionController : Controller
    {
        private readonly ITransactionRepository _txnRepo;
        private readonly IRechargeRepository    _rechargeRepo;
        private readonly IComplaintRepository   _complaintRepo;
        private readonly ComplaintService       _complaintSvc;

        public TransactionController(ITransactionRepository txnRepo, IRechargeRepository rechargeRepo,
            IComplaintRepository complaintRepo, ComplaintService complaintSvc)
        {
            _txnRepo       = txnRepo;
            _rechargeRepo  = rechargeRepo;
            _complaintRepo = complaintRepo;
            _complaintSvc  = complaintSvc;
        }

        [HttpGet]
        public ActionResult Index()
        {
            return RedirectToAction("RechargeHistory");
        }

        [HttpGet]
        public ActionResult RechargeHistory(DateTime? fromDate, DateTime? toDate, string status, string searchText)
        {
            DateTime from = fromDate ?? DateTime.Today;
            DateTime to   = toDate   ?? DateTime.Today;

            // Non-admin roles see only their own transactions
            int? userId = GetScopedUserId();

            var rows = _txnRepo.GetRechargeHistory(userId, from, to, status, searchText);

            ViewBag.CanChangeStatus = IsAdmin();

            var model = new RechargeHistoryViewModel
            {
                FromDate        = from,
                ToDate          = to,
                Status          = status,
                SearchText      = searchText,
                Items           = rows,
                TotalCount      = rows.Count,
                TotalAmount     = rows.Sum(r => r.Amount),
                TotalCommission = rows.Sum(r => r.Commission)
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult TransactionHistory(DateTime? fromDate, DateTime? toDate, string type, string searchText)
        {
            DateTime from = fromDate ?? DateTime.Today;
            DateTime to   = toDate   ?? DateTime.Today;

            int? userId = GetScopedUserId();

            var rows = _txnRepo.GetTransactionHistory(userId, from, to, type, searchText);

            var model = new TransactionHistoryViewModel
            {
                FromDate    = from,
                ToDate      = to,
                Type        = type,
                SearchText  = searchText,
                Items       = rows,
                TotalCount  = rows.Count,
                TotalCredit = rows.Sum(r => r.Credit),
                TotalDebit  = rows.Sum(r => r.Debit)
            };

            return View(model);
        }

        // Manual status change from Recharge History — SuperAdmin / Admin only.
        // usp_UpdateRechargeStatus applies the ledger side (refund on Failed, commission on Success).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult ChangeStatus(long recId, int statusId, string apiTxnId, string statusMsg, string returnUrl)
        {
            ActionResult back = Url.IsLocalUrl(returnUrl)
                ? (ActionResult)Redirect(returnUrl)
                : RedirectToAction("RechargeHistory");

            if (statusId != RechargeStatusCodes.SUCCESS &&
                statusId != RechargeStatusCodes.PROCESS &&
                statusId != RechargeStatusCodes.FAILED)
            {
                TempData["ErrorMessage"] = "Invalid status selected.";
                return back;
            }

            int? current = _rechargeRepo.GetRechargeStatusId(recId);
            if (current == null)
            {
                TempData["ErrorMessage"] = "Recharge not found.";
                return back;
            }
            if (current == statusId)
            {
                TempData["ErrorMessage"] = "Recharge already has this status.";
                return back;
            }
            if (current == RechargeStatusCodes.FAILED)
            {
                // Failed recharges are already refunded; changing them would need a fresh debit
                TempData["ErrorMessage"] = "Failed recharge is final and cannot be changed.";
                return back;
            }

            try
            {
                string by = (Session["Username"] ?? "admin").ToString();
                string msg = string.IsNullOrWhiteSpace(statusMsg)
                    ? "Manual status change by " + by
                    : statusMsg.Trim() + " (by " + by + ")";

                _rechargeRepo.UpdateRechargeStatus(recId, 0, statusId,
                    apiTxnId?.Trim(), string.Empty, msg, "Manual-" + by,
                    out bool isDownline, out bool isRefund, out string spLog);

                TempData["SuccessMessage"] = "Recharge #" + recId + " updated to " + StatusName(statusId) +
                    (statusId == RechargeStatusCodes.FAILED ? (isRefund ? ". Amount refunded." : ". Refund not reported by SP.") : ".");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Status update failed: " + ex.GetBaseException().Message;
            }

            return back;
        }

        private static string StatusName(int statusId)
            => statusId == RechargeStatusCodes.SUCCESS ? "Success"
             : statusId == RechargeStatusCodes.FAILED  ? "Failed"
             : "Pending";

        private bool IsAdmin()
        {
            byte roleId = (byte)(Session["RoleId"] ?? (byte)0);
            return roleId == RoleConstants.SuperAdmin || roleId == RoleConstants.Admin;
        }

        // ── Complaints (disputes) ────────────────────────────────────────────

        // Book a complaint from Recharge History. Non-admins only on their own recharges.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateComplaint(long recId, string remark, string returnUrl)
        {
            ActionResult back = Url.IsLocalUrl(returnUrl)
                ? (ActionResult)Redirect(returnUrl)
                : RedirectToAction("RechargeHistory");

            try
            {
                int userId = (int)Session["UserId"];
                var result = _complaintSvc.Book(recId.ToString(), remark, userId, IsAdmin() ? (int?)null : userId);

                if (result.Status == ComplaintBookStatus.Booked)
                    TempData["SuccessMessage"] = "Complaint #" + result.ComplaintId + " booked for recharge #" + recId + ".";
                else
                    TempData["ErrorMessage"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Complaint failed: " + ex.GetBaseException().Message;
            }

            return back;
        }

        // Dispute / complaint report. Admins see every user's complaints; others only their own.
        [HttpGet]
        public ActionResult ComplaintReport(DateTime? fromDate, DateTime? toDate, int? statusId, string searchText)
        {
            DateTime to   = toDate   ?? DateTime.Today;
            DateTime from = fromDate ?? to.AddDays(-30);

            var model = new ComplaintReportViewModel
            {
                FromDate   = from,
                ToDate     = to,
                StatusId   = statusId,
                SearchText = searchText,
                IsAdmin    = IsAdmin(),
                Items      = _complaintRepo.GetComplaintReport(GetScopedUserId(), from, to, statusId, searchText)
            };

            return View(model);
        }

        // Admin: resolve (or keep processing) with an optional refund of the recharge.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult ResolveComplaint(long complaintId, bool isResolved, bool isRefund, string comment, string returnUrl)
        {
            ActionResult back = Url.IsLocalUrl(returnUrl)
                ? (ActionResult)Redirect(returnUrl)
                : RedirectToAction("ComplaintReport");

            try
            {
                int    adminId   = (int)Session["UserId"];
                string adminName = (Session["Username"] ?? "admin").ToString();

                var (ok, message) = _complaintSvc.Resolve(complaintId, isResolved, isRefund, comment, adminId, adminName);
                TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Complaint update failed: " + ex.GetBaseException().Message;
            }

            return back;
        }

        [HttpGet]
        public ActionResult Details()
        {
            return RedirectToAction("TransactionHistory");
        }

        // SuperAdmin and Admin see all users; others see only their own records
        private int? GetScopedUserId()
        {
            byte roleId = (byte)(Session["RoleId"] ?? (byte)0);
            if (roleId == RoleConstants.SuperAdmin || roleId == RoleConstants.Admin)
                return null;

            return (int?)Session["UserId"];
        }
    }
}
