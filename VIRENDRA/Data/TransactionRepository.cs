using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly string _connectionString;

        public TransactionRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        public DashboardStats GetDashboardStats(int userId)
        {
            const string sql = @"
                DECLARE @Today    DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);
                DECLARE @Tomorrow DATETIME = DATEADD(DAY, 1, @Today);

                SELECT
                    -- Closing balance = live UserBal from User table
                    ISNULL((SELECT TOP 1 UserBal
                            FROM [User] WHERE Id = @UserId), 0)
                        AS ClosingBalance,

                    -- Opening balance = OP_Bal of first TxnLedger entry today;
                    --   if no entry today, use CL_Bal of last entry before today;
                    --   if no TxnLedger at all, fall back to current UserBal
                    ISNULL(
                        (SELECT TOP 1 OP_Bal FROM TxnLedger
                         WHERE UserId = @UserId AND TxnDate >= @Today ORDER BY Id ASC),
                        ISNULL(
                            (SELECT TOP 1 CL_Bal FROM TxnLedger
                             WHERE UserId = @UserId AND TxnDate < @Today ORDER BY Id DESC),
                            ISNULL((SELECT TOP 1 UserBal FROM [User] WHERE Id = @UserId), 0)
                        )
                    ) AS OpeningBalance,

                    -- Success amount today
                    ISNULL((SELECT SUM(Amount) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 2
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS SuccessAmount,

                    -- Failed amount today
                    ISNULL((SELECT SUM(Amount) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 3
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS FailedAmount,

                    -- Refund (no dedicated table yet)
                    0.0 AS RefundAmount,

                    -- Credit balance: today's CR_Amt from TxnLedger
                    ISNULL((SELECT SUM(CR_Amt) FROM TxnLedger
                            WHERE UserId = @UserId
                              AND TxnDate >= @Today AND TxnDate < @Tomorrow), 0)
                        AS CreditBalance,

                    -- Today's earning: commission on success recharges
                    ISNULL((SELECT SUM(Recharge_Commision) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 2
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS TodayEarning,

                    -- Counts
                    ISNULL((SELECT COUNT(*) FROM Recharge
                            WHERE UserId = @UserId
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS TotalTxnCount,

                    ISNULL((SELECT COUNT(*) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 2
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS SuccessCount,

                    ISNULL((SELECT COUNT(*) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 3
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS FailedCount,

                    ISNULL((SELECT COUNT(*) FROM Recharge
                            WHERE UserId = @UserId AND StatusId = 1
                              AND RequestTime >= @Today AND RequestTime < @Tomorrow), 0)
                        AS PendingCount";

            using (var conn = new SqlConnection(_connectionString))
                return conn.QueryFirstOrDefault<DashboardStats>(sql, new { UserId = userId })
                       ?? new DashboardStats();
        }

        public List<RechargeHistoryItem> GetRecentRecharges(int top = 10)
        {
            const string sql = @"
                SELECT TOP (@Top)
                    r.UserTxnId   AS TxnId,
                    r.OurRefTxnId,
                    r.ApiTxnId,
                    r.CustomerNo  AS Number,
                    ISNULL(o.Name, '-') AS Operator,
                    r.Amount,
                    ISNULL(r.Recharge_Commision, 0) AS Commission,
                    CASE r.StatusId
                        WHEN 2 THEN 'Success'
                        WHEN 3 THEN 'Failed'
                        ELSE 'Pending'
                    END AS Status,
                    r.StatusMsg,
                    r.RequestTime AS Date
                FROM Recharge r
                LEFT JOIN [Operator] o ON o.Id = r.OpId
                ORDER BY r.RequestTime DESC";

            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<RechargeHistoryItem>(sql, new { Top = top }).ToList();
        }

        public List<RechargeHistoryItem> GetRechargeHistory(
            int? userId, DateTime fromDate, DateTime toDate, string status, string searchText)
        {
            const string sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY r.RequestTime DESC) AS SrNo,
                    r.UserTxnId  AS TxnId,
                    r.OurRefTxnId,
                    r.ApiTxnId,
                    r.CustomerNo AS Number,
                    ISNULL(o.Name, '-') AS Operator,
                    r.Amount,
                    ISNULL(r.Recharge_Commision, 0) AS Commission,
                    CASE r.StatusId WHEN 2 THEN 'Success' WHEN 3 THEN 'Failed' ELSE 'Pending' END AS Status,
                    r.StatusMsg,
                    r.RequestTime AS Date
                FROM Recharge r
                LEFT JOIN [Operator] o ON o.Id = r.OpId
                WHERE r.RequestTime >= @FromDate
                  AND r.RequestTime <  DATEADD(DAY, 1, @ToDate)
                  AND (@UserId IS NULL OR r.UserId = @UserId)
                  AND (@Status IS NULL OR
                       CASE r.StatusId WHEN 2 THEN 'Success' WHEN 3 THEN 'Failed' ELSE 'Pending' END = @Status)
                  AND (@Search IS NULL OR
                       r.UserTxnId  LIKE '%' + @Search + '%' OR
                       r.CustomerNo LIKE '%' + @Search + '%' OR
                       o.Name       LIKE '%' + @Search + '%')
                ORDER BY r.RequestTime DESC";

            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<RechargeHistoryItem>(sql, new
                {
                    FromDate = fromDate.Date,
                    ToDate   = toDate.Date,
                    UserId   = userId,
                    Status   = string.IsNullOrWhiteSpace(status) ? (string)null : status,
                    Search   = string.IsNullOrWhiteSpace(searchText) ? (string)null : searchText.Trim()
                }).ToList();
        }

        public List<TransactionHistoryItem> GetTransactionHistory(
            int? userId, DateTime fromDate, DateTime toDate, string type, string searchText)
        {
            const string sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY t.TxnDate DESC) AS SrNo,
                    t.RefTxnId   AS TxnId,
                    ISNULL(tt.TypeName, 'Unknown') AS Type,
                    ISNULL(t.Remark, '-') AS Description,
                    t.CR_Amt AS Credit,
                    t.DB_Amt AS Debit,
                    t.CL_Bal AS Balance,
                    t.TxnDate AS Date
                FROM TxnLedger t
                LEFT JOIN TxnType tt ON tt.Id = t.TxnTypeId
                WHERE t.TxnDate >= @FromDate
                  AND t.TxnDate <  DATEADD(DAY, 1, @ToDate)
                  AND (@UserId IS NULL OR t.UserId = @UserId)
                  AND (@Type   IS NULL OR tt.TypeName = @Type)
                  AND (@Search IS NULL OR
                       t.RefTxnId LIKE '%' + @Search + '%' OR
                       t.Remark   LIKE '%' + @Search + '%')
                ORDER BY t.TxnDate DESC";

            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<TransactionHistoryItem>(sql, new
                {
                    FromDate = fromDate.Date,
                    ToDate   = toDate.Date,
                    UserId   = userId,
                    Type     = string.IsNullOrWhiteSpace(type) ? (string)null : type,
                    Search   = string.IsNullOrWhiteSpace(searchText) ? (string)null : searchText.Trim()
                }).ToList();
        }
    }
}
