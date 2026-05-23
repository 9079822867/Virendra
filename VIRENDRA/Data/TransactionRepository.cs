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
