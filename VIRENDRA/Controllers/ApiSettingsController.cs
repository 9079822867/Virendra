using System;
using System.Net;
using System.Web.Mvc;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [Authorize]
    public class ApiSettingsController : Controller
    {
        private readonly IUserRepository _userRepository;

        public ApiSettingsController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet]
        public ActionResult Index()
        {
            User user = GetCurrentUser();

            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var model = new ApiKeySettingsViewModel
            {
                UserId = user.Id,
                Username = user.Username,
                IpAddress = user.LoginIP,
                Token = EnsureToken(user),
                CallbackUrl = user.CallbackURL,
                IsVerified = user.IsActive && !string.IsNullOrWhiteSpace(user.LoginIP)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateAPIKeys(ApiKeySettingsViewModel model)
        {
            User user = GetCurrentUser();

            if (user == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return Json(new { success = false, message = "Login required." });
            }

            if (string.IsNullOrWhiteSpace(model.IpAddress) || !IPAddress.TryParse(model.IpAddress.Trim(), out _))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(new { success = false, message = "Enter a valid IP address." });
            }

            if (!string.IsNullOrWhiteSpace(model.CallbackUrl) &&
                (!Uri.TryCreate(model.CallbackUrl.Trim(), UriKind.Absolute, out Uri callbackUri) ||
                 (callbackUri.Scheme != Uri.UriSchemeHttp && callbackUri.Scheme != Uri.UriSchemeHttps)))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(new { success = false, message = "Enter a valid callback URL." });
            }

            string token = string.IsNullOrWhiteSpace(user.TokenAPI)
                ? GenerateToken()
                : user.TokenAPI;

            _userRepository.UpdateApiSettings(
                user.Id,
                model.IpAddress.Trim(),
                (model.CallbackUrl ?? string.Empty).Trim(),
                token);

            return Json(new
            {
                success = true,
                message = "API settings updated successfully.",
                data = new
                {
                    userId = user.Id,
                    ipAddress = model.IpAddress.Trim(),
                    token,
                    callbackUrl = (model.CallbackUrl ?? string.Empty).Trim(),
                    isVerified = true
                }
            });
        }

        private User GetCurrentUser()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                return _userRepository.GetUserByUsername(User.Identity.Name);
            }

            if (Session["UserId"] is int userId)
            {
                return _userRepository.GetUserById(userId);
            }

            if (Session["Username"] is string username && !string.IsNullOrWhiteSpace(username))
            {
                return _userRepository.GetUserByUsername(username);
            }

            return null;
        }

        private string EnsureToken(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.TokenAPI))
            {
                return user.TokenAPI;
            }

            string token = GenerateToken();
            _userRepository.UpdateApiSettings(user.Id, user.LoginIP, user.CallbackURL, token);
            return token;
        }

        private static string GenerateToken()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
