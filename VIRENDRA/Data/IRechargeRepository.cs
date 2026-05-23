using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IRechargeRepository
    {
        List<Operator> GetActiveOperators();
        List<Circle>   GetCircles();

        // Returns the primary API source + its RechargeRequest URL for a given operator
        ApiUrlInfo GetRechargeApiUrl(int opId);

        // Operator-specific code for a given API (for [OOO] placeholder)
        string GetOperatorApiCode(int opId, int apiId, out string extraUrl, out string extraData);

        decimal GetUserBalance(int userId);

        // Inserts Recharge (Pending) + TxnLedger (Debit) + debits UserBal in one transaction.
        // Returns (RecId, TxnId, OpBal).
        (long RecId, long TxnId, decimal OpBal) InsertPendingRecharge(
            int userId, int opId, int circleId, string customerNo,
            decimal amount, byte rcTypeId, string userTxnId, int apiId, int addedById);

        void UpdateRechargeSuccess(long recId, string apiTxnId, string statusMsg,
            decimal apiComm, decimal apiBal);

        void UpdateRechargeFailed(long recId, string statusMsg);

        // Refund ledger + restore user balance on failure
        void InsertRefundLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal amount, int addedById);

        // Commission credit ledger
        void InsertCommissionLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal commAmount, int addedById);

        void LogRequestResponse(int userId, int urlId, long recId,
            string userTxnId, string customerNo, string requestTxt, string responseTxt);

        List<RechargeHistoryItem> GetRecentRecharges(int userId, int count = 10);
    }

    public class ApiUrlInfo
    {
        public int    ApiId      { get; set; }
        public string ApiUserId  { get; set; }
        public string ApiPassword{ get; set; }
        public string Remark     { get; set; }
        public int    UrlId      { get; set; }
        public string Url        { get; set; }
        public string Method     { get; set; }     // GET | POST
        public string PostData   { get; set; }
        public string ResType    { get; set; }
    }
}
