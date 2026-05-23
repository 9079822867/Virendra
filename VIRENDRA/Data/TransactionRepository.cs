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

        public List<RechargeHistoryItem> GetRechargeHistory(
            int? userId, DateTime fromDate, DateTime toDate, string status, string searchText)
        {
            var sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY r.RequestTime DESC) AS SrNo,
                    r.UserTxnId   AS TxnId,
                    r.CustomerNo  AS Number,
                    ISNULL(r.OperatorName, '-') AS Operator,
                    r.Amount,
                    ISNULL(r.Commission, 0) AS Commission,
                    r.Status,
                    r.RequestTime AS Date
                FROM RechargeTransaction r
                WHERE
                    r.RequestTime >= @FromDate
                    AND r.RequestTime <  DATEADD(DAY, 1, @ToDate)
                    AND (@UserId    IS NULL OR r.UserId = @UserId)
                    AND (@Status    IS NULL OR r.Status = @Status)
                    AND (@Search    IS NULL OR
                         r.UserTxnId   LIKE '%' + @Search + '%' OR
                         r.CustomerNo  LIKE '%' + @Search + '%' OR
                         r.OperatorName LIKE '%' + @Search + '%')
                ORDER BY r.RequestTime DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                return conn.Query<RechargeHistoryItem>(sql, new
                {
                    FromDate = fromDate.Date,
                    ToDate   = toDate.Date,
                    UserId   = userId,
                    Status   = string.IsNullOrWhiteSpace(status) ? (string)null : status,
                    Search   = string.IsNullOrWhiteSpace(searchText) ? (string)null : searchText.Trim()
                }).ToList();
            }
        }

        public List<TransactionHistoryItem> GetTransactionHistory(
            int? userId, DateTime fromDate, DateTime toDate, string type, string searchText)
        {
            var sql = @"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY t.AddedDate DESC) AS SrNo,
                    t.TxnId,
                    t.Type,
                    ISNULL(t.Description, '-') AS Description,
                    ISNULL(t.Credit, 0)  AS Credit,
                    ISNULL(t.Debit, 0)   AS Debit,
                    ISNULL(t.Balance, 0) AS Balance,
                    t.AddedDate AS Date
                FROM UserLedger t
                WHERE
                    t.AddedDate >= @FromDate
                    AND t.AddedDate <  DATEADD(DAY, 1, @ToDate)
                    AND (@UserId  IS NULL OR t.UserId = @UserId)
                    AND (@Type    IS NULL OR t.Type = @Type)
                    AND (@Search  IS NULL OR
                         t.TxnId       LIKE '%' + @Search + '%' OR
                         t.Description LIKE '%' + @Search + '%' OR
                         t.Type        LIKE '%' + @Search + '%')
                ORDER BY t.AddedDate DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
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
}
