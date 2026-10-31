using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class HomeController : Controller
    {
        private readonly ITransactionRepository _txnRepo;

        public HomeController(ITransactionRepository txnRepo)
        {
            _txnRepo = txnRepo;
        }

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }

        public ActionResult Dashboard()
        {
            int userId = (int)(Session["UserId"] ?? 0);
            byte roleId = (byte)(Session["RoleId"] ?? (byte)0);
            bool isAdmin = roleId == RoleConstants.SuperAdmin || roleId == RoleConstants.Admin;

            ViewBag.Stats           = _txnRepo.GetDashboardStats(userId);
            // API User / Retailer see only their own recharges
            ViewBag.RecentRecharges = _txnRepo.GetRecentRecharges(isAdmin ? (int?)null : userId, 10);
            return View();
        }
    }
}