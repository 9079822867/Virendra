using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class RechargeRepository : IRechargeRepository
    {
        private readonly string _conn;

        public RechargeRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        public List<Operator> GetActiveOperators()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<Operator>(
                    "SELECT Id, Name AS OperatorName, OpCode AS OperatorCode, API1_Id FROM [Operator] WHERE IsActive = 1 ORDER BY Name"
                ).ToList();
        }

        public List<Circle> GetCircles()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<Circle>(
                    "SELECT Id, CircleName, CircleCode FROM Circle ORDER BY CircleName"
                ).ToList();
        }

        public ApiUrlInfo GetRechargeApiUrl(int opId)
        {
            const string sql = @"
                SELECT TOP 1
                    a.Id        AS ApiId,
                    a.ApiUserId, a.ApiPassword, a.Remark,
                    u.Id        AS UrlId,
                    u.URL       AS Url,
                    ISNULL(u.Method,'GET') AS Method,
                    u.PostData,
                    u.ResType
                FROM [Operator] o
                INNER JOIN ApiSource a ON a.Id = o.API1_Id AND a.IsActive = 1
                INNER JOIN ApiUrl    u ON u.ApiId = a.Id
                INNER JOIN ApiUrlType t ON t.Id = u.UrlTypeId AND t.TypeName = 'RechargeRequest'
                WHERE o.Id = @OpId AND o.IsActive = 1";

            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<ApiUrlInfo>(sql, new { OpId = opId });
        }

        public string GetOperatorApiCode(int opId, int apiId, out string extraUrl, out string extraData)
        {
            const string sql = @"
                SELECT TOP 1 OpCode, ExtraUrl, ExtraUrlData
                FROM OperatorCode
                WHERE OpId = @OpId AND ApiId = @ApiId";

            using (var c = new SqlConnection(_conn))
            {
                var row = c.QueryFirstOrDefault(sql, new { OpId = opId, ApiId = apiId });
                if (row != null)
                {
                    extraUrl  = (string)row.ExtraUrl;
                    extraData = (string)row.ExtraUrlData;
                    return (string)row.OpCode;
                }
                // fallback to Operator.OpCode
                var opCode = c.ExecuteScalar<string>(
                    "SELECT OpCode FROM [Operator] WHERE Id = @OpId", new { OpId = opId });
                extraUrl  = null;
                extraData = null;
                return opCode;
            }
        }

        public decimal GetUserBalance(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<decimal>(
                    "SELECT ISNULL(UserBal, 0) FROM [User] WHERE Id = @Id", new { Id = userId });
        }

        public (long RecId, long TxnId, decimal OpBal) InsertPendingRecharge(
            int userId, int opId, int circleId, string customerNo,
            decimal amount, byte rcTypeId, string userTxnId, int apiId, int addedById)
        {
            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var txn = c.BeginTransaction())
                {
                    try
                    {
                        decimal opBal = c.ExecuteScalar<decimal>(
                            "SELECT ISNULL(UserBal, 0) FROM [User] WITH (UPDLOCK) WHERE Id = @Id",
                            new { Id = userId }, txn);

                        if (opBal < amount)
                            throw new InvalidOperationException("Insufficient balance. Available: ₹" + opBal.ToString("0.##"));

                        // Insert Recharge (Pending = StatusId 1)
                        long recId = c.ExecuteScalar<long>(@"
                            INSERT INTO Recharge
                                (UserId, ApiId, CustomerNo, OpId, CircleId, Amount, RCTypeId,
                                 StatusId, RequestTime, MediumId, UserTxnId, OurRefTxnId, cashback)
                            VALUES
                                (@UserId, @ApiId, @CustomerNo, @OpId, @CircleId, @Amount, @RCTypeId,
                                 1, GETDATE(), 1, @UserTxnId, @UserTxnId, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                            new { UserId=userId, ApiId=apiId, CustomerNo=customerNo,
                                  OpId=opId, CircleId=circleId, Amount=amount,
                                  RCTypeId=rcTypeId, UserTxnId=userTxnId }, txn);

                        // Insert TxnLedger (Debit, TxnTypeId=1)
                        long txnId = c.ExecuteScalar<long>(@"
                            INSERT INTO TxnLedger
                                (RecId, UserId, RefTxnId, TxnDate, OP_Bal, CR_Amt, DB_Amt,
                                 TxnTypeId, AmtTypeId, Remark, AddedById)
                            VALUES
                                (@RecId, @UserId, @RefTxnId, GETDATE(), @OpBal, 0, @DbAmt,
                                 1, 1, @Remark, @AddedById);
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                            new { RecId=recId, UserId=userId, RefTxnId=userTxnId,
                                  OpBal=opBal, DbAmt=amount,
                                  Remark="Recharge Debit - " + customerNo,
                                  AddedById=addedById }, txn);

                        // Link TxnLedger back to Recharge
                        c.Execute("UPDATE Recharge SET TxnId = @TxnId WHERE Id = @RecId",
                            new { TxnId=txnId, RecId=recId }, txn);

                        // Deduct user balance
                        c.Execute("UPDATE [User] SET UserBal = UserBal - @Amount WHERE Id = @UserId",
                            new { Amount=amount, UserId=userId }, txn);

                        txn.Commit();
                        return (recId, txnId, opBal);
                    }
                    catch
                    {
                        txn.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateRechargeSuccess(long recId, string apiTxnId, string statusMsg,
            decimal apiComm, decimal apiBal)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE Recharge SET
                        StatusId = 2, ApiTxnId = @ApiTxnId, StatusMsg = @StatusMsg,
                        ResponseTime = GETDATE(), Recharge_Commision = @ApiComm, ApiBal = @ApiBal
                    WHERE Id = @RecId",
                    new { RecId=recId, ApiTxnId=apiTxnId, StatusMsg=statusMsg,
                          ApiComm=apiComm, ApiBal=apiBal });
        }

        public void UpdateRechargeFailed(long recId, string statusMsg)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE Recharge SET StatusId = 3, StatusMsg = @StatusMsg, ResponseTime = GETDATE()
                    WHERE Id = @RecId",
                    new { RecId=recId, StatusMsg=statusMsg });
        }

        public void InsertRefundLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal amount, int addedById)
        {
            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var txn = c.BeginTransaction())
                {
                    try
                    {
                        decimal currentBal = c.ExecuteScalar<decimal>(
                            "SELECT ISNULL(UserBal, 0) FROM [User] WHERE Id = @Id",
                            new { Id = userId }, txn);

                        c.Execute(@"
                            INSERT INTO TxnLedger
                                (RecId, UserId, RefTxnId, TxnDate, OP_Bal, CR_Amt, DB_Amt,
                                 TxnTypeId, AmtTypeId, Remark, AddedById)
                            VALUES
                                (@RecId, @UserId, @RefTxnId, GETDATE(), @OpBal, @CrAmt, 0,
                                 4, 1, @Remark, @AddedById)",
                            new { RecId=recId, UserId=userId, RefTxnId="REF-"+refTxnId,
                                  OpBal=currentBal, CrAmt=amount,
                                  Remark="Refund - Recharge Failed - " + refTxnId,
                                  AddedById=addedById }, txn);

                        c.Execute("UPDATE [User] SET UserBal = UserBal + @Amount WHERE Id = @UserId",
                            new { Amount=amount, UserId=userId }, txn);

                        txn.Commit();
                    }
                    catch
                    {
                        txn.Rollback();
                        throw;
                    }
                }
            }
        }

        public void InsertCommissionLedger(long recId, int userId, string refTxnId,
            decimal opBal, decimal commAmount, int addedById)
        {
            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var txn = c.BeginTransaction())
                {
                    try
                    {
                        decimal currentBal = c.ExecuteScalar<decimal>(
                            "SELECT ISNULL(UserBal, 0) FROM [User] WHERE Id = @Id",
                            new { Id = userId }, txn);

                        c.Execute(@"
                            INSERT INTO TxnLedger
                                (RecId, UserId, RefTxnId, TxnDate, OP_Bal, CR_Amt, DB_Amt,
                                 TxnTypeId, AmtTypeId, Remark, AddedById)
                            VALUES
                                (@RecId, @UserId, @RefTxnId, GETDATE(), @OpBal, @CrAmt, 0,
                                 3, 1, @Remark, @AddedById)",
                            new { RecId=recId, UserId=userId, RefTxnId="COMM-"+refTxnId,
                                  OpBal=currentBal, CrAmt=commAmount,
                                  Remark="Commission Credit - " + refTxnId,
                                  AddedById=addedById }, txn);

                        c.Execute("UPDATE [User] SET UserBal = UserBal + @Amount WHERE Id = @UserId",
                            new { Amount=commAmount, UserId=userId }, txn);

                        txn.Commit();
                    }
                    catch
                    {
                        txn.Rollback();
                        throw;
                    }
                }
            }
        }

        public void LogRequestResponse(int userId, int urlId, long recId,
            string userTxnId, string customerNo, string requestTxt, string responseTxt)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    INSERT INTO RequestResponse
                        (UserId, UrlId, RecId, RefId, RequestTxt, ResponseText, AddedDate, UserTxnId, CustomerNo)
                    VALUES
                        (@UserId, @UrlId, @RecId, @RefId, @RequestTxt, @ResponseText, GETDATE(), @UserTxnId, @CustomerNo)",
                    new { UserId=userId, UrlId=urlId, RecId=recId, RefId=userTxnId,
                          RequestTxt=requestTxt, ResponseText=responseTxt,
                          UserTxnId=userTxnId, CustomerNo=customerNo });
        }

        public List<RechargeHistoryItem> GetRecentRecharges(int userId, int count = 10)
        {
            const string sql = @"
                SELECT TOP (@Count)
                    ROW_NUMBER() OVER (ORDER BY r.RequestTime DESC) AS SrNo,
                    r.UserTxnId  AS TxnId,
                    r.CustomerNo AS Number,
                    ISNULL(o.Name, '-') AS Operator,
                    r.Amount,
                    ISNULL(r.Recharge_Commision, 0) AS Commission,
                    CASE r.StatusId WHEN 2 THEN 'Success' WHEN 3 THEN 'Failed' ELSE 'Pending' END AS Status,
                    r.RequestTime AS Date
                FROM Recharge r
                LEFT JOIN [Operator] o ON o.Id = r.OpId
                WHERE r.UserId = @UserId
                ORDER BY r.RequestTime DESC";

            using (var c = new SqlConnection(_conn))
                return c.Query<RechargeHistoryItem>(sql, new { UserId=userId, Count=count }).ToList();
        }
    }
}
