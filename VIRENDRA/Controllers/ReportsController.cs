using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
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

        // Successful recharges of the day grouped by operator (was hard-coded sample rows).
        // A negative Recharge_Commision is a surcharge.
        private static List<OperatorwiseReportItem> GetOperatorwiseReport(DateTime reportDate)
        {
            const string sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY SUM(r.Amount) DESC) AS SrNo,
                    ISNULL(o.Name, '-') AS Operator,
                    COUNT(*)            AS Count,
                    SUM(r.Amount)       AS Amount,
                    SUM(CASE WHEN ISNULL(r.Recharge_Commision, 0) > 0 THEN r.Recharge_Commision ELSE 0 END)  AS Commission,
                    SUM(CASE WHEN ISNULL(r.Recharge_Commision, 0) < 0 THEN -r.Recharge_Commision ELSE 0 END) AS Surcharge
                FROM Recharge r
                LEFT JOIN [Operator] o ON o.Id = r.OpId
                WHERE r.StatusId = 1
                  AND r.RequestTime >= @Day AND r.RequestTime < DATEADD(DAY, 1, @Day)
                GROUP BY o.Name
                ORDER BY SUM(r.Amount) DESC";

            string cs = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");

            using (var c = new SqlConnection(cs))
                return c.Query<OperatorwiseReportItem>(sql, new { Day = reportDate.Date }).ToList();
        }
    }
}
