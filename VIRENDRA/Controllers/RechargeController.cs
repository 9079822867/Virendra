using System;
using System.Collections.Generic;
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

            // Operator is matched on the client by NAME (contains), not by id.
            // Map external circle (code first, then name) -> our local Circle id
            // so the circle dropdown can be pre-selected.
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
                circleName   = circleName,
                circleCode   = circleCode,
                circleId     = circle?.Id ?? 0
            }, JsonRequestBehavior.AllowGet);
        }

        // ────────────────────────────────────────────────────────────────────
        // BROWSE PLANS — fetch prepaid plans from the external BrowsePlan2 API.
        // ────────────────────────────────────────────────────────────────────
        [HttpGet]
        public JsonResult BrowsePlans(string operatorName, string opCode, int circleId, string circleName)
        {
            // Resolve the provider's numeric OpCode:
            //  - if a numeric opCode was supplied, use it directly
            //  - otherwise map from the operator name
            string providerOp = ResolveProviderOpCode(opCode, operatorName);
            if (string.IsNullOrEmpty(providerOp))
                return Json(new { success = false, message = "Plans are not available for this operator." },
                    JsonRequestBehavior.AllowGet);

            // BrowsePlan2 needs the provider's own numeric CircleID — map it
            // from the selected circle name (falls back to a valid default).
            int circle = ResolveProviderCircleId(circleName);

            string baseUrl = ConfigurationManager.AppSettings["PlanApiBaseUrl"]
                             ?? "https://onlinerechargeservice.in";

            var categories = new List<PlanCategory>();
            try
            {
                string url = baseUrl.TrimEnd('/') +
                    "/App/webservice/BrowsePlan2?OpCode=" + Uri.EscapeDataString(providerOp) +
                    "&CircleID=" + circle;

                string raw = HttpGet(url);
                JObject root = JObject.Parse(raw);

                if ((string)root["STATUS"] != "1" || root["RDATA"] == null)
                    return Json(new { success = false, message = "No plans found for this operator/circle." },
                        JsonRequestBehavior.AllowGet);

                foreach (var cat in root["RDATA"])
                {
                    var pc = new PlanCategory
                    {
                        Name     = (string)cat["name"],
                        FullName = (string)cat["fullName"]
                    };

                    foreach (var p in cat["plans"] ?? new JArray())
                    {
                        var item = new PlanItem
                        {
                            Id           = (long?)  p["id"]           ?? 0,
                            Amount       = (decimal?)p["amount"]      ?? 0,
                            Validity     = (string) p["validity"],
                            ValidityDays = (int?)   p["validityDays"] ?? 0,
                            Talktime     = (decimal?)p["talktime"]    ?? 0,
                            Benefit      = (string) p["benefit"],
                            Calls        = (string) p["calls"],
                            Sms          = (string) p["sms"],
                            Data         = (string) p["data"]
                        };

                        if (p["subscriptions"] is JArray subs)
                            foreach (var s in subs)
                            {
                                string nm = (string)s["name"];
                                if (!string.IsNullOrWhiteSpace(nm)) item.Subscriptions.Add(nm);
                            }

                        pc.Plans.Add(item);
                    }

                    if (pc.Plans.Count > 0) categories.Add(pc);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Plan lookup failed: " + ex.Message },
                    JsonRequestBehavior.AllowGet);
            }

            // Project to camelCase keys so the client JS (cat.plans, p.amount …) binds.
            var result = categories.Select(c => new
            {
                name     = c.Name,
                fullName = c.FullName,
                plans    = c.Plans.Select(p => new
                {
                    id            = p.Id,
                    amount        = p.Amount,
                    validity      = p.Validity,
                    validityDays  = p.ValidityDays,
                    talktime      = p.Talktime,
                    benefit       = p.Benefit,
                    calls         = p.Calls,
                    sms           = p.Sms,
                    data          = p.Data,
                    subscriptions = p.Subscriptions
                }).ToList()
            }).ToList();

            return Json(new { success = true, categories = result }, JsonRequestBehavior.AllowGet);
        }

        // Maps an operator (numeric code passthrough, else by name) to the
        // BrowsePlan2 provider's numeric OpCode.
        private static string ResolveProviderOpCode(string opCode, string operatorName)
        {
            if (!string.IsNullOrWhiteSpace(opCode) && opCode.All(char.IsDigit))
                return opCode;

            string n = (operatorName ?? string.Empty).ToLowerInvariant();
            if (n.Contains("airtel"))                                  return "1";
            if (n.Contains("jio"))                                     return "5";
            if (n.Contains("vi") || n.Contains("voda") || n.Contains("idea")) return "6";
            if (n.Contains("bsnl"))                                    return "3";
            return string.Empty;
        }

        // Provider BrowsePlan2 CircleID map, keyed by normalised circle name.
        private static readonly Dictionary<string, int> _circleMap =
            new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "delhi",            10 }, { "delhincr",         10 },
            { "upwest",           97 }, { "upeast",           54 },
            { "punjab",            2 }, { "himachalpradesh",   3 }, { "himanchalpradesh", 3 }, { "himachal", 3 },
            { "haryana",          96 }, { "jammukashmir",     55 },
            { "mumbai",           92 }, { "gujarat",          98 },
            { "madhyapradesh",    93 }, { "kolkata",          31 },
            { "westbengal",       70 }, { "odisha",           53 }, { "orissa", 53 },
            { "assam",            56 }, { "northeast",        15 },
            { "karnataka",         6 }, { "chennai",          40 },
            { "tamilnadu",        94 }, { "kerala",           95 },
            { "andhrapradesh",    49 }, { "bihar",            52 },
            { "maharashtra",      90 }
        };

        // Maps a circle display name to the provider's numeric CircleID.
        private static int ResolveProviderCircleId(string circleName)
        {
            string key = NormalizeCircle(circleName);
            if (key.Length > 0)
            {
                if (_circleMap.TryGetValue(key, out int id)) return id;
                foreach (var kv in _circleMap)
                    if (key.Contains(kv.Key) || kv.Key.Contains(key)) return kv.Value;
            }
            return 10; // valid default (Delhi-NCR) when circle is unknown
        }

        // lowercase, drop standalone "and"/"&", strip non-alphanumerics
        private static string NormalizeCircle(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var token in name.ToLowerInvariant()
                         .Split(new[] { ' ', '-', '_', '.', '&', ',', '/', '(', ')' },
                                StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "and") continue;
                foreach (char c in token)
                    if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private static string HttpGet(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method    = "GET";
            req.Timeout   = 20000;
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
