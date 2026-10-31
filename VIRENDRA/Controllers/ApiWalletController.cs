using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Vendor (API) wallet ported from STC ApiWalletController: ledger report with filters and
    /// manual top-up / take-back entries. SuperAdmin / Admin only.
    /// </summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class ApiWalletController : Controller
    {
        private readonly ApiWalletRepository _repo = new ApiWalletRepository();

        private int CurrentUserId => Session["UserId"] as int? ?? 0;

        [HttpGet]
        public ActionResult Index(DateTime? fromDate, DateTime? toDate, long? recId, long? txnId,
            int? txnTypeId, int? amtTypeId, int? apiId, int? userId, string remark)
        {
            DateTime from = fromDate ?? DateTime.Today;
            DateTime to   = toDate   ?? DateTime.Today;
            if (to < from) { var t = from; from = to; to = t; }

            var rows = _repo.GetApiWalletTxns(from, to,
                recId > 0 ? recId : null, txnId > 0 ? txnId : null,
                txnTypeId > 0 ? txnTypeId : null, amtTypeId > 0 ? amtTypeId : null,
                apiId > 0 ? apiId : null, userId > 0 ? userId : null, remark);

            ViewBag.ApiList       = ToSelect(_repo.GetApis(), apiId);
            ViewBag.UserList      = ToSelect(_repo.GetUsers(), userId);
            ViewBag.TxnTypeList   = ToSelect(_repo.GetTxnTypes(), txnTypeId);
            ViewBag.AmtTypeList   = ToSelect(_repo.GetAmtTypes(), amtTypeId);
            ViewBag.TrTypeList    = _repo.GetTransferTypes();
            ViewBag.BankAccounts  = _repo.GetBankAccounts();

            var model = new ApiWalletViewModel
            {
                FromDate       = from,
                ToDate         = to,
                RecId          = recId,
                TxnId          = txnId,
                TxnTypeId      = txnTypeId,
                AmtTypeId      = amtTypeId,
                ApiId          = apiId,
                UserId         = userId,
                Remark         = remark,
                Items          = rows,
                TotalCount     = rows.Count,
                TotalCredit    = rows.Sum(r => r.CR_Amt),
                TotalIncentive = rows.Sum(r => r.Ins_Amt),
                TotalDebit     = rows.Sum(r => r.DB_Amt)
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddEdit(ApiWalletAddModel model)
        {
            string error =
                  model.ApiId <= 0         ? "Invalid Vendor"
                : model.TrTypeId <= 0      ? "Invalid Transfer Type"
                : model.BankAccountId <= 0 ? "Invalid Bank Account"
                : model.SentAmount < 0 || model.ReceivedAmount < 0 || model.IncentiveAmount < 0
                  || model.SentAmount + model.ReceivedAmount + model.IncentiveAmount <= 0 ? "Invalid Amount"
                : string.IsNullOrWhiteSpace(model.ChequeNo) ? "Invalid ChequeNo/RefNo"
                : null;

            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Index");
            }

            try
            {
                string trType = _repo.GetTransferTypes().FirstOrDefault(t => t.Id == model.TrTypeId)?.Name ?? "NA";
                var result = _repo.AddApiWallet(model, trType, CurrentUserId);

                if (result == ApiWalletAddResult.Success)
                    TempData["SuccessMessage"] = "Wallet has been updated.";
                else if (result == ApiWalletAddResult.Duplicate)
                    TempData["ErrorMessage"] = "Duplicate Cheque/Ref number!";
                else
                    TempData["ErrorMessage"] = "Invalid Vendor";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Vendor wallet update failed: " + ex.GetBaseException().Message;
            }

            return RedirectToAction("Index", new { apiId = model.ApiId });
        }

        [HttpGet]
        public JsonResult GetApiWalletBalance(int apiId)
        {
            return Json(apiId > 0 ? _repo.GetApiWalletBalance(apiId) : 0m, JsonRequestBehavior.AllowGet);
        }

        private static System.Collections.Generic.List<SelectListItem> ToSelect(
            System.Collections.Generic.List<LookupItem> items, int? selected)
            => items.Select(x => new SelectListItem
            {
                Value    = x.Id.ToString(),
                Text     = x.Name ?? "NA",
                Selected = x.Id == selected
            }).ToList();
    }
}
