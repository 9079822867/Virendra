using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class PackageController : Controller
    {
        private readonly IPackageRepository _repo;

        public PackageController(IPackageRepository repo) { _repo = repo; }

        public ActionResult Index()
        {
            return View(_repo.GetAllPackages());
        }

        public ActionResult Create()
        {
            return View(new Package { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(Package model)
        {
            if (string.IsNullOrWhiteSpace(model.PackageName))
                ModelState.AddModelError("PackageName", "Package name is required.");

            if (!ModelState.IsValid) return View(model);

            _repo.CreatePackage(model);
            TempData["SuccessMessage"] = "Package created successfully.";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            var pkg = _repo.GetPackageById(id);
            if (pkg == null) return HttpNotFound();
            return View(pkg);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(Package model)
        {
            if (string.IsNullOrWhiteSpace(model.PackageName))
                ModelState.AddModelError("PackageName", "Package name is required.");

            if (!ModelState.IsValid) return View(model);

            _repo.UpdatePackage(model);
            TempData["SuccessMessage"] = "Package updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            _repo.DeletePackage(id);
            TempData["SuccessMessage"] = "Package deactivated.";
            return RedirectToAction("Index");
        }
    }
}
