using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    /// <summary>
    /// Shared formats for the v3 reseller API (/transaction/…, /user/api/…, /report/api/…)
    /// and its webhooks. Documented on the API Document pages.
    /// </summary>
    public static class ApiV3
    {
        // Keep nulls and exact field names (system_id, req_time, _id…), unlike the global formatter
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting        = Formatting.None
        };

        public static readonly string[] ReasonCodes =
            { "NOT_CREDITED", "DUPLICATE_DEBIT", "WRONG_AMOUNT", "SERVICE_NOT_WORKING", "OTHER" };

        /// <summary>DB times are local (IST); the API speaks ISO 8601 UTC.</summary>
        public static string Iso(DateTime local) =>
            DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        public static string Iso(DateTime? local) => local.HasValue ? Iso(local.Value) : null;

        public static string NowIso() => Iso(DateTime.Now);

        // Recharge.StatusId: 1 success, 2/4 failed or refunded, anything else still processing
        public static string RechargeStatus(int statusId) =>
            statusId == RechargeStatusCodes.SUCCESS ? "Success"
          : statusId == RechargeStatusCodes.FAILED || statusId == 4 ? "Failed"
          : "Processing";

        // Complaint.StatusId (ComplaintStatusCodes): 5 pending, 2 processing, 8 resolved
        public static string ComplaintStatus(int statusId) =>
            statusId == ComplaintStatusCodes.PENDING    ? "OPEN"
          : statusId == ComplaintStatusCodes.PROCESSING ? "IN_PROGRESS"
          : statusId == ComplaintStatusCodes.RESOLVED   ? "RESOLVED"
          : "CANCELLED";

        public static byte? ComplaintStatusId(string name)
        {
            switch ((name ?? "").Trim().ToUpperInvariant())
            {
                case "OPEN":        return ComplaintStatusCodes.PENDING;
                case "IN_PROGRESS": return ComplaintStatusCodes.PROCESSING;
                case "RESOLVED":    return ComplaintStatusCodes.RESOLVED;
                default:            return null;
            }
        }

        // ── Ticket ids: TKT-yyyyMMdd-0042 (date of the complaint + Complaint.Id) ──
        public static string Ticket(long complaintId, DateTime complaintDate) =>
            "TKT-" + complaintDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" +
            complaintId.ToString("0000", CultureInfo.InvariantCulture);

        /// <summary>Accepts a ticket id (TKT-20260101-0042) or the plain complaint id.</summary>
        public static long? ParseTicket(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            string last = id.Trim().Split('-').Last();
            return long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out long n) && n > 0 ? n : (long?)null;
        }

        // ── Complaint text: "[REASON] subject — description (asked ₹199)" ──────
        // Readable for admins in Complaint Report, and parseable back for the API.
        public static string ComplaintRemark(string reasonCode, string subject, string description, decimal? askedAmount)
        {
            string s = "[" + reasonCode + "] " + (string.IsNullOrWhiteSpace(subject) ? "Recharge complaint" : subject.Trim());
            if (!string.IsNullOrWhiteSpace(description)) s += " — " + description.Trim();
            if (askedAmount > 0) s += " (asked ₹" + askedAmount.Value.ToString("0.##", CultureInfo.InvariantCulture) + ")";
            return s.Length > 500 ? s.Substring(0, 500) : s;
        }

        private static readonly Regex RemarkRe = new Regex(
            @"^\[(?<r>[A-Z_]+)\]\s*(?<s>.*?)(?:\s—\s(?<d>.*?))?(?:\s\(asked ₹(?<a>[\d.]+)\))?$",
            RegexOptions.Singleline);

        public static (string Reason, string Subject, string Description, decimal? Asked) ParseRemark(string remark)
        {
            var m = RemarkRe.Match(remark ?? "");
            if (!m.Success) return ("OTHER", remark, null, null);
            decimal.TryParse(m.Groups["a"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal a);
            return (m.Groups["r"].Value,
                    m.Groups["s"].Value,
                    m.Groups["d"].Success && m.Groups["d"].Length > 0 ? m.Groups["d"].Value : null,
                    a > 0 ? a : (decimal?)null);
        }

        // ── Webhooks: GET <user url>?param=value…, logged to RequestResponse ──
        public static void SendWebhook(string url, IDictionary<string, string> query,
            int userId, long recId, string logRef, string logRemark, string customerNo, string userTxnId)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                return;

            string qs = string.Join("&", query.Where(kv => kv.Value != null)
                .Select(kv => HttpUtility.UrlEncode(kv.Key) + "=" + HttpUtility.UrlEncode(kv.Value)));
            string target = baseUri.AbsoluteUri + (string.IsNullOrEmpty(baseUri.Query) ? "?" : "&") + qs;

            HostingEnvironment.QueueBackgroundWorkItem(async ct =>
            {
                string result;
                try
                {
                    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    {
                        var resp = await http.GetAsync(target, ct);
                        string body = await resp.Content.ReadAsStringAsync();
                        result = (int)resp.StatusCode + " " + (body.Length > 1000 ? body.Substring(0, 1000) : body);
                    }
                }
                catch (Exception ex) { result = "error: " + ex.GetBaseException().Message; }

                try { new RechargeRepository().InsertReqRes(userId, 0, recId, logRef, logRemark, target, result, customerNo, userTxnId); }
                catch { /* best-effort log */ }
            });
        }
    }
}
