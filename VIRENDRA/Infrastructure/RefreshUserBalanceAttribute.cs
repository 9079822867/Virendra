using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using Dapper;

namespace VIRENDRA.Infrastructure
{
    /// <summary>
    /// Global filter: reloads Session["UserBal"] from the [User] table on every
    /// request so the balance shown in the layout is always live.
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
                    session["UserBal"] = c.ExecuteScalar<decimal>(
                        "SELECT ISNULL(UserBal, 0) FROM [User] WHERE Id = @Id", new { Id = userId });
                }
            }
            catch
            {
                // Keep the last known balance if the DB read fails.
            }
        }
    }
}
