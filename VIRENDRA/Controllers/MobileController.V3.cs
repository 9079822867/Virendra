using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VIRENDRA.Data;
using VIRENDRA.Models;
using VIRENDRA.Services;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Reseller API v3 (documented on ApiDocument pages):
    ///   Recharge  GET /transaction/recharge/?mobileno=…  GET /transaction/recharge/{id}
    ///   Wallet    GET /user/api/wallets
    ///   Disputes  GET /report/api/complaints/raise       GET /report/api/complaints/{id}
    ///             GET /report/api/complaints
    /// Auth: API key = User.TokenAPI, as ?key= or the x-api-key header (header preferred).
    /// The older /api/recharge, /api/status and /api/balance keep their own format.
    /// </summary>
    public partial class MobileController
    {
        // ════════════════════════════════════════════════════════════════════
        // RECHARGE
        // ════════════════════════════════════════════════════════════════════

        [HttpGet, HttpPost]
        [Route("~/transaction/recharge")]
        public IHttpActionResult V3Recharge()
        {
            var kv = ReadKV();
            string mobileNo  = Val(kv, "mobileno") ?? Val(kv, "msisdn");   // "msisdn" = earlier name, still accepted
            string opCode    = Val(kv, "operatorCode");
            string circle    = Val(kv, "circle");
            string clientRef = Val(kv, "clientRef");
            decimal.TryParse(Val(kv, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount);
            // "type" (P2P / P2A) is accepted for compatibility; routing decides the vendor.

            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            IHttpActionResult Failed(string why, string reason = null) => V3Json(new
            {
                status    = "Failed",
                txid      = (string)null,
                message   = why,
                remark    = (string)null,
                clientRef = string.IsNullOrWhiteSpace(clientRef) ? null : clientRef,
                req_time  = ApiV3.NowIso(),
                number    = string.IsNullOrWhiteSpace(mobileNo) ? null : mobileNo,
                amount    = amount > 0 ? (decimal?)amount : null,
                reason,
                request_ip = ClientIp()
            });

            if (string.IsNullOrWhiteSpace(mobileNo) || !mobileNo.Trim().All(char.IsDigit) || mobileNo.Trim().Length < 6 || mobileNo.Trim().Length > 15)
                return Failed("mobileno must be the mobile / subscriber number (digits only).", "INVALID_MOBILENO");
            if (string.IsNullOrWhiteSpace(opCode))
                return Failed("operatorCode is required.", "INVALID_OPERATOR");
            if (amount <= 0)
                return Failed("amount must be a positive number in Rupees.", "INVALID_AMOUNT");
            if (string.IsNullOrWhiteSpace(clientRef))
                return Failed("clientRef is required and must be unique per recharge.", "MISSING_CLIENT_REF");

            mobileNo = mobileNo.Trim(); clientRef = clientRef.Trim();
            if (clientRef.Length > 50)
                return Failed("clientRef can be at most 50 characters.", "INVALID_CLIENT_REF");
            if (FindUserRecharge(user.Id, clientRef) != null)
                return Failed("Duplicate clientRef: a recharge with this reference already exists.", "DUPLICATE_CLIENT_REF");

            Operator op = null;
            try { op = _rcRepo.GetOperatorByCode(opCode.Trim()); } catch { }
            if (op == null)
                return Failed("Invalid operatorCode. See the Operator Codes page.", "INVALID_OPERATOR");

            Circle ci = null;
            if (!string.IsNullOrWhiteSpace(circle))
            {
                try { ci = _rcRepo.GetCircleByCode(circle.Trim()); } catch { }
            }

            var rm = new RechargeModel
            {
                UserId     = user.Id,
                Username   = user.Username,
                ApiToken   = user.TokenAPI,
                MobileNo   = mobileNo,
                Amount     = amount,
                OpId       = op.Id,
                OpCode     = op.OperatorCode,
                CircleId   = ci?.Id,
                CircleCode = ci?.CircleCode ?? (circle ?? string.Empty).Trim(),
                RefTxnId   = clientRef,
                OurTxnId   = MakeTxnId(),
                IpAddress  = ClientIp()
            };

            // Same pipeline as /api/recharge (validation SP → routing → vendor call)
            JObject r = JObject.FromObject(RechargeRequest(rm));
            string code    = (string)r["status"] ?? "0";
            string message = (string)r["message"] ?? string.Empty;

            var row = FindUserRecharge(user.Id, clientRef);
            if (row == null)
            {
                // Rejected before a recharge row was created (balance, validation, routing…)
                bool lowBal = message.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0
                           || message.IndexOf("balance", StringComparison.OrdinalIgnoreCase) >= 0;
                return Failed(message.Length > 0 ? message : "Recharge could not be created.",
                              lowBal ? "INSUFFICIENT_BALANCE" : "REJECTED");
            }

            string status = code == "1" ? "Success" : code == "3" ? "Processing" : ApiV3.RechargeStatus(row.StatusId);
            string operatorRef = FirstNonEmpty((string)r["operator_reference"], row.OptTxnId, row.ApiTxnId);

            return V3Json(new
            {
                status,
                txnId     = row.SystemId,
                message   = status == "Success"    ? FirstNonEmpty(operatorRef, "Recharge successful")
                          : status == "Processing" ? "Processing your request."
                          : FirstNonEmpty(message, "Recharge failed"),
                clientRef,
                system_id = row.SystemId,
                remark    = (string)null,
                req_time  = ApiV3.Iso(row.RequestTime),
                number    = row.CustomerNo,
                amount    = row.Amount
            });
        }

        [HttpGet]
        [Route("~/transaction/recharge/{id}")]
        public IHttpActionResult V3RechargeStatus(string id)
        {
            var kv = ReadKV();
            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            var row = FindUserRecharge(user.Id, id);
            if (row == null)
                return V3NotFound("Transaction not found with this #" + id + " request id or client ref.", "txnId");

            return V3Json(new
            {
                status     = ApiV3.RechargeStatus(row.StatusId),
                txnId      = FirstNonEmpty(row.OptTxnId, row.ApiTxnId),   // operator reference
                message    = StatusMessage(row),
                remark     = (string)null,
                clientRef  = row.UserTxnId,
                req_time   = ApiV3.Iso(row.RequestTime),
                number     = row.CustomerNo,
                @operator  = row.Operator,
                circle     = row.Circle,
                amounts    = row.Amount,
                commission = row.Commission > 0 ? (long?)Paise(row.Commission) : null,   // gross, paise
                margin     = row.Commission != 0 ? (decimal?)row.Commission : null      // net, Rupees
            });
        }


        // ════════════════════════════════════════════════════════════════════
        // WALLET
        // ════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("~/user/api/wallets")]
        public IHttpActionResult V3Wallets()
        {
            var kv = ReadKV();
            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            string type = (Val(kv, "type") ?? string.Empty).Trim().ToUpperInvariant();
            var wallets = new Dictionary<string, object>();

            if (type.Length == 0 || type == "RECHARGE")
            {
                var w = WithDb(c => c.QueryFirstOrDefault(
                    "SELECT ISNULL(UserBal, 0) AS Bal, UpdatedDate, AddedDate FROM [User] WHERE Id = @Id", new { Id = user.Id }));
                wallets["RECHARGE"] = new
                {
                    id        = user.Id.ToString(CultureInfo.InvariantCulture),
                    type      = "RECHARGE",
                    balance   = Math.Round((decimal)w.Bal, 2),
                    status    = user.IsActive && !user.IsLocked ? "ACTIVE" : "BLOCKED",
                    currency  = "INR",
                    updatedAt = ApiV3.Iso((DateTime?)w.UpdatedDate ?? DateTime.Now),
                    createdAt = ApiV3.Iso((DateTime?)w.AddedDate)
                };
            }

            return V3Json(new { wallets, count = wallets.Count });
        }

        // ════════════════════════════════════════════════════════════════════
        // DISPUTES
        // ════════════════════════════════════════════════════════════════════

        [HttpGet, HttpPost]
        [Route("~/report/api/complaints/raise")]
        public IHttpActionResult V3RaiseComplaint()
        {
            var kv = ReadKV();
            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            string txnId       = Val(kv, "txnId");
            string reasonCode  = (Val(kv, "reasonCode") ?? "OTHER").Trim().ToUpperInvariant();
            string subject     = Val(kv, "subject");
            string description = Val(kv, "description");
            decimal.TryParse(Val(kv, "askedAmount"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal asked);

            if (string.IsNullOrWhiteSpace(txnId))
                return V3Message("txnId is required.", HttpStatusCode.BadRequest);
            if (!ApiV3.ReasonCodes.Contains(reasonCode))
                return V3Message("reasonCode must be one of " + string.Join(", ", ApiV3.ReasonCodes) + ".", HttpStatusCode.BadRequest);
            if ((subject ?? "").Length > 200)
                return V3Message("subject can be at most 200 characters.", HttpStatusCode.BadRequest);
            if ((description ?? "").Length > 2000)
                return V3Message("description can be at most 2000 characters.", HttpStatusCode.BadRequest);

            var row = FindUserRecharge(user.Id, txnId.Trim());
            if (row == null)
                return V3Message("Transaction not found with this #" + txnId.Trim() + " request id or client ref.", HttpStatusCode.NotFound);

            string remark = ApiV3.ComplaintRemark(reasonCode, subject, description, asked > 0 ? asked : (decimal?)null);
            var svc = new ComplaintService(new ComplaintRepository(), _rcRepo);
            var result = svc.Book(row.Id.ToString(CultureInfo.InvariantCulture), remark, user.Id, user.Id);

            if (result.Status != ComplaintBookStatus.Booked)
            {
                var code = result.Status == ComplaintBookStatus.Duplicate ? HttpStatusCode.Conflict
                         : result.Status == ComplaintBookStatus.NotFound  ? HttpStatusCode.NotFound
                         : HttpStatusCode.BadRequest;
                string msg = result.Status == ComplaintBookStatus.Duplicate ? "A complaint already exists for this transaction."
                           : result.Status == ComplaintBookStatus.NotSuccess ? "Complaints can be raised only for successful recharges."
                           : result.Message;
                return V3Message(msg, code);
            }

            var now = DateTime.Now;
            return V3Json(new
            {
                message     = "Complaint created successfully",
                ticketId    = ApiV3.Ticket(result.ComplaintId, now),
                complaintId = result.ComplaintId.ToString(CultureInfo.InvariantCulture),
                status      = "OPEN",
                reasonCode,
                txnId       = row.SystemId,
                clientRef   = row.UserTxnId,
                createdAt   = ApiV3.Iso(now)
            }, HttpStatusCode.Created);
        }

        [HttpGet]
        [Route("~/report/api/complaints/{id}")]
        public IHttpActionResult V3Complaint(string id) => ComplaintById(id);


        [HttpGet]
        [Route("~/report/api/complaints")]
        public IHttpActionResult V3Complaints()
        {
            var kv = ReadKV();
            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            int.TryParse(Val(kv, "page"), out int page);
            int.TryParse(Val(kv, "limit"), out int limit);
            page  = Math.Max(1, page);
            limit = limit <= 0 ? 20 : Math.Min(50, limit);

            var statuses = (Val(kv, "status") ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ApiV3.ComplaintStatusId).Where(s => s.HasValue).Select(s => (int)s.Value).ToList();
            bool filter = statuses.Any();

            int total = WithDb(c => c.ExecuteScalar<int>(@"
                SELECT COUNT(1) FROM Complaint cp INNER JOIN Recharge r ON r.Id = cp.RecId
                WHERE r.UserId = @UserId AND (@Filter = 0 OR cp.StatusId IN @Statuses)",
                new { UserId = user.Id, Filter = filter ? 1 : 0, Statuses = filter ? statuses : new List<int> { -1 } }));

            var rows = WithDb(c => c.Query<V3ComplaintRow>(ComplaintSql + @"
                WHERE r.UserId = @UserId AND (@Filter = 0 OR cp.StatusId IN @Statuses)
                ORDER BY cp.Id DESC
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                new { UserId = user.Id, Filter = filter ? 1 : 0, Statuses = filter ? statuses : new List<int> { -1 },
                      Skip = (page - 1) * limit, Take = limit }).ToList());

            return V3Json(new
            {
                data = rows.Select(x =>
                {
                    var p = ApiV3.ParseRemark(x.Remark);
                    return new
                    {
                        _id         = x.Id.ToString(CultureInfo.InvariantCulture),
                        ticketId    = ApiV3.Ticket(x.Id, x.ComplaintDate),
                        status      = ApiV3.ComplaintStatus(x.StatusId),
                        reasonCode  = p.Reason,
                        subject     = p.Subject,
                        askedAmount = p.Asked ?? x.Amount,
                        txnId       = x.SystemId,
                        createdAt   = ApiV3.Iso(x.ComplaintDate)
                    };
                }),
                pagination = new { total, page, pages = (int)Math.Ceiling(total / (double)limit), limit }
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private IHttpActionResult ComplaintById(string id)
        {
            var kv = ReadKV();
            User user = V3Auth(kv, out IHttpActionResult authError);
            if (user == null) return authError;

            long? complaintId = ApiV3.ParseTicket(id);
            var x = complaintId == null ? null : WithDb(c => c.QueryFirstOrDefault<V3ComplaintRow>(
                ComplaintSql + " WHERE cp.Id = @Id AND r.UserId = @UserId", new { Id = complaintId, UserId = user.Id }));
            if (x == null)
                return V3Message("Complaint not found", HttpStatusCode.NotFound);

            var p = ApiV3.ParseRemark(x.Remark);
            return V3Json(new
            {
                data = new
                {
                    _id         = x.Id.ToString(CultureInfo.InvariantCulture),
                    ticketId    = ApiV3.Ticket(x.Id, x.ComplaintDate),
                    status      = ApiV3.ComplaintStatus(x.StatusId),
                    reasonCode  = p.Reason,
                    subject     = p.Subject,
                    description = p.Description,
                    askedAmount = p.Asked ?? x.Amount,
                    txnId       = x.SystemId,
                    createdAt   = ApiV3.Iso(x.ComplaintDate),
                    updatedAt   = ApiV3.Iso(x.UpdatedDate ?? x.ResolvedDate ?? x.ComplaintDate)
                }
            });
        }

        private const string ComplaintSql = @"
            SELECT cp.Id, ISNULL(cp.StatusId, 0) AS StatusId, cp.Remark, cp.ComplaintDate, cp.UpdatedDate, cp.ResolvedDate,
                   r.Amount, COALESCE(r.OurRefTxnId, CAST(r.Id AS varchar(30))) AS SystemId
            FROM Complaint cp
            INNER JOIN Recharge r ON r.Id = cp.RecId";

        /// <summary>
        /// The caller's own recharge by system id (OurRefTxnId), clientRef (UserTxnId) or Recharge.Id.
        /// </summary>
        private V3RechargeRow FindUserRecharge(int userId, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long recId);
            return WithDb(c => c.QueryFirstOrDefault<V3RechargeRow>(@"
                SELECT TOP 1 r.Id, r.UserTxnId, r.OurRefTxnId, r.ApiTxnId, r.OptTxnId, r.CustomerNo,
                       r.Amount, ISNULL(r.Recharge_Commision, 0) AS Commission, ISNULL(r.StatusId, 3) AS StatusId,
                       r.StatusMsg, r.RequestTime, o.Name AS Operator, ci.CircleName AS Circle
                FROM Recharge r
                LEFT JOIN [Operator] o ON o.Id  = r.OpId
                LEFT JOIN Circle    ci ON ci.Id = r.CircleId
                WHERE r.UserId = @UserId
                  AND (r.OurRefTxnId = @Ref OR r.UserTxnId = @Ref OR (@RecId > 0 AND r.Id = @RecId))
                ORDER BY CASE WHEN r.OurRefTxnId = @Ref THEN 0 WHEN r.UserTxnId = @Ref THEN 1 ELSE 2 END, r.Id DESC",
                new { UserId = userId, Ref = id.Trim(), RecId = recId }));
        }

        private static string StatusMessage(V3RechargeRow row)
        {
            string st = ApiV3.RechargeStatus(row.StatusId);
            return st == "Success"    ? FirstNonEmpty(row.OptTxnId, row.ApiTxnId, "Recharge successful")
                 : st == "Processing" ? "Processing your request."
                 : "Recharge failed";
        }

        /// <summary>API key from the x-api-key header (preferred) or ?key=. Same account rules as /api.</summary>
        private User V3Auth(IDictionary<string, string> kv, out IHttpActionResult error)
        {
            error = null;
            string key = Request.Headers.TryGetValues("x-api-key", out IEnumerable<string> h) ? h.FirstOrDefault() : null;
            if (string.IsNullOrWhiteSpace(key)) key = Val(kv, "key");

            if (string.IsNullOrWhiteSpace(key))
            {
                error = V3Message("API key is required: send the x-api-key header or ?key=.", HttpStatusCode.Unauthorized);
                return null;
            }

            // The key must match exactly one user
            var ids = WithDb(c => c.Query<int>(
                "SELECT TOP 2 Id FROM [User] WHERE TokenAPI = @Key", new { Key = key.Trim() }).ToList());
            User user = ids.Count == 1 ? _userRepo.GetUserById(ids[0]) : null;

            string err = null;
            if (user == null || user.IsDeleted)          err = "Invalid API key.";
            else if (!user.IsActive || user.IsLocked)    err = "Account is not active.";
            else if (!IpOk(user))                        err = "IP address not allowed.";
            else if (KycBlocked(user))                   err = "KYC approval pending.";

            if (err == null) return user;
            error = V3Message(err, err == "Invalid API key." ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
            return null;
        }

        private IHttpActionResult V3Json(object body, HttpStatusCode code = HttpStatusCode.OK)
        {
            // Keep our JSON on 401/404: no Forms-auth login redirect, no IIS error page
            var ctx = System.Web.HttpContext.Current;
            if (ctx != null)
            {
                ctx.Response.TrySkipIisCustomErrors = true;
                ctx.Response.SuppressFormsAuthenticationRedirect = true;
            }
            return ResponseMessage(new HttpResponseMessage(code)
            {
                Content = new StringContent(JsonConvert.SerializeObject(body, ApiV3.Json), Encoding.UTF8, "application/json")
            });
        }

        private IHttpActionResult V3Message(string message, HttpStatusCode code) => V3Json(new { message }, code);

        // Not-found / bad-request in the recharge response shape
        private IHttpActionResult V3NotFound(string message, string idField, HttpStatusCode code = HttpStatusCode.NotFound)
        {
            var body = new JObject
            {
                ["status"]    = "Failed",
                [idField]     = null,
                ["message"]   = message,
                ["remark"]    = null,
                ["clientRef"] = null,
                ["req_time"]  = ApiV3.NowIso(),
                ["number"]    = null,
                ["amount"]    = null
            };
            return V3Json(body, code);
        }

        private T WithDb<T>(Func<SqlConnection, T> f)
        {
            using (var c = new SqlConnection(_conn)) return f(c);
        }

        private static string Val(IDictionary<string, string> kv, string key) =>
            kv.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        private static string FirstNonEmpty(params string[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        private static long Paise(decimal rupees) => (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);

        private class V3RechargeRow
        {
            public long     Id          { get; set; }
            public string   UserTxnId   { get; set; }
            public string   OurRefTxnId { get; set; }
            public string   ApiTxnId    { get; set; }
            public string   OptTxnId    { get; set; }
            public string   CustomerNo  { get; set; }
            public decimal  Amount      { get; set; }
            public decimal  Commission  { get; set; }
            public int      StatusId    { get; set; }
            public string   StatusMsg   { get; set; }
            public DateTime RequestTime { get; set; }
            public string   Operator    { get; set; }
            public string   Circle      { get; set; }
            public string   SystemId => string.IsNullOrWhiteSpace(OurRefTxnId) ? Id.ToString(CultureInfo.InvariantCulture) : OurRefTxnId;
        }

        private class V3ComplaintRow
        {
            public long      Id            { get; set; }
            public int       StatusId      { get; set; }
            public string    Remark        { get; set; }
            public DateTime  ComplaintDate { get; set; }
            public DateTime? UpdatedDate   { get; set; }
            public DateTime? ResolvedDate  { get; set; }
            public decimal   Amount        { get; set; }
            public string    SystemId      { get; set; }
        }
    }
}
