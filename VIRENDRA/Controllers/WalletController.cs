using System;
using System.Linq;
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
            ViewBag.PendingCount = list.Count(w => w.StatusId == 1 || w.StatusId == null);
            ViewBag.ApprovedCount= list.Count(w => w.StatusId == 2);
            ViewBag.RejectedCount= list.Count(w => w.StatusId == 3);
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

                if (result.Success)
                {
                    TempData["SuccessMessage"] = !string.IsNullOrWhiteSpace(result.Log) && result.Log != "0"
                        ? result.Log
                        : "Payment request submitted successfully.";
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("", result.Error);
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

        // ─── Edit ───────────────────────────────────────────────────────────────

        [HttpGet]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public ActionResult Edit(int id)
        {
            var req = _walletRepo.GetWalletRequestById(id);
            if (req == null) return HttpNotFound();

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

        [HttpPost]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public JsonResult Approve(int id)
        {
            try
            {
                int userId = (int)(Session["UserId"] ?? 0);
                _walletRepo.UpdateRequestStatus(id, 2, userId);   // 2 = Approved
                return Json(new { success = true, message = "Request approved." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
        public JsonResult Reject(int id)
        {
            try
            {
                int userId = (int)(Session["UserId"] ?? 0);
                _walletRepo.UpdateRequestStatus(id, 3, userId);   // 3 = Rejected
                return Json(new { success = true, message = "Request rejected." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
