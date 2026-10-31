using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// API User / Retailer: raise a payment (fund) request after paying into a company account,
    /// and follow it in "My requests". Admins approve or reject it under Money → Payment Requests.
    /// </summary>
    [RoleAuthorize(RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class PaymentRequestController : Controller
    {
        private readonly PaymentRequestRepository _repo = new PaymentRequestRepository();

        private int CurrentUserId => Session["UserId"] is int uid ? uid : 0;

        [HttpGet]
        public ActionResult Index(DateTime? fromDate, DateTime? toDate, string status)
        {
            DateTime to   = toDate   ?? DateTime.Today;
            DateTime from = fromDate ?? to.AddDays(-30);
            if (to < from) { var t = from; from = to; to = t; }

            var all = _repo.GetMyRequests(CurrentUserId, from, to);

            var model = new PaymentRequestListViewModel
            {
                FromDate       = from,
                ToDate         = to,
                Status         = status,
                Items          = string.IsNullOrEmpty(status) ? all : all.Where(r => r.Status == status).ToList(),
                PendingCount   = all.Count(r => r.Status == "Pending"),
                PendingAmount  = all.Where(r => r.Status == "Pending").Sum(r => r.Amount),
                ApprovedCount  = all.Count(r => r.Status == "Approved"),
                ApprovedAmount = all.Where(r => r.Status == "Approved").Sum(r => r.Amount),
                RejectedCount  = all.Count(r => r.Status == "Rejected")
            };
            return View(model);
        }

        [HttpGet]
        public ActionResult Create()
        {
            LoadLists();
            return View(new PaymentRequestModel { PaymentDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PaymentRequestModel model)
        {
            var accounts = _repo.GetDepositAccounts();

            if (model.BankAccountId.HasValue && accounts.All(a => a.Id != model.BankAccountId.Value))
                ModelState.AddModelError("BankAccountId", "Choose one of the listed accounts.");

            if (model.TrTypeId.HasValue && _repo.GetTransferTypes().All(t => t.Id != model.TrTypeId.Value))
                ModelState.AddModelError("TrTypeId", "Choose a payment mode.");

            if (model.PaymentDate.HasValue)
            {
                if (model.PaymentDate.Value.Date > DateTime.Today)
                    ModelState.AddModelError("PaymentDate", "Payment date can't be in the future.");
                else if (model.PaymentDate.Value.Date < DateTime.Today.AddDays(-30))
                    ModelState.AddModelError("PaymentDate", "Payments older than 30 days need admin help.");
            }

            // The approve step rejects a reused reference, so stop it here
            if (!string.IsNullOrWhiteSpace(model.Chequeno) && _repo.ReferenceExists(model.Chequeno))
                ModelState.AddModelError("Chequeno", "This UTR / reference number was already used in a request.");

            if (!ModelState.IsValid)
            {
                LoadLists();
                return View(model);
            }

            try
            {
                long id = _repo.Create(CurrentUserId, model);
                TempData["SuccessMessage"] = "Request #" + id + " for ₹" + model.Amount.Value.ToString("N2") +
                                             " sent. Your wallet is credited once an admin approves it.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Could not send the request: " + ex.GetBaseException().Message);
                LoadLists();
                return View(model);
            }
        }

        private void LoadLists()
        {
            ViewBag.Accounts      = _repo.GetDepositAccounts();
            ViewBag.TransferTypes = _repo.GetTransferTypes();
        }
    }
}
