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
            ViewBag.RecentRecharges = _txnRepo.GetRecentRecharges(10);
            return View();
        }
    }
}