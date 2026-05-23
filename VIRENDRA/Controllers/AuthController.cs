using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserRepository _userRepository;
        private const int MAX_RETRY_COUNT = 5;

        public AuthController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // GET: Auth/Login
        [HttpGet]
        public ActionResult Login()
        {
            return View(new LoginModel());
        }

        // POST: Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                // Fetch user from database using Dapper
                User user = _userRepository.GetUserByUsername(model.Username);

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid username or password");
                    return View(model);
                }

                // Check if user is locked
                if (user.IsLocked)
                {
                    ModelState.AddModelError("", "Your account is locked. Please contact administrator.");
                    return View(model);
                }

                // Check if user is deleted
                if (user.IsDeleted)
                {
                    ModelState.AddModelError("", "Your account has been deleted.");
                    return View(model);
                }

                // Check if user is active
                if (!user.IsActive)
                {
                    ModelState.AddModelError("", "Your account is inactive. Please contact administrator.");
                    return View(model);
                }

                // Verify password (in production, use proper hashing like BCrypt)
                if (user.Password == model.Password)
                {
                    // Reset retry count on successful login
                    _userRepository.UpdateRetryCount(user.Id, 0);

                    // Update login IP
                    string clientIP = Request.UserHostAddress;
                    _userRepository.UpdateLoginIP(user.Id, clientIP);

                    // Set authentication cookie
                    FormsAuthentication.SetAuthCookie(user.Username, model.RememberMe);
                    Session["UserId"] = user.Id;
                    Session["Username"] = user.Username;

                    return RedirectToAction("Dashboard", "Home");
                }
                else
                {
                    // Increment retry count
                    int newRetryCount = user.RetryCount + 1;

                    if (newRetryCount >= MAX_RETRY_COUNT)
                    {
                        // Lock the account
                        user.IsLocked = true;
                        _userRepository.UpdateRetryCount(user.Id, newRetryCount);
                        ModelState.AddModelError("", "Your account has been locked due to multiple failed login attempts. Please contact administrator.");
                    }
                    else
                    {
                        _userRepository.UpdateRetryCount(user.Id, newRetryCount);
                        int remainingAttempts = MAX_RETRY_COUNT - newRetryCount;
                        ModelState.AddModelError("", $"Invalid username or password. You have {remainingAttempts} attempts remaining.");
                    }
                }
            }

            return View(model);
        }

        // GET: Auth/Logout
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: Auth/Index
        public ActionResult Index()
        {
            return View();
        }
    }
}
