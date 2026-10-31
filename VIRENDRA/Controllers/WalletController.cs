using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    // Add Money credits / debits a user's wallet directly (sp_AddUserWallet), so the whole
    // controller is SuperAdmin / Admin only.
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class WalletController : Controller
    {
        private readonly IWalletRepository _walletRepo;
        private readonly IUserRepository   _userRepo;

        public WalletController(IWalletRepository walletRepo, IUserRepository userRepo)
        {
            _walletRepo = walletRepo;
            _userRepo   = userRepo;
        }

        // ─── Shared helpers ────────────────────────────────────────────────────

        private void PopulateDropdowns(int? selectedUserId = null,
                                       int? selectedBankId = null,
                                       string selectedTransferType = "IMPS",
                                       string selectedWalletType   = "Main")
        {
            ViewBag.UserList = new SelectList(
                _userRepo.GetAllUsers(), "Id", "Username", selectedUserId);

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
            }, "Value", "Text", selectedTransferType);

            ViewBag.WalletTypeList = new SelectList(new[]
            {
                new { Value = "Main",        Text = "Main Wallet"       },
                new { Value = "BillPayment", Text = "Bill Payment Wallet"},
            }, "Value", "Text", selectedWalletType);
        }

        // ─── Payment Request Index ──────────────────────────────────────────────

        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult Index(int? statusId = null)
        {
            var list = _walletRepo.GetTransactions(statusId: statusId);

            ViewBag.StatusId     = statusId;
            ViewBag.TotalCount   = list.Count;
            ViewBag.PendingCount = list.Count(w => w.StatusId == 3 || w.StatusId == null);
            ViewBag.ApprovedCount= list.Count(w => w.StatusId == 6);
            ViewBag.RejectedCount= list.Count(w => w.StatusId == 7);
            ViewBag.TotalAmount  = list.Sum(w => w.Amount);

            return View(list);
        }

        // ─── Add Money (Create) ─────────────────────────────────────────────────

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
                PopulateDropdowns(model.UserId, model.BankAccountId,
                                  model.TransferType, model.WalletType);
                return View(model);
            }

            model.AddedBy = (int?)Session["UserId"];

            try
            {
                var result = _walletRepo.AddMoney(model);
                string message = AddMoneyMessage(result.Error);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = message;
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("", message);
                PopulateDropdowns(model.UserId, model.BankAccountId,
                                  model.TransferType, model.WalletType);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Unexpected error: " + ex.Message);
                PopulateDropdowns(model.UserId, model.BankAccountId,
                                  model.TransferType, model.WalletType);
                return View(model);
            }
        }

        // sp_AddUserWallet @error codes → message (0 / 6 / 7 are successes)
        private static string AddMoneyMessage(string code)
        {
            switch ((code ?? "0").Trim())
            {
                case "":
                case "0": return "Wallet has been updated.";
                case "7": return "Wallet with credit has been updated.";
                case "6": return "Outstanding balance debited.";
                case "2": return "Duplicate cheque / ref number.";
                case "3": return "Already approved.";
                case "4": return "Duplicate transaction for the same wallet request.";
                case "5": return "User's outstanding balance is too low.";
                case "Rollback": return "Nothing was saved: the database rolled the transaction back.";
                default:  return "Something went wrong (code " + code + ").";
            }
        }

        // ─── Edit ───────────────────────────────────────────────────────────────

        [HttpGet]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult Edit(int id)
        {
            var req = _walletRepo.GetWalletRequestById(id);
            if (req == null) return HttpNotFound();
            if (req.StatusId == StatusApproved)
            {
                TempData["ErrorMessage"] = "Approved requests are already in the ledger and can't be edited.";
                return RedirectToAction("Index");
            }

            PopulateDropdowns(req.UserId, req.BankAccountId,
                              req.TransferType ?? req.TrTypeName,
                              req.WalletType   ?? "Main");
            return View(req);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult Edit(WalletRequest model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model.UserId, model.BankAccountId,
                                  model.TransferType, model.WalletType);
                return View(model);
            }

            model.UpdatedById = (int?)Session["UserId"];

            if (_walletRepo.GetWalletRequestById(model.Id)?.StatusId == StatusApproved)
            {
                TempData["ErrorMessage"] = "Approved requests are already in the ledger and can't be edited.";
                return RedirectToAction("Index");
            }

            try
            {
                _walletRepo.UpdateWalletRequest(model);
                TempData["SuccessMessage"] = "Payment request updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                PopulateDropdowns(model.UserId, model.BankAccountId,
                                  model.TransferType, model.WalletType);
                return View(model);
            }
        }

        // ─── Delete ─────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult Delete(int id)
        {
            if (_walletRepo.GetWalletRequestById(id)?.StatusId == StatusApproved)
            {
                // Deleting would hide a credit that stays in TxnLedger
                TempData["ErrorMessage"] = "Approved requests are already in the ledger and can't be deleted.";
                return RedirectToAction("Index");
            }

            try
            {
                _walletRepo.DeleteWalletRequest(id);
                TempData["SuccessMessage"] = "Payment request deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Cannot delete: " + ex.Message;
            }
            return RedirectToAction("Index");
        }

        // ─── Approve / Reject (AJAX) ─────────────────────────────────────────────

        // StatusType: 3 = pending, 5 = Hold, 6 = Approved, 7 = Reject
        private const int StatusApproved = 6;
        private const int StatusRejected = 7;

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Approve(int id)
        {
            try
            {
                int userId = (int)(Session["UserId"] ?? 0);
                // Credits the wallet through sp_AddUserWallet; only then is the request Approved
                var (ok, error) = _walletRepo.ApproveRequest(id, userId);
                return Json(ok
                    ? new { success = true,  message = "Request approved and wallet credited." }
                    : new { success = false, message = error });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Reject(int id)
        {
            try
            {
                var req = _walletRepo.GetWalletRequestById(id);
                if (req == null)
                    return Json(new { success = false, message = "Request not found." });
                if (req.StatusId == StatusApproved)
                    return Json(new { success = false, message = "Approved requests can't be rejected." });

                int userId = (int)(Session["UserId"] ?? 0);
                _walletRepo.UpdateRequestStatus(id, StatusRejected, userId);
                return Json(new { success = true, message = "Request rejected." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
