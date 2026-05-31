using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    [RoutePrefix("api")]
    public class MobileController : ApiController
    {
        private readonly IUserRepository    _userRepo;
        private readonly IRechargeRepository _rcRepo;
        private readonly string             _conn;

        public MobileController()
        {
            _conn     = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString ?? string.Empty;
            _userRepo = new UserRepository(_conn);
            _rcRepo   = new RechargeRepository();
        }

        // ────────────────────────────────────────────────────────────────────
        // BALANCE CHECK
        // ────────────────────────────────────────────────────────────────────
        [HttpGet, HttpPost]
        [Route("balance")]
        [Route("balanceCheck")]
        public IHttpActionResult BalanceCheck(string username = null, string token = null)
        {
            var kv       = ReadKV();
            username = FirstVal(username, kv, "username");
            token    = FirstVal(token,    kv, "token");

            User user = Auth(username, token, out string err);
            if (user == null) return Ok(new { status = "0", message = err });

            return Ok(new { status = "1", balance = Fmt(user.UserBal ?? 0) });
        }

        // ────────────────────────────────────────────────────────────────────
        // RECHARGE  (legacy endpoint — now calls full routing logic)
        // ────────────────────────────────────────────────────────────────────
        [HttpGet, HttpPost]
        [Route("recharge")]
        public IHttpActionResult Recharge(
            string username      = null, string token         = null,
            string number        = null, string operator_code = null,
            string circle_code   = null, decimal? amount      = null,
            string txn_id        = null, string optional1     = null,
            string optional2     = null, string optional3     = null)
        {
            return Recharge2(username, token, number, operator_code, circle_code, amount, txn_id);
        }

        // ────────────────────────────────────────────────────────────────────
        // RECHARGE2  (full SP-driven routing endpoint)
        // ────────────────────────────────────────────────────────────────────
        [HttpGet, HttpPost]
        [Route("recharge2")]
        [Route("~/Service/Recharge2")]
        public IHttpActionResult Recharge2(
            string username      = null, string token         = null,
            string number        = null, string operator_code = null,
            string circle_code   = null, decimal? amount      = null,
            string txn_id        = null)
        {
            var kv         = ReadKV();
            username       = FirstVal(username,      kv, "username");
            token          = FirstVal(token,         kv, "token");
            number         = FirstVal(number,        kv, "number");
            operator_code  = FirstVal(operator_code, kv, "operator_code");
            circle_code    = FirstVal(circle_code,   kv, "circle_code");
            txn_id         = FirstVal(txn_id,        kv, "txn_id");

            if (amount == null && kv.TryGetValue("amount", out string amtStr) &&
                decimal.TryParse(amtStr, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal pa))
                amount = pa;

            // 1. Authenticate
            User user = Auth(username, token, out string err);
            if (user == null) return Ok(new { status = "0", message = err });

            // 2. Basic field validation
            if (string.IsNullOrWhiteSpace(number))
                return Ok(new { status = "0", message = "number is required" });
            if (string.IsNullOrWhiteSpace(operator_code))
                return Ok(new { status = "0", message = "operator_code is required" });
            if (amount == null || amount <= 0)
                return Ok(new { status = "0", message = "valid amount is required" });

            // 3. Resolve operator
            Operator op = null;
            try { op = _rcRepo.GetOperatorByCode(operator_code.Trim()); }
            catch { /* DB error: will fail below */ }

            if (op == null)
                return Ok(new { status = "0", message = "Invalid operator_code" });

            // 4. Resolve circle (optional)
            Circle circle = null;
            if (!string.IsNullOrWhiteSpace(circle_code))
            {
                try { circle = _rcRepo.GetCircleByCode(circle_code.Trim()); }
                catch { /* non-fatal */ }
            }

            // 5. Build request model
            string refTxn = !string.IsNullOrWhiteSpace(txn_id)
                ? txn_id.Trim()
                : MakeTxnId();

            var rm = new RechargeModel
            {
                UserId     = user.Id,
                Username   = user.Username,
                ApiToken   = (token ?? string.Empty).Trim(),
                MobileNo   = number.Trim(),
                Amount     = amount.Value,
                OpId       = op.Id,
                OpCode     = op.OperatorCode,
                CircleId   = circle?.Id,
                CircleCode = circle?.CircleCode ?? (circle_code ?? string.Empty).Trim(),
                RefTxnId   = refTxn,
                OurTxnId   = MakeTxnId(),
                IpAddress  = ClientIp()
            };

            object result = RechargeRequest(rm);
            return Ok(result);
        }

        // ────────────────────────────────────────────────────────────────────
        // STATUS CHECK
        // ────────────────────────────────────────────────────────────────────
        [HttpGet, HttpPost]
        [Route("statusCheck")]
        [Route("status")]
        public IHttpActionResult StatusCheck(
            string username = null, string token  = null,
            string txn_id   = null, string number = null)
        {
            var kv   = ReadKV();
            username = FirstVal(username, kv, "username");
            token    = FirstVal(token,    kv, "token");
            txn_id   = FirstVal(txn_id,   kv, "txn_id");
            number   = FirstVal(number,   kv, "number");

            User user = Auth(username, token, out string err);
            if (user == null) return Ok(new { status = "0", message = err });

            if (string.IsNullOrWhiteSpace(txn_id))
                return Ok(new { status = "0", message = "txn_id is required" });

            if (string.IsNullOrWhiteSpace(number))
                return Ok(new { status = "0", message = "number is required" });

            StatusRow row = QueryStatus(user.Id, txn_id.Trim(), number.Trim());
            if (row == null)
                return Ok(new { status = "0", message = "Transaction not found" });

            return Ok(new
            {
                status             = row.StatusId.ToString(CultureInfo.InvariantCulture),
                amount             = Fmt(row.Amount),
                txn_id             = row.UserTxnId,
                operator_reference = row.ApiTxnId ?? string.Empty,
                message            = StatusMsg(row.StatusId)
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // RECHARGE PIPELINE
        // ════════════════════════════════════════════════════════════════════

        private object RechargeRequest(RechargeModel rm)
        {
            // ── 1. Validate via SP (with fallback to balance-check only) ────
            RechargeValidationResult val = null;
            try
            {
                val = _rcRepo.ValidateRechargeRequest(
                    rm.UserId, rm.MobileNo, rm.Amount,
                    rm.OpId ?? 0, rm.CircleId ?? 0, rm.RefTxnId,
                    rm.IpAddress);
            }
            catch
            {
                // SP not available → manual balance check only
                decimal bal = 0;
                try { bal = _rcRepo.GetUserBalance(rm.UserId); } catch { }

                if (bal < rm.Amount)
                    return new { status = "0", message = "Insufficient balance. Available: " + bal.ToString("0.##") };

                val = new RechargeValidationResult
                {
                    StatusCode   = 0,   // 0 = success in SP conventions
                    StatusMsg    = "OK",
                    SwitchTypeId = RouteType.COMMON_ROUTE,
                    DebitAmount  = rm.Amount,
                    CommAmount   = 0m
                };
            }

            // SP uses ErrorCode 0 for success; all non-zero codes are failures
            // except 104 (Callback Thread) which means "create & pending"
            if (val.StatusCode == 104)
            {
                // Callback thread: proceed but routing will handle it as pending
            }
            else if (val.StatusCode != 0)
            {
                return new { status = "0", message = val.StatusMsg };
            }

            // ── 2. Build helper ─────────────────────────────────────────────
            var helper = new RechargeHelperDto
            {
                SwitchId    = val.SwitchTypeId > 0 ? val.SwitchTypeId : RouteType.COMMON_ROUTE,
                DebitAmount = val.DebitAmount  > 0 ? val.DebitAmount  : rm.Amount,
                CommAmount  = val.CommAmount
            };

            rm.IsROffer     = val.IsROffer;
            rm.ROfferAmount = val.ROAmount;

            // ── 3. Get routing list ─────────────────────────────────────────
            try
            {
                switch (helper.SwitchId)
                {
                    case RouteType.CIRCLE_WISE:
                        helper.ApiRouteList = _rcRepo.GetCircleRouting(
                            rm.OpId ?? 0, rm.CircleId ?? 0, rm.Amount);
                        break;
                    case RouteType.AMOUNT_WISE:
                        helper.ApiRouteList = _rcRepo.GetAmountRoutes(
                            rm.OpId ?? 0, rm.CircleId ?? 0, rm.Amount);
                        break;
                    case RouteType.OPERATOR_WISE:
                        helper.ApiRouteList = _rcRepo.GetOperatorRoutes(
                            rm.OpId ?? 0, rm.Amount);
                        break;
                    default:
                        helper.ApiRouteList = _rcRepo.GetCommanRouting(
                            rm.OpId ?? 0, rm.Amount, rm.CircleCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                return new { status = "0", message = "Routing error: " + ex.Message };
            }

            if (helper.ApiRouteList == null || !helper.ApiRouteList.Any())
                return new { status = "0", message = "No active route found for this recharge" };

            return RechargeProcess(rm, helper);
        }

        private object RechargeProcess(RechargeModel rm, RechargeHelperDto helper)
        {
            bool firstRoute = true;

            foreach (var route in helper.ApiRouteList)
            {
                // ── Filter checks ───────────────────────────────────────────
                if (!UserAllowed(rm.UserId, route.UserFilter, route.BlockUser)) continue;
                if (!CircleAllowed(rm.CircleCode, route.CircleFilter)) continue;

                helper.CurrentApiId      = route.ApiId;
                helper.CurrentPriorityId = route.PriorityId;
                helper.RouteId           = route.RouteId;
                helper.FTypeId           = route.FTypeId;
                helper.RouteOP1          = route.RouteOP1;
                helper.RoutePriorityId   = route.RoutePriorityId;
                helper.MinRO             = route.MinRO;
                helper.CircleFilter      = route.CircleFilter;
                helper.BlockUser         = route.BlockUser;
                helper.UserFilter        = route.UserFilter;

                // ── Call usp_RechargeCreate ─────────────────────────────────
                // First call (switchedRecId=0): creates record + debits balance
                // Retry (switchedRecId=helper.RecId): updates existing record,
                //   no extra debit, returns URL for the new API
                CreateRechargeResult cr;
                try
                {
                    cr = _rcRepo.CreateRecharge(
                        rm.UserId,
                        rm.MobileNo,
                        rm.Amount,
                        helper.DebitAmount,
                        rm.OpId ?? 0,
                        rm.CircleId ?? 0,
                        route.ApiId,
                        rm.RefTxnId,
                        rm.OurTxnId ?? rm.RefTxnId,
                        rm.IpAddress,
                        helper.SwitchId,
                        firstRoute ? 0L : helper.RecId,
                        2,                       // mediumId = 2 (web)
                        route.CircleFilter);
                }
                catch (Exception ex)
                {
                    if (firstRoute)
                        return new { status = "0", message = "Error creating recharge: " + ex.Message };
                    continue;
                }

                firstRoute = false;

                // Store/update RecId; update commission from SP's accurate calculation
                if (cr.RecId > 0) { helper.RecId = cr.RecId; helper.TxnId = cr.TxnId; }
                if (cr.Comm1  > 0) helper.CommAmount = cr.Comm1;
                helper.ApiTypeId = cr.ApiTypeId;

                // ── Handle SP-level error codes ─────────────────────────────
                if (cr.StatusCode == 9)
                    // Insufficient balance — hard stop, no point retrying
                    return new { status = "0", message = cr.StatusMsg };

                if (cr.StatusCode == 102)
                {
                    // "Processing" default API — record created, return pending
                    return new
                    {
                        status             = "3",
                        txn_id             = rm.RefTxnId,
                        our_txn_id         = helper.RecId.ToString(CultureInfo.InvariantCulture),
                        operator_reference = string.Empty,
                        message            = cr.StatusMsg,
                        amount             = Fmt(helper.DebitAmount)
                    };
                }

                // 100 = Offline default, 101 = Failed default → try next route
                if (cr.StatusCode == 100 || cr.StatusCode == 101) continue;

                // Any other non-zero code → try next route
                if (cr.StatusCode != 0) continue;

                // ── Build actual API URL (SP returns raw templates + all creds) ─
                string apiUrl  = BuildUrl(cr.ApiUrl,   rm, cr);
                string postData = BuildUrl(cr.PostData, rm, cr);

                if (string.IsNullOrWhiteSpace(apiUrl)) continue;

                // ── Make API call ───────────────────────────────────────────
                string response = string.Empty;
                try { response = ApiGet(apiUrl, postData, cr.Method, cr.ContentType); }
                catch (Exception ex) { response = "HTTP_ERROR:" + ex.Message; }

                // ── Log ─────────────────────────────────────────────────────
                try { _rcRepo.AddUpdateReqRes(helper.RecId, apiUrl, response, route.ApiId); }
                catch { /* non-fatal */ }

                // ── Parse response ───────────────────────────────────────────
                List<FilterTag> tags = null;
                try { tags = _rcRepo.GetFilterTags(route.ApiId); }
                catch { /* keyword fallback */ }

                int    respStatus = ClassifyResponse(response, tags);
                string operRef    = ExtractOperRef(response, tags);
                string truncMsg   = response.Length > 500 ? response.Substring(0, 500) : response;

                if (respStatus == RechargeStatusCodes.SUCCESS)
                {
                    bool dl = false, rf = false; string spLog = string.Empty;
                    try
                    {
                        UpdateStatusWithCheck(
                            helper.RecId, rm.UserId, RechargeStatusCodes.SUCCESS,
                            operRef, string.Empty, truncMsg, "API Success",
                            ref dl, ref rf, ref spLog,
                            0, null, rm.OpId ?? 0, 0, rm.ROfferAmount);

                        if (!dl && helper.CommAmount > 0)
                            _rcRepo.InsertCommissionLedger(
                                helper.RecId, rm.UserId, rm.RefTxnId,
                                0, helper.CommAmount, rm.UserId);
                    }
                    catch { /* non-fatal */ }

                    return new
                    {
                        status             = "1",
                        txn_id             = rm.RefTxnId,
                        our_txn_id         = helper.RecId.ToString(CultureInfo.InvariantCulture),
                        operator_reference = operRef,
                        message            = "Recharge successful",
                        amount             = Fmt(helper.DebitAmount)
                    };
                }

                if (respStatus == RechargeStatusCodes.PROCESS)
                {
                    bool dl = false, rf = false; string spLog = string.Empty;
                    try
                    {
                        UpdateStatusWithCheck(
                            helper.RecId, rm.UserId, RechargeStatusCodes.PROCESS,
                            operRef, string.Empty, truncMsg, "API Pending",
                            ref dl, ref rf, ref spLog,
                            0, null, rm.OpId ?? 0, 0, 0);
                    }
                    catch { /* non-fatal */ }

                    return new
                    {
                        status             = "3",
                        txn_id             = rm.RefTxnId,
                        our_txn_id         = helper.RecId.ToString(CultureInfo.InvariantCulture),
                        operator_reference = operRef,
                        message            = "Recharge pending",
                        amount             = Fmt(helper.DebitAmount)
                    };
                }

                // FAILED → update and try next route
                try
                {
                    bool dl = false, rf = false; string spLog = string.Empty;
                    UpdateStatusWithCheck(
                        helper.RecId, rm.UserId, RechargeStatusCodes.FAILED,
                        string.Empty, string.Empty, truncMsg, "API Failed",
                        ref dl, ref rf, ref spLog,
                        0, null, rm.OpId ?? 0, 0, 0);
                }
                catch { /* non-fatal */ }
            }

            // ── All routes exhausted ────────────────────────────────────────
            if (helper.RecId > 0)
            {
                bool dl = false, rfDone = false; string spLog = string.Empty;
                try
                {
                    UpdateStatusWithCheck(
                        helper.RecId, rm.UserId, RechargeStatusCodes.FAILED,
                        string.Empty, string.Empty, "All routes failed", "Final Failure",
                        ref dl, ref rfDone, ref spLog,
                        0, null, rm.OpId ?? 0, 0, 0);
                }
                catch { /* non-fatal */ }

                if (!rfDone)
                    try { _rcRepo.InsertRefundLedger(helper.RecId, rm.UserId, rm.RefTxnId, 0, helper.DebitAmount, rm.UserId); }
                    catch { /* non-fatal */ }
            }

            return new { status = "2", txn_id = rm.RefTxnId, message = "Recharge failed. Amount refunded to wallet." };
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        // ── Auth ─────────────────────────────────────────────────────────────
        private User Auth(string username, string token, out string errMsg)
        {
            errMsg = string.Empty;

            if (string.IsNullOrWhiteSpace(username)) { errMsg = "username is required"; return null; }
            if (string.IsNullOrWhiteSpace(token))    { errMsg = "token is required";    return null; }

            User user = _userRepo.GetUserByUsername(username.Trim());

            if (user == null || user.IsDeleted)     { errMsg = "Invalid username";            return null; }
            if (!user.IsActive || user.IsLocked)    { errMsg = "Account is not active";       return null; }
            if (!string.Equals(user.TokenAPI, token.Trim(), StringComparison.Ordinal))
                                                    { errMsg = "Invalid token";               return null; }
            if (!IpOk(user))                        { errMsg = "IP address not allowed";      return null; }

            return user;
        }

        private bool IpOk(User user)
        {
            if (string.IsNullOrWhiteSpace(user.LoginIP)) return true;
            return string.Equals(user.LoginIP.Trim(), ClientIp(), StringComparison.OrdinalIgnoreCase);
        }

        // ── Request key-value reader ─────────────────────────────────────────
        private IDictionary<string, string> ReadKV()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in Request.GetQueryNameValuePairs())
                d[p.Key] = p.Value;

            HttpRequest req = HttpContext.Current?.Request;
            if (req != null)
                foreach (string k in req.Form.AllKeys.Where(k => k != null))
                    d[k] = req.Form[k];

            if (Request.Content?.Headers?.ContentType != null &&
                string.Equals(Request.Content.Headers.ContentType.MediaType,
                    "application/json", StringComparison.OrdinalIgnoreCase))
            {
                string body = Request.Content.ReadAsStringAsync().Result;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        foreach (JProperty prop in JObject.Parse(body).Properties())
                            d[prop.Name] = prop.Value.Type == JTokenType.Null
                                ? string.Empty : prop.Value.ToString();
                    }
                    catch (JsonReaderException) { }
                }
            }

            return d;
        }

        private static string FirstVal(string explicitVal,
            IDictionary<string, string> kv, string key)
        {
            if (!string.IsNullOrWhiteSpace(explicitVal)) return explicitVal;
            return kv.TryGetValue(key, out string v) ? v : explicitVal;
        }

        // ── IP ───────────────────────────────────────────────────────────────
        private static string ClientIp()
        {
            HttpRequest req = HttpContext.Current?.Request;
            if (req == null) return string.Empty;
            string fwd = req.Headers["X-Forwarded-For"];
            return !string.IsNullOrWhiteSpace(fwd)
                ? fwd.Split(',')[0].Trim()
                : req.UserHostAddress ?? string.Empty;
        }

        // ── Routing filter helpers ───────────────────────────────────────────
        private static bool UserAllowed(int userId, string userFilter, string blockUser)
        {
            string uid = userId.ToString(CultureInfo.InvariantCulture);

            // Block list has priority
            if (!string.IsNullOrWhiteSpace(blockUser))
            {
                bool blocked = blockUser.Split(',')
                    .Any(b => b.Trim().Equals(uid, StringComparison.Ordinal));
                if (blocked) return false;
            }

            // UserFilter: "All" means everyone allowed
            if (string.IsNullOrWhiteSpace(userFilter) ||
                userFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            return userFilter.Split(',')
                .Any(u => u.Trim().Equals(uid, StringComparison.Ordinal));
        }

        private static bool CircleAllowed(string circleCode, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                filter.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(circleCode)) return true;
            return filter.Split(',')
                .Any(f => f.Trim().Equals(circleCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // ── URL placeholder replacement ──────────────────────────────────────
        /// <summary>
        /// Replace all [XXX] placeholders using credentials and operator codes
        /// returned directly by usp_RechargeCreate.
        /// </summary>
        private static string BuildUrl(string template, RechargeModel rm, CreateRechargeResult cr)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            string rnd = new Random().Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
            string dt  = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            return template
                .Replace("[UUU]",  cr.ApiUserId    ?? string.Empty)   // API user ID
                .Replace("[PPP]",  cr.ApiPassword  ?? string.Empty)   // API password
                .Replace("[RRR]",  cr.ApiOptional  ?? string.Empty)   // remark/optional
                .Replace("[MMM]",  rm.MobileNo     ?? string.Empty)   // mobile number
                .Replace("[HHH]",  rm.MobileNo     ?? string.Empty)   // account (bill pay)
                .Replace("[AAA]",  rm.Amount.ToString("0.##", CultureInfo.InvariantCulture))
                .Replace("[OOO]",  cr.OpCode       ?? string.Empty)   // operator code
                .Replace("[CCC]",  rm.CircleCode   ?? string.Empty)   // circle code
                .Replace("[VVV]",  rm.RefTxnId     ?? string.Empty)   // user txn id
                .Replace("[TTT]",  rm.RefTxnId     ?? string.Empty)
                .Replace("[NNN]",  string.Empty)
                .Replace("[EEE]",  cr.ExtraUrl     ?? string.Empty)   // extra url
                .Replace("[DDD]",  cr.ExtraUrlData ?? string.Empty)   // extra data
                .Replace("[COMM1]",cr.Comm1.ToString("0.##", CultureInfo.InvariantCulture))
                .Replace("[FFFT]", dt)
                .Replace("[FFFR]", rnd);
        }

        // ── HTTP call ────────────────────────────────────────────────────────
        /// <summary>
        /// Make an HTTP call; uses Method and ContentType from usp_RechargeCreate when provided.
        /// </summary>
        private static string ApiGet(string url, string postBody,
            string method = null, string contentType = null)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout   = 30000;
            req.UserAgent = "VRecharge/1.0";

            bool isPost = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                          || (!string.IsNullOrEmpty(method) == false && !string.IsNullOrWhiteSpace(postBody));

            if (isPost && !string.IsNullOrWhiteSpace(postBody))
            {
                req.Method      = "POST";
                req.ContentType = !string.IsNullOrWhiteSpace(contentType)
                    ? contentType
                    : (postBody.TrimStart().StartsWith("{")
                        ? "application/json"
                        : "application/x-www-form-urlencoded");

                byte[] bytes = Encoding.UTF8.GetBytes(postBody);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);
            }
            else
            {
                req.Method = "GET";
            }

            using (var res = (HttpWebResponse)req.GetResponse())
            using (var rdr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                return rdr.ReadToEnd();
        }

        // ── Response classification ──────────────────────────────────────────
        private static int ClassifyResponse(string response, IEnumerable<FilterTag> tags)
        {
            if (string.IsNullOrWhiteSpace(response))
                return RechargeStatusCodes.FAILED;

            if (tags != null)
            {
                foreach (var t in tags.OrderBy(x => x.Id))
                {
                    if (!string.IsNullOrEmpty(t.TagValue) &&
                        response.IndexOf(t.TagValue, StringComparison.OrdinalIgnoreCase) >= 0)
                        return t.StatusId;
                }
            }

            // Keyword fallback
            string lower = response.ToLowerInvariant();
            if (lower.Contains("success"))                                    return RechargeStatusCodes.SUCCESS;
            if (lower.Contains("pending") || lower.Contains("process") ||
                lower.Contains("queue")   || lower.Contains("inprogress"))    return RechargeStatusCodes.PROCESS;
            return RechargeStatusCodes.FAILED;
        }

        // ── Operator reference extraction ────────────────────────────────────
        private static string ExtractOperRef(string response, IEnumerable<FilterTag> tags)
        {
            if (string.IsNullOrWhiteSpace(response)) return string.Empty;

            // Try to extract value associated with a "txnid" tag from the filter list
            if (tags != null)
            {
                var txnTag = tags.FirstOrDefault(t =>
                    !string.IsNullOrEmpty(t.TagName) &&
                    (t.TagName.IndexOf("txn",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.TagName.IndexOf("ref",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.TagName.IndexOf("opid", StringComparison.OrdinalIgnoreCase) >= 0));

                if (txnTag != null && !string.IsNullOrEmpty(txnTag.TagValue))
                {
                    // Try JSON key extraction: "TagValue":"<value>"
                    var m = Regex.Match(response,
                        "\"" + Regex.Escape(txnTag.TagValue) + "\"\\s*:\\s*\"?([^\"\\s,}]+)",
                        RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value;
                }
            }

            // Generic fallback patterns
            string[] patterns =
            {
                "\"txnid\"\\s*:\\s*\"([^\"]+)\"",
                "\"txn_id\"\\s*:\\s*\"([^\"]+)\"",
                "\"operatorid\"\\s*:\\s*\"([^\"]+)\"",
                "\"refid\"\\s*:\\s*\"([^\"]+)\"",
                "txnid=([^&\\s]+)",
                "TXNID=([^&\\s]+)"
            };
            foreach (var pat in patterns)
            {
                var m = Regex.Match(response, pat, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }

            return string.Empty;
        }

        // ── usp_UpdateRechargeStatus wrapper ────────────────────────────────
        /// <summary>
        /// Calls usp_UpdateRechargeStatus to update the recharge record and
        /// handle commission / refund distribution.
        /// <paramref name="isRefund"/> is set true by the SP when a refund was performed.
        /// <paramref name="isDownline"/> is set true when downline commission was distributed.
        /// </summary>
        private void UpdateStatusWithCheck(
            long recId, int userId, int statusId,
            string apiTxnId, string optTxnId, string statusMsg, string remark,
            ref bool isDownline, ref bool isRefund, ref string log,
            long lapuId = 0, string lapuNo = null, int opId = 0,
            decimal apiBal = 0, decimal rOffer = 0,
            string updateType = "StatusWithCheck", string comment = "")
        {
            remark = string.IsNullOrEmpty(remark) ? "StatusWithCheck" : remark;

            using (var con = new SqlConnection(_conn))
            {
                var cmd = new SqlCommand("usp_UpdateRechargeStatus", con)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@UpdateType", updateType);
                cmd.Parameters.AddWithValue("@RecId",      recId);
                cmd.Parameters.AddWithValue("@StatusId",   statusId);

                if (userId > 0)
                    cmd.Parameters.AddWithValue("@UserId", userId);
                if (!string.IsNullOrWhiteSpace(apiTxnId))
                    cmd.Parameters.AddWithValue("@ApiTxnId", apiTxnId);
                if (!string.IsNullOrWhiteSpace(optTxnId))
                    cmd.Parameters.AddWithValue("@OptTxnId", optTxnId);
                if (!string.IsNullOrWhiteSpace(statusMsg))
                    cmd.Parameters.AddWithValue("@StatusMsg", statusMsg);
                if (!string.IsNullOrWhiteSpace(remark))
                    cmd.Parameters.AddWithValue("@Remark", remark);
                if (lapuId > 0)
                    cmd.Parameters.AddWithValue("@LapuId", lapuId);
                if (!string.IsNullOrWhiteSpace(lapuNo))
                    cmd.Parameters.AddWithValue("@LapuNo", lapuNo);
                if (opId > 0)
                    cmd.Parameters.AddWithValue("@OpId", opId);
                if (apiBal > 0)
                    cmd.Parameters.AddWithValue("@ApiBal", apiBal);
                if (rOffer > 0)
                    cmd.Parameters.AddWithValue("@ROfferAmt", rOffer);
                if (!string.IsNullOrWhiteSpace(comment))
                    cmd.Parameters.AddWithValue("@Comment", comment);

                cmd.Parameters.Add("@Log",        SqlDbType.NVarChar, 250).Direction = ParameterDirection.Output;
                cmd.Parameters.Add("@IsRefund",   SqlDbType.Bit).Direction            = ParameterDirection.Output;
                cmd.Parameters.Add("@IsDownline", SqlDbType.Bit).Direction            = ParameterDirection.Output;

                con.Open();
                cmd.ExecuteNonQuery();

                string rfVal  = Convert.ToString(cmd.Parameters["@IsRefund"].Value);
                string dlVal  = Convert.ToString(cmd.Parameters["@IsDownline"].Value);
                string spLog  = Convert.ToString(cmd.Parameters["@Log"].Value);

                isRefund   = !string.IsNullOrEmpty(rfVal) && Convert.ToBoolean(rfVal);
                isDownline = !string.IsNullOrEmpty(dlVal) && Convert.ToBoolean(dlVal);
                log       += spLog;
            }
        }

        // ── Status check DB query ────────────────────────────────────────────
        private StatusRow QueryStatus(int userId, string txnId, string number)
        {
            if (string.IsNullOrWhiteSpace(_conn)) return null;
            try
            {
                using (var db = new System.Data.SqlClient.SqlConnection(_conn))
                    return db.QueryFirstOrDefault<StatusRow>(@"
                        SELECT TOP 1
                            Id, UserTxnId, CustomerNo, Amount, StatusId, ApiTxnId
                        FROM Recharge
                        WHERE UserId    = @UserId
                          AND UserTxnId = @TxnId
                          AND CustomerNo = @Number
                          AND (IsDeleted IS NULL OR IsDeleted = 0)
                        ORDER BY RequestTime DESC",
                        new { UserId=userId, TxnId=txnId, Number=number });
            }
            catch { return null; }
        }

        private static string StatusMsg(int statusId)
        {
            switch (statusId)
            {
                case 2: return "recharge_success";
                case 3: return "recharge_failed";
                case 4: return "recharge_refund";
                case 5: return "recharge_pending";
                default: return "recharge_processing";
            }
        }

        // ── Utilities ────────────────────────────────────────────────────────
        private static string MakeTxnId()
            => DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

        private static string Fmt(decimal amount)
            => amount.ToString("0.0000", CultureInfo.InvariantCulture);

        // ── Inner DTOs ───────────────────────────────────────────────────────
        private class StatusRow
        {
            public long    Id         { get; set; }
            public string  UserTxnId  { get; set; }
            public string  CustomerNo { get; set; }
            public decimal Amount     { get; set; }
            public int     StatusId   { get; set; }
            public string  ApiTxnId   { get; set; }
        }
    }
}
