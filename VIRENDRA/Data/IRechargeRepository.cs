using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IRechargeRepository
    {
        // ── Existing lookup / legacy helpers ────────────────────────────────
        List<Operator>  GetActiveOperators();
        List<Circle>    GetCircles();
        ApiUrlInfo      GetRechargeApiUrl(int opId);
        string          GetOperatorApiCode(int opId, int apiId, out string extraUrl, out string extraData);
        decimal         GetUserBalance(int userId);

        (long RecId, long TxnId, decimal OpBal) InsertPendingRecharge(
            int userId, int opId, int circleId, string customerNo,
            decimal amount, byte rcTypeId, string userTxnId, int apiId, int addedById);

        void UpdateRechargeSuccess(long recId, string apiTxnId, string statusMsg,
            decimal apiComm, decimal apiBal);
        void UpdateRechargeProcess(long recId, string apiTxnId, string statusMsg);
        void UpdateRechargeFailed(long recId, string statusMsg);

        void InsertRefundLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal amount, int addedById);
        void InsertCommissionLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal commAmount, int addedById);

        void LogRequestResponse(int userId, int urlId, long recId,
            string userTxnId, string customerNo, string requestTxt, string responseTxt);

        List<RechargeHistoryItem> GetRecentRecharges(int userId, int count = 10);

        // ── Recharge2 / full-routing support ────────────────────────────────

        /// <summary>Look up an operator row by its API code string.</summary>
        Operator GetOperatorByCode(string opCode);

        /// <summary>Look up a circle row by its code string.</summary>
        Circle GetCircleByCode(string circleCode);

        /// <summary>Get the API URL template for a specific API source (not operator).</summary>
        ApiUrlInfo GetApiUrlByApiId(int apiId);

        /// <summary>
        /// Call SP_RechargeRequestValidation to validate the request and determine
        /// the switch type, debit amount, commission and r-offer info.
        /// </summary>
        RechargeValidationResult ValidateRechargeRequest(
            int userId, string mobileNo, decimal amount,
            int opId, int circleId, string refTxnId, string ipAddress = null);

        /// <summary>Common-route routing: no circle restriction, purely priority-ordered.</summary>
        List<ApiPriorityDto> GetCommanRouting(int opId, decimal amount, string circleCode);

        /// <summary>Circle-wise routing: filter by circleId.</summary>
        List<ApiPriorityDto> GetCircleRouting(int opId, int circleId, decimal amount);

        /// <summary>Amount-wise routing: filter by amount range / list.</summary>
        List<ApiPriorityDto> GetAmountRoutes(int opId, int circleId, decimal amount);

        /// <summary>Operator-wise routing: all active routes for the operator.</summary>
        List<ApiPriorityDto> GetOperatorRoutes(int opId, decimal amount);

        /// <summary>
        /// Call usp_RechargeCreate.
        /// Pass switchedRecId = 0 for a new recharge (creates record + debits balance).
        /// Pass switchedRecId = existing RecId to retry with a different API (no extra debit).
        /// </summary>
        CreateRechargeResult CreateRecharge(
            int userId,
            string customerNo,
            decimal amount,
            decimal debitAmount,
            int opId,
            int circleId,
            int apiId,
            string userTxnId,
            string ourRef,
            string ipAddress     = null,
            int    switchTypeId  = 0,
            long   switchedRecId = 0,
            int    mediumId      = 2,
            string circleFilter  = null);

        /// <summary>Insert or update the RequestResponse log row for a recharge attempt.</summary>
        void AddUpdateReqRes(long recId, string reqTxt, string respTxt, int apiId);

        /// <summary>Retrieve tag-value filter rules used to classify an API response.</summary>
        List<FilterTag> GetFilterTags(int apiId);

        /// <summary>
        /// Call usp_UpdateRechargeStatus. The SP updates the Recharge row and handles
        /// refund / downline commission; it reports what it did via isRefund / isDownline.
        /// </summary>
        /// <summary>Current Recharge.StatusId, or null when the record does not exist.</summary>
        int? GetRechargeStatusId(long recId);

        void UpdateRechargeStatus(long recId, int userId, int statusId,
            string apiTxnId, string optTxnId, string statusMsg, string remark,
            out bool isDownline, out bool isRefund, out string spLog,
            int opId = 0, decimal apiBal = 0, string updateType = "StatusWithCheck");

        // ── Vendor callback ─────────────────────────────────────────────────

        /// <summary>Vendor whose ApiSource.CallbackId matches, with its CallBack ApiUrl row (if any).</summary>
        CallbackApiInfo GetCallbackApi(string callbackId);

        /// <summary>Recharge sent to <paramref name="apiId"/> matching our ref / user txn id or the vendor txn id.</summary>
        CallbackRecharge FindCallbackRecharge(int apiId, string ourRef, string apiTxnId);

        /// <summary>Append a RequestResponse row (does not overwrite the original request log).</summary>
        void InsertReqRes(int userId, int urlId, long recId, string refId, string remark,
            string reqTxt, string respTxt, string customerNo, string userTxnId);
    }

    public class CallbackApiInfo
    {
        public int    ApiId   { get; set; }
        public string ApiName { get; set; }
        public int?   UrlId   { get; set; }
        public string ResType { get; set; }
    }

    public class CallbackRecharge
    {
        public long    RecId           { get; set; }
        public int     UserId          { get; set; }
        public int     ApiId           { get; set; }
        public int?    OpId            { get; set; }
        public int     StatusId        { get; set; }
        public decimal Amount          { get; set; }
        public string  CustomerNo      { get; set; }
        public string  UserTxnId       { get; set; }
        public string  OurRefTxnId     { get; set; }
        public string  ApiTxnId        { get; set; }
        public string  OptTxnId        { get; set; }
        public string  OperatorName    { get; set; }
        public string  UserCallbackUrl { get; set; }
    }

    // ── Supporting DTO ───────────────────────────────────────────────────────
    public class ApiUrlInfo
    {
        public int    ApiId      { get; set; }
        public string ApiUserId  { get; set; }
        public string ApiPassword{ get; set; }
        public string Remark     { get; set; }
        public int    UrlId      { get; set; }
        public string Url        { get; set; }
        public string Method     { get; set; }   // GET | POST
        public string PostData   { get; set; }
        public string ResType    { get; set; }
    }
}
