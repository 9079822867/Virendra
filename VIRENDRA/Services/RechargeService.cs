using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    /// <summary>
    /// MVC recharge pipeline (same flow as STC RechargeProcess):
    ///   1. SP_RechargeRequestValidation  → switch type, debit amount, Api1..3
    ///   2. Routing list (common / circle / amount / operator) + user filters
    ///   3. For each route: usp_RechargeCreate (first call creates Recharge + debit,
    ///      later calls switch the same RecId) → vendor API call → FilterRespTag parse
    ///   4. usp_UpdateRechargeStatus for Success / Process / Failed
    ///      (Failed on every route → final failed update, SP refunds the debit)
    /// </summary>
    public class RechargeService
    {
        private readonly IRechargeRepository _repo;

        // MediumId for recharges coming from the MVC web panel
        private const int MEDIUM_WEB = 1;

        public RechargeService(IRechargeRepository repo)
        {
            _repo = repo;
        }

        #region Recharge Request

        public RechargeResult ProcessRecharge(RechargeRequestModel req, int userId, int addedById)
        {
            if (string.IsNullOrWhiteSpace(req.UserTxnId))
                req.UserTxnId = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            var model = new RechargeModel
            {
                UserId     = userId,
                MobileNo   = req.CustomerNo,
                Amount     = req.Amount,
                OpId       = req.OperatorId,
                CircleId   = req.CircleId,
                RefTxnId   = req.UserTxnId,
                OurTxnId   = GetUniqueNumber(),
                IpAddress  = ClientIp()
            };

            var circle = _repo.GetCircles().Find(c => c.Id == req.CircleId);
            model.CircleCode = circle?.CircleCode ?? string.Empty;

            // ── 0. Operator input rules (Switch > Operator Switch > Validation) ──
            string inputError = ValidateOperatorInput(model.OpId ?? 0, model.MobileNo, model.Amount);
            if (inputError != null)
                return Fail(inputError, 0, model.RefTxnId);

            // ── 1. Request validation SP ────────────────────────────────────
            RechargeValidationResult val;
            try
            {
                val = _repo.ValidateRechargeRequest(
                    userId, model.MobileNo, model.Amount,
                    model.OpId ?? 0, model.CircleId ?? 0, model.RefTxnId, model.IpAddress);
            }
            catch (Exception ex)
            {
                return Fail("Recharge validation failed: " + ex.GetBaseException().Message, 0, model.RefTxnId);
            }

            if (val.StatusCode == 100)
                return Fail("Operator is offline. Please try later.", 0, model.RefTxnId);
            if (val.StatusCode == 103)
                return Fail("Route is blocked for this operator.", 0, model.RefTxnId);

            // 0 = OK, 101 = default failed, 102 = default processing, 104 = callback thread
            if (val.StatusCode != 0 && val.StatusCode != 101 &&
                val.StatusCode != 102 && val.StatusCode != 104)
                return Fail("Recharge failed: " + val.StatusMsg, 0, model.RefTxnId);

            if ((model.CircleId ?? 0) == 0 && val.SerialCircleId > 0)
            {
                model.CircleId   = val.SerialCircleId;
                model.CircleCode = val.SerialCircleCode;
            }

            var helper = new RechargeHelperDto
            {
                SwitchId    = val.SwitchTypeId > 0 ? val.SwitchTypeId : RouteType.COMMON_ROUTE,
                DebitAmount = val.DebitAmount  > 0 ? val.DebitAmount  : model.Amount,
            };
            helper.CommAmount = model.Amount - helper.DebitAmount;

            // ── 2. Route list ───────────────────────────────────────────────
            try
            {
                helper.ApiRouteList = BuildRouteList(model, helper, val);
            }
            catch (Exception ex)
            {
                return Fail("Routing error: " + ex.GetBaseException().Message, 0, model.RefTxnId);
            }

            if (helper.ApiRouteList.Count == 0)
                return Fail("Route not available. Please contact admin.", 0, model.RefTxnId);

            // ── 3. Route-by-route processing ────────────────────────────────
            return RechargeProcessCall(model, helper);
        }

        private List<ApiPriorityDto> BuildRouteList(RechargeModel model, RechargeHelperDto helper,
            RechargeValidationResult val)
        {
            // Api1..3 from the validation SP are the operator-level default routes
            var spRoutes = new List<ApiPriorityDto>();
            if (val.Api1 > 0) spRoutes.Add(new ApiPriorityDto { ApiId = val.Api1, PriorityId = 1 });
            if (val.Api2 > 0) spRoutes.Add(new ApiPriorityDto { ApiId = val.Api2, PriorityId = 2 });
            if (val.Api3 > 0) spRoutes.Add(new ApiPriorityDto { ApiId = val.Api3, PriorityId = 3 });

            int opId     = model.OpId ?? 0;
            int circleId = model.CircleId ?? 0;
            List<ApiPriorityDto> routes;

            switch (helper.SwitchId)
            {
                case RouteType.COMMON_ROUTE:
                    routes = _repo.GetCommanRouting(opId, model.Amount, model.CircleCode);
                    break;
                case RouteType.CIRCLE_WISE:
                    routes = _repo.GetCircleRouting(opId, circleId, model.Amount);
                    break;
                case RouteType.AMOUNT_WISE:
                    routes = _repo.GetAmountRoutes(opId, circleId, model.Amount);
                    break;
                default: // OPERATOR_WISE
                    routes = _repo.GetOperatorRoutes(opId, model.Amount);
                    if (routes == null || routes.Count == 0) routes = spRoutes;
                    break;
            }

            var allowed = (routes ?? new List<ApiPriorityDto>())
                .Where(r => r.ApiId > 0)
                // R-Offer routes need an R-Offer check, which this panel does not do
                .Where(r => r.MinRO <= 0)
                .Where(r => UserAllowed(model.UserId, r.UserFilter, r.BlockUser))
                .Where(r => CircleAllowed(model.CircleCode, r.CircleFilter))
                .OrderBy(r => r.PriorityId)
                .ThenBy(r => r.FTypeId);

            // Same API + MinRO only once, priorities renumbered 1..n
            var final = new List<ApiPriorityDto>();
            int p = 1;
            foreach (var r in allowed)
            {
                if (final.Any(x => x.ApiId == r.ApiId && x.MinRO == r.MinRO)) continue;
                r.PriorityId = p++;
                final.Add(r);
            }
            return final;
        }

        #endregion

        #region Recharge Process Call

        private RechargeResult RechargeProcessCall(RechargeModel model, RechargeHelperDto helper)
        {
            bool   anyRefund  = false;
            string lastMsg    = string.Empty;

            foreach (var route in helper.ApiRouteList)
            {
                helper.CurrentApiId      = route.ApiId;
                helper.CurrentPriorityId = route.PriorityId;
                helper.RouteId           = route.RouteId;
                helper.RouteOP1          = route.RouteOP1;
                helper.CircleFilter      = route.CircleFilter;

                // First call (RecId = 0) creates Recharge + TxnLedger debit.
                // Later calls switch the same RecId to this API (no extra debit).
                CreateRechargeResult cr;
                try
                {
                    cr = _repo.CreateRecharge(
                        model.UserId, model.MobileNo, model.Amount, helper.DebitAmount,
                        model.OpId ?? 0, model.CircleId ?? 0, route.ApiId,
                        model.RefTxnId, model.OurTxnId, model.IpAddress,
                        helper.SwitchId, helper.RecId, MEDIUM_WEB, route.CircleFilter);
                }
                catch (Exception ex)
                {
                    if (helper.RecId == 0)
                        return Fail("Error creating recharge: " + ex.GetBaseException().Message, 0, model.RefTxnId);

                    // Debit already done — leave it processing for a status check
                    return Pending(helper, model, string.Empty, "Recharge is processing.");
                }

                if (cr.RecId > 0) { helper.RecId = cr.RecId; helper.TxnId = cr.TxnId; }
                helper.ApiTypeId = cr.ApiTypeId;

                // 9 = insufficient balance
                if (cr.StatusCode == 9)
                    return Fail(cr.StatusMsg, helper.RecId, model.RefTxnId);

                // 100 = default offline
                if (cr.StatusCode == 100)
                {
                    lastMsg = "Operator is offline.";
                    break;
                }

                // 101 = default failed → no switching
                if (cr.StatusCode == 101)
                {
                    lastMsg = "Recharge failed (default failed).";
                    break;
                }

                // 102 = default processing → keep debit, status stays Process
                if (cr.StatusCode == 102)
                    return Pending(helper, model, string.Empty, "Recharge is processing.");

                // 10/11/14/23/24 = route-level errors → try next API
                if (cr.StatusCode == 10 || cr.StatusCode == 11 || cr.StatusCode == 14 ||
                    cr.StatusCode == 23 || cr.StatusCode == 24)
                {
                    lastMsg = cr.StatusMsg;
                    continue;
                }

                if (cr.StatusCode != 0)
                {
                    lastMsg = "Recharge failed: " + cr.StatusMsg;
                    break;
                }

                if (string.IsNullOrEmpty(cr.OpCode) && string.IsNullOrEmpty(route.RouteOP1))
                {
                    lastMsg = "Operator code not set for API " + route.ApiId;
                    MarkRouteFailed(helper, model, lastMsg, ref anyRefund);
                    continue;
                }
                if (string.IsNullOrEmpty(cr.ApiUrl))
                {
                    lastMsg = "API URL not set for API " + route.ApiId;
                    MarkRouteFailed(helper, model, lastMsg, ref anyRefund);
                    continue;
                }

                // ── Vendor API call ─────────────────────────────────────────
                string opCode   = !string.IsNullOrEmpty(route.RouteOP1) ? route.RouteOP1 : cr.OpCode;
                string url      = ReplaceUrl(cr.ApiUrl,   model, cr, opCode);
                string postData = ReplaceUrl(cr.PostData, model, cr, opCode);

                string apiRes;
                try { apiRes = MakeHttpCall(url, cr.Method, postData, cr.ContentType); }
                catch (Exception ex) { apiRes = string.Empty; lastMsg = "API error: " + ex.Message; }

                try { _repo.AddUpdateReqRes(helper.RecId, url + (string.IsNullOrEmpty(postData) ? "" : " | " + postData), apiRes, route.ApiId); }
                catch { /* non-critical */ }

                // ── Handle response ─────────────────────────────────────────
                var fResp = FilterRespTagValue(route.ApiId, apiRes);
                string statusMsg = "p" + helper.CurrentPriorityId + "-v" + helper.CurrentApiId + "-" + fResp.Message;

                if (fResp.StatusId == RechargeStatusCodes.FAILED)
                {
                    lastMsg = fResp.Message;
                    UpdateStatus(helper, model, RechargeStatusCodes.FAILED, fResp.ApiTxnId,
                        statusMsg, "Response", ref anyRefund);

                    // Switch > Stop Route Message: this failure must not be retried on another vendor
                    if (IsStopRouteMessage(apiRes, route.ApiId, model.OpId ?? 0))
                        break;

                    continue; // switch to next API
                }

                if (fResp.StatusId == RechargeStatusCodes.SUCCESS)
                {
                    UpdateStatus(helper, model, RechargeStatusCodes.SUCCESS, fResp.ApiTxnId,
                        statusMsg, "StatusWithCheck", ref anyRefund);

                    return new RechargeResult
                    {
                        Success   = true,
                        Status    = "Success",
                        Message   = "Recharge successful. " + fResp.Message,
                        ApiTxnId  = fResp.ApiTxnId,
                        RecId     = helper.RecId,
                        UserTxnId = model.RefTxnId
                    };
                }

                // Process (or response not matched by any tag) → keep debit
                UpdateStatus(helper, model, RechargeStatusCodes.PROCESS, fResp.ApiTxnId,
                    statusMsg, "StatusWithCheck", ref anyRefund);
                return Pending(helper, model, fResp.ApiTxnId, "Recharge is processing. " + fResp.Message);
            }

            // ── All APIs failed / stopped → final Failed + refund ───────────
            if (helper.RecId > 0)
            {
                UpdateStatus(helper, model, RechargeStatusCodes.FAILED, string.Empty,
                    string.IsNullOrEmpty(lastMsg) ? "All routes failed" : lastMsg, "Auto", ref anyRefund);

                // Safety net: credit the debit back if the SP did not refund
                if (!anyRefund)
                {
                    try { _repo.InsertRefundLedger(helper.RecId, model.UserId, model.RefTxnId, 0, helper.DebitAmount, model.UserId); }
                    catch { /* non-critical */ }
                }

                return Fail("Recharge failed. " + lastMsg + " Amount refunded to wallet.", helper.RecId, model.RefTxnId);
            }

            return Fail("Recharge failed. " + lastMsg, 0, model.RefTxnId);
        }

        // Shared with MobileController (API recharges)
        internal static string ValidateOperatorInput(int opId, string number, decimal amount)
        {
            try { return new SwitchRepository().ValidateRechargeInput(opId, number, amount); }
            catch { return null; }   // never block a recharge because the rule lookup failed
        }

        internal static bool IsStopRouteMessage(string response, int apiId, int opId)
        {
            try { return new SwitchRepository().IsStopRouteMessage(response, apiId, opId); }
            catch { return false; }
        }

        private void MarkRouteFailed(RechargeHelperDto helper, RechargeModel model, string msg, ref bool anyRefund)
        {
            if (helper.RecId > 0)
                UpdateStatus(helper, model, RechargeStatusCodes.FAILED, string.Empty,
                    "p" + helper.CurrentPriorityId + "-v" + helper.CurrentApiId + "-" + msg,
                    "Response", ref anyRefund);
        }

        private void UpdateStatus(RechargeHelperDto helper, RechargeModel model, int statusId,
            string apiTxnId, string statusMsg, string updateType, ref bool anyRefund)
        {
            if (helper.RecId <= 0) return;
            try
            {
                _repo.UpdateRechargeStatus(helper.RecId, model.UserId, statusId,
                    apiTxnId, string.Empty, statusMsg, "Auto",
                    out bool isDownline, out bool isRefund, out string spLog,
                    model.OpId ?? 0, 0, updateType);

                anyRefund |= isRefund;
            }
            catch { /* status can be corrected by status check */ }
        }

        #endregion

        #region Response Filter (FilterRespTag)

        // Tag StatusId uses RechargeStatusCodes. When the vendor has tags but none match,
        // the recharge stays Process (same as STC). Keyword fallback only when no tags exist.
        private FilterResponseModel FilterRespTagValue(int apiId, string apiRes)
        {
            var res = new FilterResponseModel { StatusId = RechargeStatusCodes.PROCESS };

            if (string.IsNullOrWhiteSpace(apiRes))
            {
                res.Message = "No response from API";
                return res;
            }

            res.Message  = apiRes.Length > 200 ? apiRes.Substring(0, 200) : apiRes;
            res.ApiTxnId = ExtractApiTxnId(apiRes);

            List<FilterTag> tags = null;
            try { tags = _repo.GetFilterTags(apiId); }
            catch { /* keyword fallback */ }

            if (tags != null && tags.Count > 0)
            {
                foreach (var t in tags.OrderBy(x => x.Id))
                {
                    if (string.IsNullOrEmpty(t.TagValue) ||
                        apiRes.IndexOf(t.TagValue, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (t.StatusId == RechargeStatusCodes.SUCCESS ||
                        t.StatusId == RechargeStatusCodes.FAILED  ||
                        t.StatusId == RechargeStatusCodes.PROCESS)
                    {
                        res.StatusId = t.StatusId;
                        break;
                    }
                }
                return res;
            }

            string lower = apiRes.ToLowerInvariant();
            if (lower.Contains("fail") || lower.Contains("unsuccess") || lower.Contains("reject"))
                res.StatusId = RechargeStatusCodes.FAILED;
            else if (lower.Contains("success"))
                res.StatusId = RechargeStatusCodes.SUCCESS;

            return res;
        }

        private static string ExtractApiTxnId(string raw)
        {
            var patterns = new[] { "\"txnid\"\\s*:\\s*\"([^\"]+)\"", "\"txn_id\"\\s*:\\s*\"([^\"]+)\"",
                                   "\"operatorid\"\\s*:\\s*\"([^\"]+)\"", "\"refid\"\\s*:\\s*\"([^\"]+)\"",
                                   "txnid=([^&\\s]+)" };
            foreach (var p in patterns)
            {
                var m = Regex.Match(raw, p, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
            return string.Empty;
        }

        #endregion

        #region Helpers

        private static string ReplaceUrl(string template, RechargeModel model, CreateRechargeResult cr, string opCode)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            string circleCode = !string.IsNullOrEmpty(cr.CircleCode) ? cr.CircleCode : model.CircleCode;
            string randomKey  = new Random().Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
            string dateTime   = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            return template
                .Replace("[UUU]",   cr.ApiUserId    ?? "")
                .Replace("[PPP]",   cr.ApiPassword  ?? "")
                .Replace("[RRR]",   cr.ApiOptional  ?? "")
                .Replace("[MMM]",   model.MobileNo  ?? "")
                .Replace("[HHH]",   model.MobileNo  ?? "")   // account (BillPay)
                .Replace("[NNN]",   "")
                .Replace("[VVV]",   model.OurTxnId  ?? "")
                .Replace("[TTT]",   model.OurTxnId  ?? "")
                .Replace("[OOO]",   opCode          ?? "")
                .Replace("[CCC]",   circleCode      ?? "")
                .Replace("[AAA]",   model.Amount.ToString("0.##", CultureInfo.InvariantCulture))
                .Replace("[EEE]",   cr.ExtraUrl     ?? "")
                .Replace("[DDD]",   cr.ExtraUrlData ?? "")
                .Replace("[FFF1]",  "")
                .Replace("[FFF2]",  "")
                .Replace("[FFF3]",  "")
                .Replace("[FFF4]",  "")
                .Replace("[FFFT]",  dateTime)
                .Replace("[FFFR]",  randomKey)
                .Replace("[COMM1]", cr.Comm1.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static string MakeHttpCall(string url, string method, string postData, string contentType)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout   = 30000;
            request.UserAgent = "VRecharge/1.0";

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(postData))
            {
                request.Method      = "POST";
                request.ContentType = !string.IsNullOrWhiteSpace(contentType)
                    ? contentType
                    : (postData.TrimStart().StartsWith("{") ? "application/json" : "application/x-www-form-urlencoded");

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

        private static bool UserAllowed(int userId, string userFilter, string blockUser)
        {
            string uid = userId.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(blockUser) &&
                blockUser.Replace(" ", "").Split(',').Any(b => b == uid))
                return false;

            if (string.IsNullOrWhiteSpace(userFilter) ||
                userFilter.ToLower().Contains("all"))
                return true;

            return userFilter.Replace(" ", "").Split(',').Any(u => u == uid);
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

        private static string GetUniqueNumber()
            => "E" + DateTime.Now.ToString("yyMMddHHmmss") +
               Guid.NewGuid().ToString("N").ToUpper().Substring(10, 6);

        private static string ClientIp()
        {
            var req = System.Web.HttpContext.Current?.Request;
            if (req == null) return string.Empty;
            string fwd = req.ServerVariables["HTTP_X_FORWARDED_FOR"];
            return !string.IsNullOrWhiteSpace(fwd) ? fwd.Split(',')[0].Trim() : req.UserHostAddress ?? string.Empty;
        }

        private static RechargeResult Pending(RechargeHelperDto helper, RechargeModel model, string apiTxnId, string msg)
            => new RechargeResult
            {
                Success   = false,
                IsPending = true,
                Status    = "Pending",
                Message   = msg,
                ApiTxnId  = apiTxnId,
                RecId     = helper.RecId,
                UserTxnId = model.RefTxnId
            };

        private static RechargeResult Fail(string msg, long recId = 0, string userTxnId = null)
            => new RechargeResult { Success=false, Status="Failed", Message=msg, RecId=recId, UserTxnId=userTxnId };

        private class FilterResponseModel
        {
            public int    StatusId { get; set; }
            public string Message  { get; set; }
            public string ApiTxnId { get; set; }
        }

        #endregion
    }
}
