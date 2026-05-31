using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class RechargeController : Controller
    {
        private readonly RechargeService     _rechargeSvc;
        private readonly IRechargeRepository _rechargeRepo;

        public RechargeController(RechargeService rechargeSvc, IRechargeRepository rechargeRepo)
        {
            _rechargeSvc  = rechargeSvc;
            _rechargeRepo = rechargeRepo;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var vm = BuildViewModel(null);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(RechargeRequestModel req)
        {
            if (!ModelState.IsValid)
            {
                var vmFail = BuildViewModel(null);
                vmFail.Request = req;
                return View(vmFail);
            }

            int userId     = (int)Session["UserId"];
            int addedById  = userId;

            var result = _rechargeSvc.ProcessRecharge(req, userId, addedById);

            var vm = BuildViewModel(result);
            vm.Request = req;
            return View(vm);
        }

        private RechargeFormViewModel BuildViewModel(RechargeResult result)
        {
            var operators = _rechargeRepo.GetActiveOperators();
            var circles   = _rechargeRepo.GetCircles();

            return new RechargeFormViewModel
            {
                OperatorList   = new System.Web.Mvc.SelectList(operators, "Id", "Name"),
                CircleList     = new System.Web.Mvc.SelectList(circles,   "Id", "CircleName"),
                RecentRecharges= _rechargeRepo.GetRecentRecharges(
                                    Session["UserId"] is int uid ? uid : 0),
                Result         = result,
                Request        = new RechargeRequestModel()
            };
        }
    }
}
