using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
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

        // ── Existing methods ─────────────────────────────────────────────────

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
                            throw new InvalidOperationException("Insufficient balance. Available: " + opBal.ToString("0.##"));

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

                        c.Execute("UPDATE Recharge SET TxnId = @TxnId WHERE Id = @RecId",
                            new { TxnId=txnId, RecId=recId }, txn);

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

        // ── Recharge2 / full-routing methods ────────────────────────────────

        public Operator GetOperatorByCode(string opCode)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<Operator>(
                    @"SELECT Id, Name AS OperatorName, OpCode AS OperatorCode,
                             API1_Id, SwitchTypeId, IsActive
                      FROM [Operator]
                      WHERE OpCode = @OpCode AND IsActive = 1",
                    new { OpCode = opCode });
        }

        public Circle GetCircleByCode(string circleCode)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<Circle>(
                    "SELECT Id, CircleName, CircleCode FROM Circle WHERE CircleCode = @Code",
                    new { Code = circleCode });
        }

        public ApiUrlInfo GetApiUrlByApiId(int apiId)
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
                FROM ApiSource a
                INNER JOIN ApiUrl    u ON u.ApiId = a.Id
                INNER JOIN ApiUrlType t ON t.Id = u.UrlTypeId AND t.TypeName = 'RechargeRequest'
                WHERE a.Id = @ApiId AND a.IsActive = 1";

            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<ApiUrlInfo>(sql, new { ApiId = apiId });
        }

        public RechargeValidationResult ValidateRechargeRequest(
            int userId, string mobileNo, decimal amount,
            int opId, int circleId, string refTxnId)
        {
            var p = new DynamicParameters();
            p.Add("@UserId",      userId);
            p.Add("@MobileNo",    mobileNo,    DbType.String);
            p.Add("@Amount",      amount,      DbType.Decimal);
            p.Add("@OpId",        opId,        DbType.Int32);
            p.Add("@CircleId",    circleId,    DbType.Int32);
            p.Add("@RefTxnId",    refTxnId,    DbType.String);
            p.Add("@StatusCode",  0,           DbType.Int32,   ParameterDirection.Output);
            p.Add("@StatusMsg",   string.Empty, DbType.String,  ParameterDirection.Output, 500);
            p.Add("@SwitchId",    0,           DbType.Int32,   ParameterDirection.Output);
            p.Add("@Api1",        0,           DbType.Int32,   ParameterDirection.Output);
            p.Add("@Api2",        0,           DbType.Int32,   ParameterDirection.Output);
            p.Add("@Api3",        0,           DbType.Int32,   ParameterDirection.Output);
            p.Add("@DebitAmount", 0m,          DbType.Decimal, ParameterDirection.Output);
            p.Add("@CommAmount",  0m,          DbType.Decimal, ParameterDirection.Output);

            using (var c = new SqlConnection(_conn))
                c.Execute("SP_RechargeRequestValidation", p,
                    commandType: CommandType.StoredProcedure);

            return new RechargeValidationResult
            {
                StatusCode   = p.Get<int>("@StatusCode"),
                StatusMsg    = p.Get<string>("@StatusMsg") ?? string.Empty,
                SwitchTypeId = p.Get<int>("@SwitchId"),
                Api1         = p.Get<int>("@Api1"),
                Api2         = p.Get<int>("@Api2"),
                Api3         = p.Get<int>("@Api3"),
                DebitAmount  = p.Get<decimal>("@DebitAmount"),
                CommAmount   = p.Get<decimal>("@CommAmount")
            };
        }

        // ── Shared routing SQL ───────────────────────────────────────────────

        private const string _routeSelectSql = @"
            SELECT cr.ApiId,
                   cr.Priority       AS PriorityId,
                   cr.CircleFilter,
                   cr.BlockUser,
                   cr.UserFilter,
                   ISNULL(cr.MinRO, 0) AS MinRO,
                   cr.Id             AS RouteId,
                   cr.FTypeId,
                   cr.RouteOP1,
                   cr.Priority       AS RoutePriorityId,
                   cr.AmountFilter
            FROM CommanRouting cr
            WHERE cr.OpId = @OpId AND cr.IsActive = 1
            ORDER BY cr.Priority";

        public List<ApiPriorityDto> GetCommanRouting(int opId, decimal amount, string circleCode)
        {
            using (var c = new SqlConnection(_conn))
            {
                var rows = c.Query<ApiPriorityDto>(_routeSelectSql, new { OpId = opId }).ToList();
                return rows.Where(r =>
                    CircleMatchesFilter(circleCode, r.CircleFilter) &&
                    AmountMatchesFilter(amount, r.AmountFilter, r.FTypeId ?? 1)
                ).ToList();
            }
        }

        public List<ApiPriorityDto> GetCircleRouting(int opId, int circleId, decimal amount)
        {
            using (var c = new SqlConnection(_conn))
            {
                string circleCode = c.ExecuteScalar<string>(
                    "SELECT ISNULL(CircleCode,'') FROM Circle WHERE Id = @Id",
                    new { Id = circleId }) ?? string.Empty;

                var rows = c.Query<ApiPriorityDto>(_routeSelectSql, new { OpId = opId }).ToList();
                return rows.Where(r =>
                    CircleMatchesFilter(circleCode, r.CircleFilter) &&
                    AmountMatchesFilter(amount, r.AmountFilter, r.FTypeId ?? 1)
                ).ToList();
            }
        }

        public List<ApiPriorityDto> GetAmountRoutes(int opId, int circleId, decimal amount)
        {
            using (var c = new SqlConnection(_conn))
            {
                var rows = c.Query<ApiPriorityDto>(_routeSelectSql, new { OpId = opId }).ToList();
                return rows.Where(r =>
                    AmountMatchesFilter(amount, r.AmountFilter, r.FTypeId ?? 1)
                ).ToList();
            }
        }

        public List<ApiPriorityDto> GetOperatorRoutes(int opId, decimal amount)
        {
            using (var c = new SqlConnection(_conn))
            {
                var rows = c.Query<ApiPriorityDto>(_routeSelectSql, new { OpId = opId }).ToList();
                return rows.Where(r =>
                    AmountMatchesFilter(amount, r.AmountFilter, r.FTypeId ?? 1)
                ).ToList();
            }
        }

        public CreateRechargeResult CreateRecharge(
            int userId, string mobileNo, decimal debitAmt, decimal commAmt,
            int opId, int circleId, int apiId, int routeId,
            string refTxnId, int addedById)
        {
            var p = new DynamicParameters();
            p.Add("@UserId",     userId,    DbType.Int32);
            p.Add("@MobileNo",   mobileNo,  DbType.String);
            p.Add("@Amount",     debitAmt,  DbType.Decimal);
            p.Add("@CommAmt",    commAmt,   DbType.Decimal);
            p.Add("@OpId",       opId,      DbType.Int32);
            p.Add("@CircleId",   circleId,  DbType.Int32);
            p.Add("@ApiId",      apiId,     DbType.Int32);
            p.Add("@RouteId",    routeId,   DbType.Int32);
            p.Add("@RefTxnId",   refTxnId,  DbType.String);
            p.Add("@AddedById",  addedById, DbType.Int32);

            p.Add("@RecId",      0L,            DbType.Int64,   ParameterDirection.Output);
            p.Add("@TxnId",      0L,            DbType.Int64,   ParameterDirection.Output);
            p.Add("@OpBal",      0m,            DbType.Decimal, ParameterDirection.Output);
            p.Add("@ApiURL",     string.Empty,  DbType.String,  ParameterDirection.Output, 2000);
            p.Add("@PostData",   string.Empty,  DbType.String,  ParameterDirection.Output, 2000);
            p.Add("@OP1",        string.Empty,  DbType.String,  ParameterDirection.Output, 200);
            p.Add("@OP2",        string.Empty,  DbType.String,  ParameterDirection.Output, 200);
            p.Add("@ApiTypeId",  0,             DbType.Int32,   ParameterDirection.Output);
            p.Add("@StatusCode", 0,             DbType.Int32,   ParameterDirection.Output);
            p.Add("@StatusMsg",  string.Empty,  DbType.String,  ParameterDirection.Output, 500);

            using (var c = new SqlConnection(_conn))
                c.Execute("usp_RechargeCreate", p, commandType: CommandType.StoredProcedure);

            return new CreateRechargeResult
            {
                RecId      = p.Get<long>("@RecId"),
                TxnId      = p.Get<long>("@TxnId"),
                OpBal      = p.Get<decimal>("@OpBal"),
                ApiUrl     = p.Get<string>("@ApiURL")   ?? string.Empty,
                PostData   = p.Get<string>("@PostData") ?? string.Empty,
                OP1        = p.Get<string>("@OP1")       ?? string.Empty,
                OP2        = p.Get<string>("@OP2")       ?? string.Empty,
                ApiTypeId  = p.Get<int>("@ApiTypeId"),
                StatusCode = p.Get<int>("@StatusCode"),
                StatusMsg  = p.Get<string>("@StatusMsg") ?? string.Empty
            };
        }

        public void UpdateStatusWithCheck(long recId, int statusId, string apiTxnId,
            string statusMsg, decimal apiComm, decimal apiBal)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE Recharge SET
                        StatusId     = @StatusId,
                        ApiTxnId     = CASE WHEN @ApiTxnId <> '' THEN @ApiTxnId ELSE ApiTxnId END,
                        StatusMsg    = @StatusMsg,
                        ResponseTime = GETDATE(),
                        Recharge_Commision = CASE WHEN @ApiComm > 0 THEN @ApiComm ELSE Recharge_Commision END,
                        ApiBal       = CASE WHEN @ApiBal  > 0 THEN @ApiBal  ELSE ApiBal          END
                    WHERE Id = @RecId",
                    new { RecId=recId, StatusId=statusId,
                          ApiTxnId=apiTxnId ?? string.Empty, StatusMsg=statusMsg,
                          ApiComm=apiComm, ApiBal=apiBal });
        }

        public void AddUpdateReqRes(long recId, string reqTxt, string respTxt, int apiId)
        {
            using (var c = new SqlConnection(_conn))
            {
                // Pull userId / txnId from the Recharge row so we can INSERT if needed
                var info = c.QueryFirstOrDefault(
                    "SELECT UserId, UserTxnId, CustomerNo FROM Recharge WHERE Id = @Id",
                    new { Id = recId });

                int    userId    = info != null ? (int)info.UserId        : 0;
                string userTxnId = info != null ? (string)info.UserTxnId ?? "" : "";
                string custNo    = info != null ? (string)info.CustomerNo ?? "" : "";

                int exists = c.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM RequestResponse WHERE RecId = @RecId",
                    new { RecId = recId });

                if (exists > 0)
                {
                    c.Execute(@"
                        UPDATE RequestResponse
                        SET RequestTxt = @Req, ResponseText = @Resp, AddedDate = GETDATE()
                        WHERE RecId = @RecId",
                        new { RecId=recId, Req=reqTxt, Resp=respTxt });
                }
                else
                {
                    c.Execute(@"
                        INSERT INTO RequestResponse
                            (UserId, UrlId, RecId, RefId, RequestTxt, ResponseText,
                             AddedDate, UserTxnId, CustomerNo)
                        VALUES
                            (@UserId, @ApiId, @RecId, @RefId, @Req, @Resp,
                             GETDATE(), @TxnId, @CustNo)",
                        new { UserId=userId, ApiId=apiId, RecId=recId, RefId=userTxnId,
                              Req=reqTxt, Resp=respTxt, TxnId=userTxnId, CustNo=custNo });
                }
            }
        }

        public List<FilterTag> GetFilterTags(int apiId)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<FilterTag>(
                    @"SELECT Id, ApiId, TagName, TagValue, StatusId
                      FROM FilterRespTag
                      WHERE ApiId = @ApiId AND IsActive = 1
                      ORDER BY Id",
                    new { ApiId = apiId }).ToList();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static bool CircleMatchesFilter(string circleCode, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                filter.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(circleCode))
                return true;
            return filter.Split(',').Any(part =>
                part.Trim().Equals(circleCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static bool AmountMatchesFilter(decimal amount, string filter, int fTypeId)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            if (fTypeId == 2) // Exact amount list
            {
                return filter.Split(',').Any(part =>
                    decimal.TryParse(part.Trim(), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out decimal v) && v == amount);
            }

            // Range  (e.g. "10-5000")
            int dash = filter.IndexOf('-');
            if (dash > 0 &&
                decimal.TryParse(filter.Substring(0, dash).Trim(),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out decimal min) &&
                decimal.TryParse(filter.Substring(dash + 1).Trim(),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out decimal max))
            {
                return amount >= min && amount <= max;
            }

            return true; // unparseable filter -> allow
        }
    }
}
