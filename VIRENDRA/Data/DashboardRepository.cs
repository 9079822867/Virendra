using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly string _connectionString;

        public DashboardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public UserDashboardInfo GetUserDashboardInfo(int userId)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT 
                        u.Id as UserId,
                        u.Username,
                        up.FullName,
                        up.Email,
                        up.MobileNumber,
                        u.UserBal as CurrentBalance,
                        u.UserOutStandingBal as OutstandingBalance,
                        u.PackageId,
                        p.PackageName
                    FROM [User] u
                    LEFT JOIN UserProfile up ON u.Id = up.UserId
                    LEFT JOIN Package p ON u.PackageId = p.Id
                    WHERE u.Id = @UserId AND u.IsDeleted = 0";

                return db.QueryFirstOrDefault<UserDashboardInfo>(query, new { UserId = userId });
            }
        }

        public WalletSummary GetWalletSummary(int userId, DateTime fromDate, DateTime toDate)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    DECLARE @OpeningBalance DECIMAL(18, 4) = 0;

                    -- Get opening balance (balance at start of period)
                    SELECT TOP 1 @OpeningBalance = OP_Bal 
                    FROM TxnLedger 
                    WHERE UserId = @UserId AND TxnDate >= @FromDate
                    ORDER BY TxnDate ASC;

                    SELECT 
                        ISNULL(@OpeningBalance, 0) as OpeningBalance,
                        ISNULL(MAX(CL_Bal), 0) as ClosingBalance,
                        ISNULL(SUM(CASE WHEN TxnTypeId = 1 THEN CR_Amt ELSE 0 END), 0) as TodayEarning,
                        ISNULL(SUM(CASE WHEN AmtTypeId = 1 THEN CR_Amt ELSE 0 END), 0) as CreditBalance,
                        ISNULL(SUM(CASE WHEN TxnTypeId = 3 THEN CR_Amt ELSE 0 END), 0) as RefundAmount,
                        COUNT(*) as TotalTransactionCount
                    FROM TxnLedger
                    WHERE UserId = @UserId AND TxnDate BETWEEN @FromDate AND @ToDate";

                return db.QueryFirstOrDefault<WalletSummary>(query,
                    new { UserId = userId, FromDate = fromDate, ToDate = toDate });
            }
        }

        public TransactionSummary GetTransactionSummary(int userId, DateTime fromDate, DateTime toDate)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT 
                        COUNT(CASE WHEN StatusId = 1 THEN 1 END) as SuccessCount,
                        COUNT(CASE WHEN StatusId = 2 THEN 1 END) as FailureCount,
                        ISNULL(SUM(CASE WHEN StatusId = 1 THEN Amount ELSE 0 END), 0) as SuccessAmount,
                        ISNULL(SUM(CASE WHEN StatusId = 2 THEN Amount ELSE 0 END), 0) as FailureAmount,
                        ISNULL(AVG(CASE WHEN StatusId = 1 THEN Amount ELSE NULL END), 0) as AverageTransactionValue
                    FROM Recharge
                    WHERE UserId = @UserId AND RequestTime BETWEEN @FromDate AND @ToDate
                    AND IsDeleted IS NULL OR IsDeleted = 0";

                return db.QueryFirstOrDefault<TransactionSummary>(query,
                    new { UserId = userId, FromDate = fromDate, ToDate = toDate });
            }
        }

        public List<RecentTransaction> GetRecentTransactions(int userId, int pageSize = 10)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT TOP (@PageSize)
                        r.Id as RecId,
                        r.CustomerNo,
                        o.Name as OperatorName,
                        c.CircleName,
                        r.Amount,
                        CASE 
                            WHEN r.StatusId = 1 THEN 'SUCCESS'
                            WHEN r.StatusId = 2 THEN 'FAILURE'
                            WHEN r.StatusId = 3 THEN 'PENDING'
                            ELSE 'UNKNOWN'
                        END as Status,
                        r.RequestTime,
                        r.UserTxnId,
                        r.Recharge_Commision as Commission
                    FROM Recharge r
                    LEFT JOIN Operator o ON r.OpId = o.Id
                    LEFT JOIN Circle c ON r.CircleId = c.Id
                    WHERE r.UserId = @UserId
                    ORDER BY r.RequestTime DESC";

                return db.Query<RecentTransaction>(query, new { UserId = userId, PageSize = pageSize }).ToList();
            }
        }
    }
}
