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
            int opId, int circleId, string refTxnId);

        /// <summary>Common-route routing: no circle restriction, purely priority-ordered.</summary>
        List<ApiPriorityDto> GetCommanRouting(int opId, decimal amount, string circleCode);

        /// <summary>Circle-wise routing: filter by circleId.</summary>
        List<ApiPriorityDto> GetCircleRouting(int opId, int circleId, decimal amount);

        /// <summary>Amount-wise routing: filter by amount range / list.</summary>
        List<ApiPriorityDto> GetAmountRoutes(int opId, int circleId, decimal amount);

        /// <summary>Operator-wise routing: all active routes for the operator.</summary>
        List<ApiPriorityDto> GetOperatorRoutes(int opId, decimal amount);

        /// <summary>
        /// Call usp_RechargeCreate to insert the Recharge + TxnLedger debit and
        /// return the API URL credentials and status.
        /// </summary>
        CreateRechargeResult CreateRecharge(
            int userId, string mobileNo, decimal debitAmt, decimal commAmt,
            int opId, int circleId, int apiId, int routeId,
            string refTxnId, int addedById);

        /// <summary>Update recharge status (preserves ApiTxnId / ApiComm / ApiBal if already set).</summary>
        void UpdateStatusWithCheck(long recId, int statusId, string apiTxnId,
            string statusMsg, decimal apiComm, decimal apiBal);

        /// <summary>Insert or update the RequestResponse log row for a recharge attempt.</summary>
        void AddUpdateReqRes(long recId, string reqTxt, string respTxt, int apiId);

        /// <summary>Retrieve tag-value filter rules used to classify an API response.</summary>
        List<FilterTag> GetFilterTags(int apiId);
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
