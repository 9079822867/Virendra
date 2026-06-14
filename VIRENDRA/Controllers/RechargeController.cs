using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using VIRENDRA.Data;
using VIRENDRA.Infrastructure;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    [RoleAuthorize(RoleConstants.SuperAdmin, RoleConstants.Admin, RoleConstants.ApiUser, RoleConstants.Retailer)]
    public class RechargeController : Controller
    {
        private readonly RechargeService     _rechargeSvc;
        private readonly IRechargeRepository _rechargeRepo;

        public RechargeController(RechargeService rechargeSvc, IRechargeRepository rechargeRepo)
        {
            _rechargeSvc  = rechargeSvc;
            _rechargeRepo = rechargeRepo;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var vm = BuildViewModel(null);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(RechargeRequestModel req)
        {
            if (!ModelState.IsValid)
            {
                var vmFail = BuildViewModel(null);
                vmFail.Request = req;
                return View(vmFail);
            }

            int userId     = (int)Session["UserId"];
            int addedById  = userId;

            var result = _rechargeSvc.ProcessRecharge(req, userId, addedById);

            var vm = BuildViewModel(result);
            vm.Request = req;
            return View(vm);
        }

        // ────────────────────────────────────────────────────────────────────
        // FETCH OPERATOR — auto-detect operator + circle from a 10-digit number
        // via the external Plan API, then map to our local Operator / Circle ids.
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public JsonResult FetchOperator(string number)
        {
            number = (number ?? string.Empty).Trim();

            if (number.Length != 10 || !number.All(char.IsDigit))
                return Json(new { success = false, message = "Enter a valid 10-digit number." },
                    JsonRequestBehavior.AllowGet);

            string baseUrl = ConfigurationManager.AppSettings["PlanApiBaseUrl"]
                             ?? "https://onlinerechargeservice.in";
            string tokenId = ConfigurationManager.AppSettings["PlanApiTokenId"] ?? string.Empty;

            string opName = null, circleName = null, circleCode = null, apiMsg = null;
            try
            {
                string url = baseUrl.TrimEnd('/') +
                    "/Api/Plan/FetchOperator?TokenId=" + Uri.EscapeDataString(tokenId) +
                    "&Number=" + Uri.EscapeDataString(number);

                string raw = HttpGet(url);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    JObject j = JObject.Parse(raw);
                    opName     = (string)j["OperatorName"];
                    circleName = (string)j["CircleName"];
                    circleCode = (string)j["CircleCode"];
                    apiMsg     = (string)j["MESSAGE"];
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lookup failed: " + ex.Message },
                    JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrWhiteSpace(opName))
                return Json(new { success = false, message = apiMsg ?? "Operator could not be detected." },
                    JsonRequestBehavior.AllowGet);

            // Map external operator name -> our local Operator id
            var operators = _rechargeRepo.GetActiveOperators();
            var op = operators.FirstOrDefault(o =>
                        string.Equals(o.Name, opName, StringComparison.OrdinalIgnoreCase))
                  ?? operators.FirstOrDefault(o =>
                        !string.IsNullOrEmpty(o.Name) &&
                        (o.Name.IndexOf(opName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         opName.IndexOf(o.Name, StringComparison.OrdinalIgnoreCase) >= 0));

            // Map external circle (code first, then name) -> our local Circle id.
            // Circle display name may live in either Name or CircleName depending on query.
            var circles = _rechargeRepo.GetCircles();
            Func<Circle, string> cname = c => c.Name ?? c.CircleName;
            var circle = (!string.IsNullOrWhiteSpace(circleCode)
                            ? circles.FirstOrDefault(c =>
                                string.Equals(c.CircleCode, circleCode, StringComparison.OrdinalIgnoreCase))
                            : null)
                      ?? (!string.IsNullOrWhiteSpace(circleName)
                            ? circles.FirstOrDefault(c =>
                                string.Equals(cname(c), circleName, StringComparison.OrdinalIgnoreCase))
                            : null);

            return Json(new
            {
                success      = true,
                operatorName = opName,
                operatorId   = op?.Id ?? 0,
                opTypeId     = op?.OpTypeId ?? 0,
                matched      = op != null,
                circleName   = circleName,
                circleCode   = circleCode,
                circleId     = circle?.Id ?? 0
            }, JsonRequestBehavior.AllowGet);
        }

        private static string HttpGet(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method    = "GET";
            req.Timeout   = 15000;
            req.UserAgent = "VRecharge/1.0";
            req.Accept    = "application/json";

            using (var res = (HttpWebResponse)req.GetResponse())
            using (var rdr = new StreamReader(res.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                return rdr.ReadToEnd();
        }

        private RechargeFormViewModel BuildViewModel(RechargeResult result)
        {
            var operators = _rechargeRepo.GetActiveOperators();
            var circles   = _rechargeRepo.GetCircles();

            return new RechargeFormViewModel
            {
                OperatorList   = new System.Web.Mvc.SelectList(operators, "Id", "Name"),
                CircleList     = new System.Web.Mvc.SelectList(circles,   "Id", "Name"),
                Operators      = operators,
                Circles        = circles,
                RecentRecharges= _rechargeRepo.GetRecentRecharges(
                                    Session["UserId"] is int uid ? uid : 0),
                Result         = result,
                Request        = new RechargeRequestModel()
            };
        }
    }
}
