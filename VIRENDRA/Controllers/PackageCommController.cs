using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class PackageCommController : Controller
    {
        private readonly IPackageRepository _repo;

        public PackageCommController(IPackageRepository repo) { _repo = repo; }

        // Show all operators for a package with their commission
        public ActionResult Index(int packageId)
        {
            var pkg = _repo.GetPackageById(packageId);
            if (pkg == null) return HttpNotFound();

            ViewBag.Package = pkg;
            return View(_repo.GetCommissionsByPackage(packageId));
        }

        // Bulk save commissions from the grid form
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveAll(int packageId, int[] operatorId, string[] commType, decimal[] commValue, bool[] isActive)
        {
            for (int i = 0; i < operatorId.Length; i++)
            {
                _repo.SaveCommission(new PackageComm
                {
                    PackageId  = packageId,
                    OperatorId = operatorId[i],
                    CommType   = commType[i],
                    CommValue  = commValue[i],
                    IsActive   = isActive != null && i < isActive.Length && isActive[i]
                });
            }

            TempData["SuccessMessage"] = "Commissions saved successfully.";
            return RedirectToAction("Index", new { packageId });
        }

        // API User / Retailer — view own package commission
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
        public ActionResult MyCommission()
        {
            if (Session["UserId"] is int userId)
            {
                var list = _repo.GetCommissionsByUser(userId);
                return View(list);
            }
            return RedirectToAction("Login", "Auth");
        }
    }
}
