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
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    [RoutePrefix("api")]
    public partial class MobileController : ApiController   // v3 endpoints: MobileController.V3.cs
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

            // 5. Same pipeline as the web panel and the mobile app
            string refTxn = !string.IsNullOrWhiteSpace(txn_id) ? txn_id.Trim() : MakeTxnId();
            var r = ProcessApiRecharge(user, number.Trim(), op, circle, amount.Value, refTxn);

            // Legacy codes: 1 success, 3 pending, 2 failed after debit (refunded), 0 rejected up front
            if (r.Success || r.IsPending)
                return Ok(new
                {
                    status             = r.Success ? "1" : "3",
                    txn_id             = refTxn,
                    our_txn_id         = r.RecId.ToString(CultureInfo.InvariantCulture),
                    operator_reference = r.OperatorRef ?? r.ApiTxnId ?? string.Empty,
                    message            = r.Success ? "Recharge successful" : "Recharge pending",
                    amount             = Fmt(r.DebitAmount)
                });

            if (r.RecId > 0)
                return Ok(new { status = "2", txn_id = refTxn, message = "Recharge failed. Amount refunded to wallet." });
            return Ok(new { status = "0", message = r.Message });
        }

        /// <summary>API recharges go through RechargeService, the pipeline shared with web and app.</summary>
        private RechargeResult ProcessApiRecharge(User user, string number, Operator op, Circle circle,
            decimal amount, string clientRef)
        {
            var req = new RechargeRequestModel
            {
                OperatorId = op.Id,
                CircleId   = circle?.Id ?? 0,
                CustomerNo = number,
                Amount     = amount,
                UserTxnId  = clientRef
            };
            return new RechargeService(_rcRepo).ProcessRecharge(req, user.Id, user.Id, RechargeService.MEDIUM_API);
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
            if (KycBlocked(user))                   { errMsg = "KYC approval pending";        return null; }

            return user;
        }

        // Same rule as the portal (KycGateAttribute) and the mobile app: non-admin users need
        // approved KYC before they can use the API. Fails closed.
        private static bool KycBlocked(User user)
        {
            if (user.RoleId == RoleConstants.SuperAdmin || user.RoleId == RoleConstants.Admin) return false;
            try { return !new KycRepository().IsApproved(user.Id); }
            catch { return true; }
        }

        // User.LoginIP may hold several IPs ("1.2.3.4,5.6.7.8"); empty = any IP
        private bool IpOk(User user) => Infrastructure.IpWhitelist.Allows(user.LoginIP, ClientIp());

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

            // Behind Cloudflare the socket address is a Cloudflare edge; CF-Connecting-IP is the caller
            string cf = req.Headers["CF-Connecting-IP"];
            if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();

            string fwd = req.Headers["X-Forwarded-For"];
            return !string.IsNullOrWhiteSpace(fwd)
                ? fwd.Split(',')[0].Trim()
                : req.UserHostAddress ?? string.Empty;
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
                          AND CustomerNo = @Number   -- Recharge has no IsDeleted column
                        ORDER BY RequestTime DESC",
                        new { UserId=userId, TxnId=txnId, Number=number });
            }
            catch { return null; }
        }

        private static string StatusMsg(int statusId)
        {
            // Recharge.StatusId (StatusType): 1 success, 2 failure, 3 pending, 4 refund, 5 hold
            switch (statusId)
            {
                case 1: return "recharge_success";
                case 2: return "recharge_failed";
                case 4: return "recharge_refund";
                case 3:
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
