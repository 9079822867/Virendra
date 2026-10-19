using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Controllers
{
    /// <summary>
    /// Vendor → us recharge status callback.
    /// URL is generated per vendor in VendorController: ~/service/reccallback/{ApiSource.CallbackId}
    /// </summary>
    public class CallbackController : ApiController
    {
        private const int RC_HOLD = 5;

        // Vendor field names differ — first non-empty match wins (case-insensitive)
        private static readonly string[] RefKeys =
        {
            "reqid", "req_id", "requestid", "request_id", "clientid", "client_id", "clientrefid",
            "client_ref_id", "agentid", "agent_id", "refid", "ref_id", "reference", "yourref",
            "usertxnid", "user_txn_id", "merchantrefno", "uniqueid", "orderid", "order_id"
        };
        private static readonly string[] ApiTxnKeys =
        {
            "txnid", "txn_id", "transactionid", "transaction_id", "apitxnid", "api_txn_id", "id"
        };
        private static readonly string[] StatusKeys =
        {
            "status", "rstatus", "txnstatus", "txn_status", "recharge_status", "statustext", "state", "result"
        };
        private static readonly string[] OperatorRefKeys =
        {
            "opid", "operatorid", "operator_id", "operatorref", "operator_ref", "operator_reference",
            "optxnid", "opt_txn_id", "oprid", "opr_id", "liveid", "live_id", "operatortxnid", "oprefno", "utr"
        };
        private static readonly string[] MessageKeys =
        {
            "message", "msg", "statusmsg", "status_msg", "remark", "remarks", "description"
        };

        private readonly IRechargeRepository _rcRepo;

        public CallbackController()
        {
            _rcRepo = new RechargeRepository();
        }

        [HttpGet, HttpPost]
        [Route("~/Service/RecCallback/{eApiID}")]
        public async Task<HttpResponseMessage> RecCallback(string eApiID = "")
        {
            var log = new StringBuilder();
            string body = Request.Content != null ? await Request.Content.ReadAsStringAsync() : string.Empty;
            string raw  = HttpUtility.UrlDecode(Request.RequestUri.AbsoluteUri)
                        + (string.IsNullOrWhiteSpace(body) ? string.Empty : " | " + body);

            CallbackApiInfo api  = null;
            CallbackRecharge rc  = null;
            bool processed       = false;

            try
            {
                api = _rcRepo.GetCallbackApi(eApiID);
                if (api == null)
                {
                    log.Append("unknown callback id");
                    return Text("failed");
                }

                var fields   = ReadFields(Request.RequestUri.Query, body);
                string opRef = First(fields, OperatorRefKeys);
                string msg   = First(fields, MessageKeys);
                int status   = MapStatus(First(fields, StatusKeys), raw);

                // Match recharge: our ref / user txn id first, then the vendor txn id
                foreach (string r in All(fields, RefKeys))
                    if ((rc = _rcRepo.FindCallbackRecharge(api.ApiId, r, null)) != null) break;
                if (rc == null)
                    foreach (string t in All(fields, ApiTxnKeys))
                        if ((rc = _rcRepo.FindCallbackRecharge(api.ApiId, null, t)) != null) break;

                if (rc == null)
                {
                    log.Append("recharge not found");
                    return Text("failed");
                }

                string apiTxnId = First(fields, ApiTxnKeys);
                log.AppendFormat("rec={0}, rcStatus={1}, cbStatus={2}", rc.RecId, rc.StatusId, status);

                if (!CanApply(rc.StatusId, status, log))
                    return Text("success");   // acknowledged, nothing to change

                string statusMsg = Truncate(string.IsNullOrWhiteSpace(msg) ? "CallBack" : msg, 200);
                _rcRepo.UpdateRechargeStatus(rc.RecId, 0, status,
                    apiTxnId, opRef, statusMsg, "CallBack",
                    out bool isDownline, out bool isRefund, out string spLog,
                    rc.OpId ?? 0);

                processed = true;
                log.AppendFormat(", refund={0}, sp=[{1}]", isRefund, spLog);

                QueueUserCallback(rc, status, opRef ?? rc.OptTxnId, statusMsg);
                return Text("success");
            }
            catch (Exception ex)
            {
                log.Append(" | error: ").Append(ex.GetBaseException().Message);
                return Text("failed");
            }
            finally
            {
                try
                {
                    _rcRepo.InsertReqRes(rc?.UserId ?? 0, api?.UrlId ?? 0, rc?.RecId ?? 0,
                        rc?.OurRefTxnId ?? eApiID, processed ? "CallBack" : "CallBack-Ignored",
                        Truncate(raw, 4000), Truncate(log.ToString(), 4000),
                        rc?.CustomerNo, rc?.UserTxnId);
                }
                catch { /* logging is best-effort */ }
            }
        }

        // ── Status rules ─────────────────────────────────────────────────────
        // usp_UpdateRechargeStatus (StatusWithCheck) handles refund / re-debit itself;
        // here we only decide which vendor transitions we trust.
        private static bool CanApply(int current, int incoming, StringBuilder log)
        {
            if (incoming != RechargeStatusCodes.SUCCESS && incoming != RechargeStatusCodes.FAILED)
            {
                log.Append(", still pending — no change");
                return false;
            }
            if (current == RC_HOLD)
            {
                log.Append(", recharge on hold — no change");
                return false;
            }
            if (current == incoming)
            {
                log.Append(", same status — no change");
                return false;
            }
            if (current == RechargeStatusCodes.FAILED)
            {
                // Would debit the user again after they were refunded — leave to admin
                log.Append(", already failed/refunded — manual review");
                return false;
            }
            return true;   // processing → success/failed, success → failed (vendor reversal)
        }

        private static int MapStatus(string value, string raw)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();

            if (v == "success" || v == "successful" || v == "succeeded" || v == "completed" || v == "ok")
                return RechargeStatusCodes.SUCCESS;
            if (v == "failed" || v == "failure" || v == "fail" || v == "failled" || v == "error" ||
                v == "rejected" || v == "reversed" || v == "refund" || v == "refunded")
                return RechargeStatusCodes.FAILED;
            if (v.Length > 0)
            {
                if (v.Contains("success")) return RechargeStatusCodes.SUCCESS;
                if (v.Contains("fail") || v.Contains("revers") || v.Contains("refund"))
                    return RechargeStatusCodes.FAILED;
                return RechargeStatusCodes.PROCESS;   // unknown value → never guess a final status
            }

            // No status field at all → keyword scan, defaulting to pending
            string lower = (raw ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("success")) return RechargeStatusCodes.SUCCESS;
            if (lower.Contains("fail"))    return RechargeStatusCodes.FAILED;
            return RechargeStatusCodes.PROCESS;
        }

        // ── Forward to the API user's callback URL (docs: ApiDocument/FilesCallbackInfo) ──
        private static void QueueUserCallback(CallbackRecharge rc, int status, string opRef, string message)
        {
            if (string.IsNullOrWhiteSpace(rc.UserCallbackUrl) ||
                !Uri.TryCreate(rc.UserCallbackUrl.Trim(), UriKind.Absolute, out Uri target) ||
                (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
                return;

            string now  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string json = JsonConvert.SerializeObject(new
            {
                status             = status,
                txn_id             = rc.UserTxnId,
                api_txn_id         = rc.RecId,
                message            = message,
                operator_reference = opRef ?? string.Empty,
                callback_timestamp = now,
                data = new
                {
                    phone_number    = rc.CustomerNo,
                    amount          = rc.Amount,
                    @operator       = rc.OperatorName,
                    completion_date = now
                }
            });

            HostingEnvironment.QueueBackgroundWorkItem(async ct =>
            {
                string result;
                try
                {
                    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    {
                        var resp = await http.PostAsync(target,
                            new StringContent(json, Encoding.UTF8, "application/json"), ct);
                        result = (int)resp.StatusCode + " " + Truncate(await resp.Content.ReadAsStringAsync(), 1000);
                    }
                }
                catch (Exception ex) { result = "error: " + ex.GetBaseException().Message; }

                try
                {
                    new RechargeRepository().InsertReqRes(rc.UserId, 0, rc.RecId, rc.OurRefTxnId,
                        "UserCallback", target + " | " + json, result, rc.CustomerNo, rc.UserTxnId);
                }
                catch { /* best-effort */ }
            });
        }

        // ── Field parsing: query string, form body or JSON body ─────────────
        private static Dictionary<string, string> ReadFields(string query, string body)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddForm(d, query);

            string b = (body ?? string.Empty).Trim();
            if (b.StartsWith("{") || b.StartsWith("["))
            {
                try { AddJson(d, JToken.Parse(b)); } catch { AddForm(d, b); }
            }
            else
            {
                AddForm(d, b);
            }
            return d;
        }

        private static void AddForm(Dictionary<string, string> d, string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            var nv = HttpUtility.ParseQueryString(s.TrimStart('?'));
            foreach (string k in nv.AllKeys)
                if (!string.IsNullOrEmpty(k) && !d.ContainsKey(k.Trim()))
                    d[k.Trim()] = nv[k];
        }

        private static void AddJson(Dictionary<string, string> d, JToken token)
        {
            // Flatten nested objects; leaf name only ("data.status" → "status"), first one wins
            foreach (var v in token.SelectTokens("$..*").OfType<JValue>())
            {
                string key = v.Parent is JProperty p ? p.Name : null;
                if (!string.IsNullOrEmpty(key) && !d.ContainsKey(key))
                    d[key] = Convert.ToString(v.Value, CultureInfo.InvariantCulture);
            }
        }

        private static string First(Dictionary<string, string> d, string[] keys)
            => All(d, keys).FirstOrDefault();

        private static IEnumerable<string> All(Dictionary<string, string> d, string[] keys)
            => keys.Select(k => d.TryGetValue(k, out string v) ? v?.Trim() : null)
                   .Where(v => !string.IsNullOrEmpty(v))
                   .Distinct();

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        private HttpResponseMessage Text(string s)
            => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "text/plain") };
    }
}
