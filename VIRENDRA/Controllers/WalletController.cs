using System;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.Retailer)]
    public class WalletController : Controller
    {
        private readonly IWalletRepository _walletRepo;
        private readonly IUserRepository   _userRepo;

        public WalletController(IWalletRepository walletRepo, IUserRepository userRepo)
        {
            _walletRepo = walletRepo;
            _userRepo   = userRepo;
        }

        private void PopulateDropdowns(int? selectedUserId = null, int? selectedBankId = null)
        {
            ViewBag.UserList = new SelectList(
                _userRepo.GetAllUsers(), "Id", "Username", selectedUserId);

            // BankAccount now uses HolderName as the display field
            ViewBag.BankAccountList = new SelectList(
                _walletRepo.GetAllBankAccounts(), "Id", "HolderName", selectedBankId);

            ViewBag.TransferTypeList = new SelectList(new[]
            {
                new { Value = "IMPS",   Text = "IMPS"   },
                new { Value = "NEFT",   Text = "NEFT"   },
                new { Value = "RTGS",   Text = "RTGS"   },
                new { Value = "UPI",    Text = "UPI"    },
                new { Value = "Cash",   Text = "Cash"   },
                new { Value = "Cheque", Text = "Cheque" },
            }, "Value", "Text");
        }

        [HttpGet]
        public ActionResult AddMoney()
        {
            PopulateDropdowns();

            var refNo = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var model = new WalletRequest
            {
                WalletType   = "Main",
                TransferType = "IMPS",
                ChequeRefNo  = refNo,
                Remark       = refNo,
                PaymentDate  = DateTime.Today,
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMoney(WalletRequest model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model.UserId, model.BankAccountId);
                return View(model);
            }

            model.AddedBy = (int?)Session["UserId"];

            try
            {
                _walletRepo.AddMoney(model);
                TempData["SuccessMessage"] = "Wallet transaction added successfully.";
                return RedirectToAction("AddMoney");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving transaction: " + ex.Message);
                PopulateDropdowns(model.UserId, model.BankAccountId);
                return View(model);
            }
        }
    }
}
