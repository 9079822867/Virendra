using System;
using System.Data.SqlClient;
using System.Linq;
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
        // Roles whose users can own child users (be picked as ParentID).
        private static readonly byte[] ParentRoleIds =
            { RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser };

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
            PopulateLists(null, null, null);
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

            if (user.RoleId == null)
            {
                ModelState.AddModelError("RoleId", "Role is required.");
            }

            ValidateParent(user.ParentID, null);

            if (!string.IsNullOrWhiteSpace(user.Username) &&
                _userRepository.GetUserByUsername(user.Username.Trim()) != null)
            {
                ModelState.AddModelError("Username", "Username already exists.");
            }

            if (!ModelState.IsValid)
            {
                PopulateLists(user.RoleId, user.PackageId, user.ParentID);
                return View(user);
            }

            user.Username = user.Username.Trim();
            user.Password = user.Password.Trim();
            user.AddedDate = DateTime.Now;
            user.AddedById = CurrentUserId();
            user.RetryCount = 0;
            user.IsDeleted = false;

            try
            {
                _userRepository.CreateUser(user);
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Could not create user: " + ex.Message);
                PopulateLists(user.RoleId, user.PackageId, user.ParentID);
                return View(user);
            }

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

            PopulateLists(user.RoleId, user.PackageId, user.ParentID, user.Id);
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

            if (model.RoleId == null)
            {
                ModelState.AddModelError("RoleId", "Role is required.");
            }

            // Keeping the existing parent is always allowed, even if it is no longer eligible.
            if (model.ParentID != user.ParentID)
            {
                ValidateParent(model.ParentID, id);
            }

            if (!ModelState.IsValid)
            {
                model.Id = id;
                PopulateLists(model.RoleId, model.PackageId, model.ParentID, id);
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
            user.UpdatedById = CurrentUserId();

            try
            {
                _userRepository.UpdateUser(user);
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Could not update user: " + ex.Message);
                model.Id = id;
                PopulateLists(model.RoleId, model.PackageId, model.ParentID, id);
                return View(model);
            }

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

        private int? CurrentUserId()
        {
            return Session["UserId"] == null ? (int?)null : Convert.ToInt32(Session["UserId"]);
        }

        // ParentID is optional; when given it must be an active, non-deleted
        // SuperAdmin / Admin / API User, and never the user being edited.
        private void ValidateParent(int? parentId, int? editingUserId)
        {
            if (parentId == null) return;

            if (parentId == editingUserId)
            {
                ModelState.AddModelError("ParentID", "A user cannot be their own parent.");
                return;
            }

            if (!_userRepository.GetParentUserOptions(ParentRoleIds).Any(p => p.Id == parentId.Value))
            {
                ModelState.AddModelError("ParentID", "Select a valid parent user.");
            }
        }

        private void PopulateLists(byte? selectedRoleId, int? selectedPackageId,
                                   int? selectedParentId, int? editingUserId = null)
        {
            ViewBag.RoleList = new SelectList(
                _userRepository.GetAllRoles(), "Id", "RoleName", selectedRoleId);

            ViewBag.PackageList = new SelectList(
                _packageRepository.GetAllPackages(), "Id", "PackageName", selectedPackageId);

            var parents = _userRepository.GetParentUserOptions(ParentRoleIds)
                                         .Where(p => p.Id != editingUserId)
                                         .ToList();

            // Show a saved parent that is no longer eligible so editing does not silently clear it.
            if (selectedParentId != null && parents.All(p => p.Id != selectedParentId.Value))
            {
                User current = _userRepository.GetUserById(selectedParentId.Value);
                if (current != null)
                {
                    parents.Insert(0, new ParentUserOption
                    {
                        Id       = current.Id,
                        Username = current.Username,
                        RoleId   = current.RoleId,
                        RoleName = (current.RoleName ?? "No role") + ", not eligible"
                    });
                }
            }

            ViewBag.ParentList = new SelectList(parents, "Id", "DisplayName", selectedParentId);
        }
    }
}
