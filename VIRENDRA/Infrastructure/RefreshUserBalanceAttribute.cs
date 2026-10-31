using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using Dapper;

namespace VIRENDRA.Infrastructure
{
    /// <summary>
    /// Global filter: reloads Session["UserBal"] and Session["UserOutstanding"] from the
    /// [User] table on every request so the figures shown in the layout are always live.
    /// </summary>
    public class RefreshUserBalanceAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.IsChildAction) return;

            var session = filterContext.HttpContext.Session;
            if (session == null || session["UserId"] == null) return;

            try
            {
                int userId = Convert.ToInt32(session["UserId"]);
                string conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString;
                if (string.IsNullOrEmpty(conn)) return;

                using (var c = new SqlConnection(conn))
                {
                    var row = c.QueryFirstOrDefault(
                        "SELECT ISNULL(UserBal, 0) AS Bal, ISNULL(UserOutStandingBal, 0) AS Outstanding FROM [User] WHERE Id = @Id",
                        new { Id = userId });
                    if (row != null)
                    {
                        session["UserBal"]         = (decimal)row.Bal;
                        session["UserOutstanding"] = (decimal)row.Outstanding;
                    }
                }
            }
            catch
            {
                // Keep the last known balance if the DB read fails.
            }
        }
    }
}
