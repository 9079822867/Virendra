using System;
using System.Net;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin)]
    public class UserController : Controller
    {
        private readonly IUserRepository    _userRepository;
        private readonly IPackageRepository _packageRepository;

        public UserController(IUserRepository userRepository, IPackageRepository packageRepository)
        {
            _userRepository    = userRepository;
            _packageRepository = packageRepository;
        }

        public ActionResult Index()
        {
            return View(_userRepository.GetAllUsers());
        }

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = _userRepository.GetUserById(id.Value);

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        public ActionResult Create()
        {
            PopulateRoleList();
            PopulatePackageList();
            return View(BuildNewUser());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Username))
            {
                ModelState.AddModelError("Username", "Username is required.");
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("Password", "Password is required.");
            }

            if (!string.IsNullOrWhiteSpace(user.Username) &&
                _userRepository.GetUserByUsername(user.Username.Trim()) != null)
            {
                ModelState.AddModelError("Username", "Username already exists.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoleList(user.RoleId);
                PopulatePackageList(user.PackageId);
                return View(user);
            }

            user.Username = user.Username.Trim();
            user.Password = user.Password.Trim();
            user.AddedDate = DateTime.Now;
            user.RetryCount = 0;
            user.IsDeleted = false;

            _userRepository.CreateUser(user);
            TempData["SuccessMessage"] = "User created successfully.";

            return RedirectToAction("Index");
        }

        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = _userRepository.GetUserById(id.Value);

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            PopulateRoleList(user.RoleId);
            PopulatePackageList(user.PackageId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, User model)
        {
            User user = _userRepository.GetUserById(id);

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            if (string.IsNullOrWhiteSpace(model.Username))
            {
                ModelState.AddModelError("Username", "Username is required.");
            }

            User sameUsernameUser = _userRepository.GetUserByUsername((model.Username ?? string.Empty).Trim());
            if (sameUsernameUser != null && sameUsernameUser.Id != id)
            {
                ModelState.AddModelError("Username", "Username already exists.");
            }

            if (!ModelState.IsValid)
            {
                model.Id = id;
                PopulateRoleList(model.RoleId);
                PopulatePackageList(model.PackageId);
                return View(model);
            }

            user.Username = model.Username.Trim();

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.Password = model.Password.Trim();
            }

            user.RoleId = model.RoleId;
            user.IsActive = model.IsActive;
            user.IsLocked = model.IsLocked;
            user.PackageId = model.PackageId;
            user.UserBal = model.UserBal;
            user.UserOutStandingBal = model.UserOutStandingBal;
            user.ParentID = model.ParentID;
            user.LoginIP = model.LoginIP;
            user.CallbackURL = model.CallbackURL;
            user.ComplainCallbackURL = model.ComplainCallbackURL;
            user.UserPin = model.UserPin;
            user.IsComm = model.IsComm;
            user.IsOtpCheck = model.IsOtpCheck;
            user.IsJioActiveHigh = model.IsJioActiveHigh;
            user.UpdatedDate = DateTime.Now;

            _userRepository.UpdateUser(user);
            TempData["SuccessMessage"] = "User updated successfully.";

            return RedirectToAction("Index");
        }

        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = _userRepository.GetUserById(id.Value);

            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            _userRepository.DeleteUser(id);
            TempData["SuccessMessage"] = "User deleted successfully.";

            return RedirectToAction("Index");
        }

        private static User BuildNewUser()
        {
            return new User
            {
                IsActive = true,
                IsComm = true,
                IsOtpCheck = false,
                UserBal = 0,
                UserOutStandingBal = 0
            };
        }

        private void PopulateRoleList(byte? selectedRoleId = null)
        {
            ViewBag.RoleList = new SelectList(
                _userRepository.GetAllRoles(), "Id", "RoleName", selectedRoleId);
        }

        private void PopulatePackageList(int? selectedPackageId = null)
        {
            ViewBag.PackageList = new SelectList(
                _packageRepository.GetAllPackages().FindAll(p => p.IsActive), "Id", "PackageName", selectedPackageId);
        }
    }
}
