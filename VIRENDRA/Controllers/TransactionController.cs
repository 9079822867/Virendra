using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class TransactionController : Controller
    {
        private readonly ITransactionRepository _txnRepo;

        public TransactionController(ITransactionRepository txnRepo)
        {
            _txnRepo = txnRepo;
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
