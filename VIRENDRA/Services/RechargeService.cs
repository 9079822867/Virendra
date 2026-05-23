using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    public class RechargeService
    {
        private readonly IRechargeRepository _repo;

        public RechargeService(IRechargeRepository repo)
        {
            _repo = repo;
        }

        public RechargeResult ProcessRecharge(RechargeRequestModel req, int userId, int addedById)
        {
            // Auto-generate UserTxnId if not provided
            if (string.IsNullOrWhiteSpace(req.UserTxnId))
                req.UserTxnId = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            // 1. Validate balance
            decimal balance = _repo.GetUserBalance(userId);
            if (balance < req.Amount)
                return Fail("Insufficient balance. Available: ₹" + balance.ToString("0.##"));

            // 2. Get API URL for this operator
            var apiUrl = _repo.GetRechargeApiUrl(req.OperatorId);
            if (apiUrl == null || string.IsNullOrWhiteSpace(apiUrl.Url))
                return Fail("No active API configured for this operator.");

            // 3. Get operator code for this API
            string extraUrl, extraData;
            string opCode = _repo.GetOperatorApiCode(req.OperatorId, apiUrl.ApiId,
                out extraUrl, out extraData);

            // 4. Get circle code
            string circleCode = "";
            var circles = _repo.GetCircles();
            var circle  = circles.Find(c => c.Id == req.CircleId);
            if (circle != null) circleCode = circle.CircleCode ?? "";

            // 5. Insert Recharge (Pending) + TxnLedger (Debit) in one DB transaction
            long recId, txnId;
            decimal opBal;
            try
            {
                (recId, txnId, opBal) = _repo.InsertPendingRecharge(
                    userId, req.OperatorId, req.CircleId,
                    req.CustomerNo, req.Amount, req.RCTypeId,
                    req.UserTxnId, apiUrl.ApiId, addedById);
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }

            // 6. Build the API request URL with placeholder replacement
            string builtUrl = ReplacePlaceholders(apiUrl.Url, new PlaceholderContext
            {
                ApiUserId   = apiUrl.ApiUserId,
                ApiPassword = apiUrl.ApiPassword,
                Remark      = apiUrl.Remark,
                CustomerNo  = req.CustomerNo,
                Amount      = req.Amount,
                OpCode      = opCode,
                CircleCode  = circleCode,
                UserTxnId   = req.UserTxnId,
                ExtraUrl    = extraUrl,
                ExtraData   = extraData,
            });

            string postData = null;
            if (!string.IsNullOrWhiteSpace(apiUrl.PostData))
                postData = ReplacePlaceholders(apiUrl.PostData, new PlaceholderContext
                {
                    ApiUserId   = apiUrl.ApiUserId,
                    ApiPassword = apiUrl.ApiPassword,
                    Remark      = apiUrl.Remark,
                    CustomerNo  = req.CustomerNo,
                    Amount      = req.Amount,
                    OpCode      = opCode,
                    CircleCode  = circleCode,
                    UserTxnId   = req.UserTxnId,
                    ExtraUrl    = extraUrl,
                    ExtraData   = extraData,
                });

            // 7. Make HTTP call
            string responseText = "";
            bool httpError = false;
            try
            {
                responseText = MakeHttpCall(builtUrl, apiUrl.Method, postData, apiUrl.ResType);
            }
            catch (Exception ex)
            {
                responseText = "HTTP_ERROR: " + ex.Message;
                httpError = true;
            }

            // 8. Log request/response
            try
            {
                _repo.LogRequestResponse(userId, apiUrl.UrlId, recId,
                    req.UserTxnId, req.CustomerNo, builtUrl, responseText);
            }
            catch { /* non-critical */ }

            // 9. Parse response
            if (httpError)
            {
                _repo.UpdateRechargeFailed(recId, "HTTP error - " + responseText);
                _repo.InsertRefundLedger(recId, userId, req.UserTxnId, opBal, req.Amount, addedById);
                return Fail("API connection failed. Amount refunded to wallet.", recId, req.UserTxnId);
            }

            var parsed = ParseApiResponse(responseText);

            // 10. Update Recharge based on parsed result
            if (parsed.Status == "Success")
            {
                _repo.UpdateRechargeSuccess(recId, parsed.ApiTxnId, parsed.Message, 0, 0);

                // Commission credit (if configured - 0 for now, extend with PackageComm lookup)
                decimal comm = GetUserCommission(userId, req.OperatorId, req.Amount);
                if (comm > 0)
                    _repo.InsertCommissionLedger(recId, userId, req.UserTxnId, opBal, comm, addedById);

                return new RechargeResult
                {
                    Success   = true,
                    Status    = "Success",
                    Message   = "Recharge successful. " + parsed.Message,
                    ApiTxnId  = parsed.ApiTxnId,
                    RecId     = recId,
                    UserTxnId = req.UserTxnId
                };
            }
            else if (parsed.Status == "Pending")
            {
                _repo.UpdateRechargeSuccess(recId, parsed.ApiTxnId, parsed.Message, 0, 0);
                // Keep debit; status remains processing
                return new RechargeResult
                {
                    Success   = false,
                    IsPending = true,
                    Status    = "Pending",
                    Message   = "Recharge is pending. " + parsed.Message,
                    ApiTxnId  = parsed.ApiTxnId,
                    RecId     = recId,
                    UserTxnId = req.UserTxnId
                };
            }
            else
            {
                _repo.UpdateRechargeFailed(recId, parsed.Message);
                _repo.InsertRefundLedger(recId, userId, req.UserTxnId, opBal, req.Amount, addedById);
                return Fail("Recharge failed: " + parsed.Message + ". Amount refunded.", recId, req.UserTxnId);
            }
        }

        // ── Placeholder replacement ──────────────────────────────────────────

        private static string ReplacePlaceholders(string template, PlaceholderContext ctx)
        {
            if (string.IsNullOrEmpty(template)) return template;

            string randomKey = new Random().Next(100000, 999999).ToString();
            string dateTime  = DateTime.Now.ToString("yyyyMMddHHmmss");

            return template
                .Replace("[UUU]",   ctx.ApiUserId   ?? "")
                .Replace("[PPP]",   ctx.ApiPassword  ?? "")
                .Replace("[RRR]",   ctx.Remark       ?? "")
                .Replace("[MMM]",   ctx.CustomerNo   ?? "")
                .Replace("[HHH]",   ctx.CustomerNo   ?? "")   // account (BillPay)
                .Replace("[NNN]",   "")
                .Replace("[VVV]",   ctx.UserTxnId    ?? "")
                .Replace("[OOO]",   ctx.OpCode       ?? "")
                .Replace("[CCC]",   ctx.CircleCode   ?? "")
                .Replace("[AAA]",   ctx.Amount.ToString("0.##"))
                .Replace("[TTT]",   ctx.UserTxnId    ?? "")
                .Replace("[EEE]",   ctx.ExtraUrl     ?? "")
                .Replace("[DDD]",   ctx.ExtraData    ?? "")
                .Replace("[FFF1]",  "")
                .Replace("[FFF2]",  "")
                .Replace("[FFF3]",  "")
                .Replace("[FFF4]",  "")
                .Replace("[FFFT]",  dateTime)
                .Replace("[FFFR]",  randomKey)
                .Replace("[COMM1]", "");
        }

        // ── HTTP call ────────────────────────────────────────────────────────

        private static string MakeHttpCall(string url, string method, string postData, string resType)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 30000;
            request.UserAgent = "VRecharge/1.0";

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(postData))
            {
                request.Method      = "POST";
                request.ContentType = resType?.Contains("json") == true
                    ? "application/json"
                    : "application/x-www-form-urlencoded";

                byte[] bytes = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                request.Method = "GET";
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader   = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        // ── Response parser ──────────────────────────────────────────────────
        // Simple keyword-based parser. TagValue table rules can replace this later.

        private static ParsedResponse ParseApiResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ParsedResponse { Status = "Failed", Message = "Empty response from API" };

            string lower = raw.ToLower();

            // Extract API txn ID (look for common JSON/text patterns)
            string apiTxnId = ExtractApiTxnId(raw);

            if (lower.Contains("success") || lower.Contains("successful") || lower.Contains("\"status\":1"))
                return new ParsedResponse { Status = "Success", Message = raw.Length > 200 ? raw.Substring(0, 200) : raw, ApiTxnId = apiTxnId };

            if (lower.Contains("pending") || lower.Contains("process") || lower.Contains("queue"))
                return new ParsedResponse { Status = "Pending", Message = raw.Length > 200 ? raw.Substring(0, 200) : raw, ApiTxnId = apiTxnId };

            return new ParsedResponse { Status = "Failed", Message = raw.Length > 200 ? raw.Substring(0, 200) : raw };
        }

        private static string ExtractApiTxnId(string raw)
        {
            // Try common JSON keys: txnid, txn_id, operatorId, refId
            var patterns = new[] { "\"txnid\"\\s*:\\s*\"([^\"]+)\"", "\"txn_id\"\\s*:\\s*\"([^\"]+)\"",
                                   "\"operatorid\"\\s*:\\s*\"([^\"]+)\"", "txnid=([^&\\s]+)" };
            foreach (var p in patterns)
            {
                var m = Regex.Match(raw, p, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
            return "";
        }

        // ── Commission lookup (extend with real PackageComm logic) ───────────

        private decimal GetUserCommission(int userId, int opId, decimal amount)
        {
            // Placeholder — wire to PackageComm table when needed
            return 0m;
        }

        private static RechargeResult Fail(string msg, long recId = 0, string userTxnId = null)
            => new RechargeResult { Success=false, Status="Failed", Message=msg, RecId=recId, UserTxnId=userTxnId };

        private class PlaceholderContext
        {
            public string ApiUserId   { get; set; }
            public string ApiPassword { get; set; }
            public string Remark      { get; set; }
            public string CustomerNo  { get; set; }
            public decimal Amount     { get; set; }
            public string OpCode      { get; set; }
            public string CircleCode  { get; set; }
            public string UserTxnId   { get; set; }
            public string ExtraUrl    { get; set; }
            public string ExtraData   { get; set; }
        }

        private class ParsedResponse
        {
            public string Status   { get; set; }
            public string Message  { get; set; }
            public string ApiTxnId { get; set; }
        }
    }
}
