using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using VIRENDRA.Data;
using VIRENDRA.Models;

namespace VIRENDRA.Services
{
    /// <summary>
    /// Recharge complaints (disputes), shared by the web panel and the mobile app API.
    /// Book: only on a successful recharge, one complaint per recharge, forwarded to the
    /// vendor's ComplainRequest URL. Resolve (admin): resolve / keep processing, optional refund.
    /// </summary>
    public class ComplaintService
    {
        private readonly IComplaintRepository _repo;
        private readonly IRechargeRepository  _rechargeRepo;

        public ComplaintService(IComplaintRepository repo, IRechargeRepository rechargeRepo)
        {
            _repo         = repo;
            _rechargeRepo = rechargeRepo;
        }

        /// <param name="ownerUserId">Recharge owner to restrict to; null for admin (any user's recharge).</param>
        public ComplaintBookResult Book(string recRef, string remark, int complaintById, int? ownerUserId)
        {
            var rec = _repo.FindRecharge((recRef ?? string.Empty).Trim(), ownerUserId);
            if (rec == null)
                return new ComplaintBookResult { Status = ComplaintBookStatus.NotFound };

            // Same rule as STC: complaints only for successful recharges
            if (rec.StatusId != RechargeStatusCodes.SUCCESS)
                return new ComplaintBookResult { Status = ComplaintBookStatus.NotSuccess };

            if (_repo.ComplaintExists(rec.Id))
                return new ComplaintBookResult { Status = ComplaintBookStatus.Duplicate };

            remark = string.IsNullOrWhiteSpace(remark) ? "Recharge complaint" : remark.Trim();
            if (remark.Length > 500) remark = remark.Substring(0, 500);

            long complaintId = _repo.InsertComplaint(rec.Id, complaintById, remark, ComplaintStatusCodes.PENDING);

            // Forward to the vendor; a vendor error never fails the complaint
            if (rec.ApiId.HasValue)
            {
                try
                {
                    string apiRes = SendVendorComplaint(rec, remark);
                    if (apiRes != null)
                        _repo.UpdateApiResponse(complaintId, Trim500(apiRes));
                }
                catch (Exception ex)
                {
                    try { _repo.UpdateApiResponse(complaintId, Trim500("VENDOR_ERROR: " + ex.Message)); }
                    catch { /* non-critical */ }
                }
            }

            return new ComplaintBookResult { Status = ComplaintBookStatus.Booked, ComplaintId = complaintId };
        }

        /// <summary>
        /// Admin action. isResolved → Resolved, else Processing. refund → recharge set to Failed through
        /// usp_UpdateRechargeStatus (the SP credits the wallet). Returns a message for the UI.
        /// </summary>
        public (bool Ok, string Message) Resolve(long complaintId, bool isResolved, bool refund,
            string comment, int adminId, string adminName)
        {
            var cp = _repo.GetComplaint(complaintId);
            if (cp == null)
                return (false, "Complaint not found.");

            if (cp.StatusId != ComplaintStatusCodes.PENDING && cp.StatusId != ComplaintStatusCodes.PROCESSING)
                return (false, "Complaint is already resolved and can't be updated.");

            comment = (comment ?? string.Empty).Trim();
            bool refunded = false;

            if (refund)
            {
                if (cp.RechargeStatus != RechargeStatusCodes.SUCCESS)
                    return (false, "Refund is allowed only while the recharge is Success.");

                _rechargeRepo.UpdateRechargeStatus(cp.RecId, 0, RechargeStatusCodes.FAILED,
                    null, null, "Refund on complaint #" + complaintId + " by " + adminName, "Refund Complaint",
                    out bool isDownline, out bool isRefund, out string spLog);

                refunded = isRefund;
            }

            byte newStatus = isResolved ? ComplaintStatusCodes.RESOLVED : ComplaintStatusCodes.PROCESSING;
            _repo.ResolveComplaint(complaintId, newStatus,
                comment.Length > 1000 ? comment.Substring(0, 1000) : comment, refunded, adminId);

            string msg = "Complaint #" + complaintId + " marked " + ComplaintStatusCodes.Name(newStatus) + ".";
            if (refund)
                msg += refunded
                    ? " Recharge set to Failed and ₹" + cp.Amount.ToString("0.00", CultureInfo.InvariantCulture) + " refunded."
                    : " Recharge set to Failed, but the SP did not report a refund — check the user's ledger.";
            return (true, msg);
        }

        // ── Vendor forwarding ───────────────────────────────────────────────

        private string SendVendorComplaint(ComplaintRecharge rec, string remark)
        {
            var api = _repo.GetComplainApiUrl(rec.ApiId.Value);
            if (api == null) return null;

            string ourRef = rec.OurRefTxnId ?? rec.UserTxnId ?? rec.Id.ToString(CultureInfo.InvariantCulture);
            bool   isPost = string.Equals(api.Method, "POST", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(api.PostData);

            string url = Fill(api.Url, api, rec, ourRef, isPost ? remark : HttpUtility.UrlEncode(remark));

            var http = (HttpWebRequest)WebRequest.Create(url);
            http.Timeout   = 30000;
            http.UserAgent = "VRecharge/1.0";

            if (isPost)
            {
                string body  = Fill(api.PostData, api, rec, ourRef, remark);
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                http.Method        = "POST";
                http.ContentType   = body.TrimStart().StartsWith("{") ? "application/json" : "application/x-www-form-urlencoded";
                http.ContentLength = bytes.Length;
                using (var s = http.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            }
            else
            {
                http.Method = "GET";
            }

            using (var resp = (HttpWebResponse)http.GetResponse())
            using (var rdr  = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return rdr.ReadToEnd();
        }

        private static string Fill(string template, ComplaintApiUrl api, ComplaintRecharge rec,
            string ourRef, string remark)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            return template
                .Replace("[UUU]",         api.ApiUserId   ?? "")
                .Replace("[PPP]",         api.ApiPassword ?? "")
                .Replace("[RRR]",         api.Remark      ?? "")
                .Replace("[MMM]",         rec.CustomerNo  ?? "")
                .Replace("[TTT]",         ourRef)
                .Replace("[VVV]",         ourRef)
                .Replace("[OPTXN]",       rec.ApiTxnId    ?? "")
                .Replace("[Description]", remark          ?? "");
        }

        private static string Trim500(string s) => s.Length > 500 ? s.Substring(0, 500) : s;
    }
}
