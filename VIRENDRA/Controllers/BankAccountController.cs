using System;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class BankAccountController : Controller
    {
        private readonly IWalletRepository _walletRepo;

        public BankAccountController(IWalletRepository walletRepo)
        {
            _walletRepo = walletRepo;
        }

        // GET: /BankAccount
        public ActionResult Index()
        {
            return View(_walletRepo.GetAllBankAccounts());
        }

        // GET: /BankAccount/Create
        [HttpGet]
        public ActionResult Create()
        {
            return View(new BankAccount());
        }

        // POST: /BankAccount/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BankAccount model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.AddedById = (int?)Session["UserId"];
            _walletRepo.CreateBankAccount(model);

            TempData["SuccessMessage"] = $"Bank account '{model.BankName}' created successfully.";
            return RedirectToAction("Index");
        }

        // GET: /BankAccount/Edit/5
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var bank = _walletRepo.GetBankAccountById(id);
            if (bank == null) return HttpNotFound();
            return View(bank);
        }

        // POST: /BankAccount/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(BankAccount model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.UpdatedById = (int?)Session["UserId"];
            _walletRepo.UpdateBankAccount(model);

            TempData["SuccessMessage"] = $"Bank account '{model.BankName}' updated successfully.";
            return RedirectToAction("Index");
        }

        // POST: /BankAccount/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                _walletRepo.DeleteBankAccount(id);
                TempData["SuccessMessage"] = "Bank account deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Cannot delete — account may be in use. " + ex.Message;
            }
            return RedirectToAction("Index");
        }

        // POST: /BankAccount/ToggleBlock  (AJAX)
        [HttpPost]
        public JsonResult ToggleBlock(int id, bool blockUser)
        {
            try
            {
                _walletRepo.ToggleBlockUser(id, blockUser);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
