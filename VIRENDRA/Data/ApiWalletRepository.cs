using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>
    /// Vendor (API) wallet ledger ported from STC ApiWalletService / sp_AddApiWallet.
    /// ApiWalletTxn.CL_Bal is a computed column (OP_Bal + CR_Amt + Ins_Amt - DB_Amt).
    /// </summary>
    public class ApiWalletRepository
    {
        private const byte TxnTypeWallet    = 4;   // TxnType.Wallet
        private const byte AmtTypeReceived  = 6;   // AmtType.Received
        private const byte AmtTypeSent      = 7;   // AmtType.Sent

        private readonly string _conn;

        public ApiWalletRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        // ── Lookups ─────────────────────────────────────────────────────────

        public List<LookupItem> GetApis()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, ApiName AS Name FROM ApiSource ORDER BY ApiName").ToList();
        }

        public List<LookupItem> GetUsers()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, Username AS Name FROM [User] ORDER BY Username").ToList();
        }

        public List<LookupItem> GetTxnTypes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>("SELECT Id, TypeName AS Name FROM TxnType ORDER BY Id").ToList();
        }

        // Only the amount types tagged for API use (STC GetAmtTypes("Api"))
        public List<LookupItem> GetAmtTypes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>(
                    "SELECT Id, AmtTypeName AS Name FROM AmtType WHERE Remark LIKE '%Api%' ORDER BY Id").ToList();
        }

        // CREDIT is excluded: vendor top-ups are always real transfers
        public List<LookupItem> GetTransferTypes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>(
                    "SELECT Id, Name FROM TransferType WHERE Name NOT LIKE '%CREDIT%' ORDER BY Id").ToList();
        }

        public List<LookupItem> GetBankAccounts()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>(@"
                    SELECT Id,
                           ISNULL(HolderName, '-') + ISNULL(' (' + BankName + ' ' + RIGHT(AccountNo, 4) + ')', '') AS Name
                    FROM BankAccount
                    ORDER BY Id").ToList();
        }

        // ── Ledger ──────────────────────────────────────────────────────────

        public List<ApiWalletTxnItem> GetApiWalletTxns(DateTime fromDate, DateTime toDate, long? recId, long? txnId,
            int? txnTypeId, int? amtTypeId, int? apiId, int? userId, string remark)
        {
            const string sql = @"
                SELECT
                    w.Id, w.ApiId,
                    ISNULL(a.ApiName, '-')       AS VendorName,
                    w.TxnDate, w.RecId, w.RefTxnId,
                    w.OP_Bal, w.CR_Amt,
                    ISNULL(w.Ins_Amt, 0)         AS Ins_Amt,
                    w.DB_Amt,
                    ISNULL(w.CL_Bal, 0)          AS CL_Bal,
                    ISNULL(tt.TypeName, '-')     AS TxnType,
                    ISNULL(at.AmtTypeName, '-')  AS AmtType,
                    w.Remark,
                    w.ApiClBal,
                    ISNULL(w.ClBalDiff, 0)       AS ClBalDiff,
                    COALESCE(ru.Username, wu.Username) AS UserName,
                    ab.Username                  AS AddedBy
                FROM ApiWalletTxn w
                LEFT JOIN ApiSource a  ON a.Id  = w.ApiId
                LEFT JOIN TxnType   tt ON tt.Id = w.TxnTypeId
                LEFT JOIN AmtType   at ON at.Id = w.AmtTypeId
                LEFT JOIN Recharge  r  ON r.Id  = w.RecId
                LEFT JOIN [User]    ru ON ru.Id = r.UserId
                LEFT JOIN [User]    wu ON wu.Id = w.UserId
                LEFT JOIN [User]    ab ON ab.Id = w.AddedById
                WHERE w.TxnDate >= @FromDate
                  AND w.TxnDate <  DATEADD(DAY, 1, @ToDate)
                  AND (@RecId     IS NULL OR w.RecId     = @RecId)
                  AND (@TxnId     IS NULL OR w.Id        = @TxnId)
                  AND (@TxnTypeId IS NULL OR w.TxnTypeId = @TxnTypeId)
                  AND (@AmtTypeId IS NULL OR w.AmtTypeId = @AmtTypeId)
                  AND (@ApiId     IS NULL OR w.ApiId     = @ApiId)
                  AND (@UserId    IS NULL OR r.UserId    = @UserId OR w.UserId = @UserId)
                  AND (@Remark    IS NULL OR w.Remark LIKE '%' + @Remark + '%')
                ORDER BY w.Id DESC";

            using (var c = new SqlConnection(_conn))
                return c.Query<ApiWalletTxnItem>(sql, new
                {
                    FromDate  = fromDate.Date,
                    ToDate    = toDate.Date,
                    RecId     = recId,
                    TxnId     = txnId,
                    TxnTypeId = txnTypeId,
                    AmtTypeId = amtTypeId,
                    ApiId     = apiId,
                    UserId    = userId,
                    Remark    = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim()
                }).ToList();
        }

        // Latest closing balance of the vendor wallet (STC View_ApiListWithBalance.cl_bal)
        public decimal GetApiWalletBalance(int apiId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<decimal?>(
                    "SELECT TOP 1 CL_Bal FROM ApiWalletTxn WITH (NOLOCK) WHERE ApiId = @ApiId ORDER BY Id DESC",
                    new { ApiId = apiId }) ?? 0;
        }

        /// <summary>
        /// Manual vendor wallet entry (STC sp_AddApiWallet): Sent → CR_Amt, Received → DB_Amt,
        /// Incentive → Ins_Amt. The cheque/ref no is kept in Remark and must be unique per vendor.
        /// ApiSource.ApiBal is synced to the new closing balance, as usp_UpdateRechargeStatus does.
        /// </summary>
        public ApiWalletAddResult AddApiWallet(ApiWalletAddModel m, string trTypeName, int addedById)
        {
            string refTag = "Ref:" + m.ChequeNo.Trim() + "|";
            string remark = (trTypeName + "|" + refTag + (m.Remark ?? "").Trim());
            if (remark.Length > 100) remark = remark.Substring(0, 100);

            using (var c = new SqlConnection(_conn))
            {
                c.Open();
                using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (c.ExecuteScalar<int>("SELECT COUNT(1) FROM ApiSource WHERE Id = @Id",
                            new { Id = m.ApiId }, tx) == 0)
                    {
                        tx.Rollback();
                        return ApiWalletAddResult.InvalidVendor;
                    }

                    int dup = c.ExecuteScalar<int>(@"
                        SELECT COUNT(1) FROM ApiWalletTxn WITH (UPDLOCK, HOLDLOCK)
                        WHERE ApiId = @ApiId AND TxnTypeId = @TxnType
                          AND CHARINDEX(@RefTag, Remark) > 0",
                        new { m.ApiId, TxnType = TxnTypeWallet, RefTag = refTag }, tx);
                    if (dup > 0)
                    {
                        tx.Rollback();
                        return ApiWalletAddResult.Duplicate;
                    }

                    c.Execute(@"
                        DECLARE @Op DECIMAL(18,2) = ISNULL((SELECT TOP 1 CL_Bal FROM ApiWalletTxn WITH (UPDLOCK)
                                                            WHERE ApiId = @ApiId ORDER BY Id DESC), 0);

                        INSERT INTO ApiWalletTxn
                            (ApiId, TxnDate, OP_Bal, CR_Amt, DB_Amt, Ins_Amt, TxnTypeId, AmtTypeId, Remark, AddedById)
                        VALUES
                            (@ApiId, GETDATE(), @Op, @Sent, @Received, @Incentive, @TxnType, @AmtType, @Remark, @AddedById);

                        UPDATE ApiSource
                        SET ApiBal = (SELECT CL_Bal FROM ApiWalletTxn WHERE Id = SCOPE_IDENTITY())
                        WHERE Id = @ApiId;",
                        new
                        {
                            m.ApiId,
                            Sent      = m.SentAmount,
                            Received  = m.ReceivedAmount,
                            Incentive = m.IncentiveAmount,
                            TxnType   = TxnTypeWallet,
                            AmtType   = m.SentAmount > 0 || m.IncentiveAmount > 0 ? AmtTypeSent : AmtTypeReceived,
                            Remark    = remark,
                            AddedById = addedById > 0 ? addedById : (int?)null
                        }, tx);

                    tx.Commit();
                    return ApiWalletAddResult.Success;
                }
            }
        }
    }
}
