using System;
using System.Linq;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>The logged-in user's own settings: profile and password (all roles).</summary>
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class SettingController : Controller
    {
        private readonly ProfileRepository _profileRepo = new ProfileRepository();
        private readonly UserRepository    _userRepo    = new UserRepository();

        private int CurrentUserId => (int)Session["UserId"];

        // ── User Profile ────────────────────────────────────────────────────

        [HttpGet]
        public ActionResult UserProfile()
        {
            var model = _profileRepo.GetProfile(CurrentUserId);
            if (model == null) return RedirectToAction("Logout", "Auth");

            LoadStates(model.StateID);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserProfile(UserProfileViewModel model)
        {
            // Account fields are read-only on this page: always the logged-in user
            var current = _profileRepo.GetProfile(CurrentUserId);
            if (current == null) return RedirectToAction("Logout", "Auth");

            model.Id          = current.Id;
            model.Username    = current.Username;
            model.RoleName    = current.RoleName;
            model.PackageName = current.PackageName;
            model.Balance     = current.Balance;

            if (!ModelState.IsValid)
            {
                LoadStates(model.StateID);
                return View(model);
            }

            try
            {
                _profileRepo.SaveProfile(CurrentUserId, model);
                TempData["SuccessMessage"] = "Profile has been updated.";
                return RedirectToAction("UserProfile");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Profile could not be saved. Please try again.");
                LoadStates(model.StateID);
                return View(model);
            }
        }

        // ── Change Password ─────────────────────────────────────────────────

        [HttpGet]
        public ActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string stored = _profileRepo.GetPassword(CurrentUserId);
            if (stored == null)
                return RedirectToAction("Logout", "Auth");

            if (stored != model.CurrentPassword)
            {
                ModelState.AddModelError("CurrentPassword", "Current password doesn't match.");
                return View(new ChangePasswordViewModel());
            }
            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from the current one.");
                return View(new ChangePasswordViewModel());
            }

            _profileRepo.UpdatePassword(CurrentUserId, model.NewPassword);
            TempData["SuccessMessage"] = "Password has been changed successfully.";
            return RedirectToAction("ChangePassword");
        }

        // States list = Circle table (same as STC)
        private void LoadStates(int? selected)
        {
            ViewBag.States = _userRepo.GetAllCircles()
                .Select(c => new SelectListItem
                {
                    Value    = c.Id.ToString(),
                    Text     = c.CircleName,
                    Selected = c.Id == selected
                }).ToList();
        }
    }
}
