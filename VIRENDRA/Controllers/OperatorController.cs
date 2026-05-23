using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class OperatorController : Controller
    {
        private readonly IPackageRepository _repo;

        public OperatorController(IPackageRepository repo) { _repo = repo; }

        public ActionResult Index()
        {
            return View(_repo.GetAllOperators());
        }

        public ActionResult Create()
        {
            return View(new Operator { IsActive = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(Operator model)
        {
            if (string.IsNullOrWhiteSpace(model.OperatorName))
                ModelState.AddModelError("OperatorName", "Operator name is required.");

            if (!ModelState.IsValid) return View(model);

            _repo.CreateOperator(model);
            TempData["SuccessMessage"] = "Operator created successfully.";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            var op = _repo.GetOperatorById(id);
            if (op == null) return HttpNotFound();
            return View(op);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(Operator model)
        {
            if (string.IsNullOrWhiteSpace(model.OperatorName))
                ModelState.AddModelError("OperatorName", "Operator name is required.");

            if (!ModelState.IsValid) return View(model);

            _repo.UpdateOperator(model);
            TempData["SuccessMessage"] = "Operator updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            _repo.DeleteOperator(id);
            TempData["SuccessMessage"] = "Operator deactivated.";
            return RedirectToAction("Index");
        }
    }
}
