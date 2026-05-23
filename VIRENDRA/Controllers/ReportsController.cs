using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class ReportsController : Controller
    {
        [HttpGet]
        public ActionResult OperatorwiseReport(DateTime? reportDate)
        {
            DateTime selectedDate = reportDate ?? DateTime.Today;
            List<OperatorwiseReportItem> rows = GetOperatorwiseReport(selectedDate);

            var model = new OperatorwiseReportViewModel
            {
                ReportDate = selectedDate,
                Items = rows,
                TotalCount = rows.Sum(item => item.Count),
                TotalAmount = rows.Sum(item => item.Amount),
                TotalCommission = rows.Sum(item => item.Commission),
                TotalSurcharge = rows.Sum(item => item.Surcharge)
            };

            return View(model);
        }

        private static List<OperatorwiseReportItem> GetOperatorwiseReport(DateTime reportDate)
        {
            return new List<OperatorwiseReportItem>
            {
                new OperatorwiseReportItem { SrNo = 1, Operator = "VI - Vodafone & Idea", Count = 10, Amount = 2590.00m, Commission = 103.60m, Surcharge = 0.00m },
                new OperatorwiseReportItem { SrNo = 2, Operator = "BSNL SPECIAL (STV)", Count = 1, Amount = 147.00m, Commission = 8.09m, Surcharge = 0.00m },
                new OperatorwiseReportItem { SrNo = 3, Operator = "Reliance Jio", Count = 11, Amount = 2391.00m, Commission = 23.91m, Surcharge = 0.00m },
                new OperatorwiseReportItem { SrNo = 4, Operator = "Airtel DTH", Count = 1, Amount = 300.00m, Commission = 12.00m, Surcharge = 0.00m }
            };
        }
    }
}
