using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class TransactionController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            return RedirectToAction("RechargeHistory");
        }

        [HttpGet]
        public ActionResult RechargeHistory(DateTime? fromDate, DateTime? toDate, string status, string searchText)
        {
            DateTime today = DateTime.Today;
            DateTime selectedFromDate = fromDate ?? today;
            DateTime selectedToDate = toDate ?? today;
            List<RechargeHistoryItem> rows = GetRechargeHistory();

            rows = rows
                .Where(item => item.Date.Date >= selectedFromDate.Date && item.Date.Date <= selectedToDate.Date)
                .Where(item => string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .Where(item => MatchesRechargeSearch(item, searchText))
                .ToList();

            var model = new RechargeHistoryViewModel
            {
                FromDate = selectedFromDate,
                ToDate = selectedToDate,
                Status = status,
                SearchText = searchText,
                Items = rows,
                TotalCount = rows.Count,
                TotalAmount = rows.Sum(item => item.Amount),
                TotalCommission = rows.Sum(item => item.Commission)
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult TransactionHistory(DateTime? fromDate, DateTime? toDate, string type, string searchText)
        {
            DateTime today = DateTime.Today;
            DateTime selectedFromDate = fromDate ?? today;
            DateTime selectedToDate = toDate ?? today;
            List<TransactionHistoryItem> rows = GetTransactionHistory();

            rows = rows
                .Where(item => item.Date.Date >= selectedFromDate.Date && item.Date.Date <= selectedToDate.Date)
                .Where(item => string.IsNullOrWhiteSpace(type) || item.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                .Where(item => MatchesTransactionSearch(item, searchText))
                .ToList();

            var model = new TransactionHistoryViewModel
            {
                FromDate = selectedFromDate,
                ToDate = selectedToDate,
                Type = type,
                SearchText = searchText,
                Items = rows,
                TotalCount = rows.Count,
                TotalCredit = rows.Sum(item => item.Credit),
                TotalDebit = rows.Sum(item => item.Debit)
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult Details()
        {
            return RedirectToAction("TransactionHistory");
        }

        private static bool MatchesRechargeSearch(RechargeHistoryItem item, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string value = searchText.Trim();

            return item.TxnId.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Number.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Operator.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesTransactionSearch(TransactionHistoryItem item, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            string value = searchText.Trim();

            return item.TxnId.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Description.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Type.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<RechargeHistoryItem> GetRechargeHistory()
        {
            DateTime today = DateTime.Today;

            return new List<RechargeHistoryItem>
            {
                new RechargeHistoryItem { SrNo = 1, TxnId = "RCH10021", Number = "9876543210", Operator = "Reliance Jio", Amount = 239.00m, Commission = 2.39m, Status = "Success", Date = today.AddHours(9) },
                new RechargeHistoryItem { SrNo = 2, TxnId = "RCH10022", Number = "9823456789", Operator = "VI - Vodafone & Idea", Amount = 299.00m, Commission = 11.96m, Status = "Success", Date = today.AddHours(10) },
                new RechargeHistoryItem { SrNo = 3, TxnId = "RCH10023", Number = "9123456780", Operator = "Airtel DTH", Amount = 300.00m, Commission = 12.00m, Status = "Pending", Date = today.AddHours(11) },
                new RechargeHistoryItem { SrNo = 4, TxnId = "RCH10024", Number = "9000012345", Operator = "BSNL SPECIAL (STV)", Amount = 147.00m, Commission = 8.09m, Status = "Failed", Date = today.AddHours(12) }
            };
        }

        private static List<TransactionHistoryItem> GetTransactionHistory()
        {
            DateTime today = DateTime.Today;

            return new List<TransactionHistoryItem>
            {
                new TransactionHistoryItem { SrNo = 1, TxnId = "TXN50031", Type = "Credit", Description = "Wallet topup", Credit = 1000.00m, Debit = 0.00m, Balance = 1958.97m, Date = today.AddHours(8) },
                new TransactionHistoryItem { SrNo = 2, TxnId = "TXN50032", Type = "Debit", Description = "Mobile prepaid recharge", Credit = 0.00m, Debit = 239.00m, Balance = 1719.97m, Date = today.AddHours(9) },
                new TransactionHistoryItem { SrNo = 3, TxnId = "TXN50033", Type = "Commission", Description = "Recharge commission", Credit = 2.39m, Debit = 0.00m, Balance = 1722.36m, Date = today.AddHours(9).AddMinutes(2) },
                new TransactionHistoryItem { SrNo = 4, TxnId = "TXN50034", Type = "Refund", Description = "Failed recharge refund", Credit = 147.00m, Debit = 0.00m, Balance = 1869.36m, Date = today.AddHours(12) }
            };
        }
    }
}
