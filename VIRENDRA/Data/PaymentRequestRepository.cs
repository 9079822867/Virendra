using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>
    /// User-side payment (fund) requests in WalletRequest. A new request is put on Hold
    /// (StatusId 5) because sp_AddUserWallet's Approve path only approves held requests.
    /// </summary>
    public class PaymentRequestRepository
    {
        public const byte StatusHold     = 5;
        public const byte StatusApproved = 6;
        public const byte StatusRejected = 7;
        private const byte TxnTypeWallet = 4;    // TxnType.Wallet
        private const byte AmtTypeRequest = 10;  // AmtType.Request

        private readonly string _conn;

        public PaymentRequestRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        // Active company accounts only (same rule as Bank Accounts: BlockUser filled = Active)
        // that someone can actually pay into — skips placeholder rows like "Admin / ADMIN".
        public List<DepositAccount> GetDepositAccounts()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<DepositAccount>(@"
                    SELECT Id, BankName, HolderName, AccountNo, IFSCCode, UpiAdress, BranchName
                    FROM BankAccount
                    WHERE ISNULL(BlockUser, '') <> '' AND UserId IS NULL AND ApiId IS NULL
                      AND (AccountNo LIKE '%[0-9][0-9][0-9][0-9]%' OR UpiAdress LIKE '%_@_%')
                    ORDER BY BankName").ToList();
        }

        // CREDIT is not a way to pay us
        public List<LookupItem> GetTransferTypes()
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<LookupItem>(
                    "SELECT Id, Name FROM TransferType WHERE Name NOT LIKE '%CREDIT%' ORDER BY Id").ToList();
        }

        public bool ReferenceExists(string chequeNo)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM WalletRequest WHERE Chequeno = @No", new { No = chequeNo.Trim() }) > 0;
        }

        public long Create(int userId, PaymentRequestModel m)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<long>(@"
                    INSERT INTO WalletRequest
                        (UserId, Amount, TxnTypeId, AmtTypeId, StatusId, Bankname, Chequeno, PaymentRemark,
                         AddedDate, AddedById, TrTypeId, BankAccountId, PaymentDate, ParentUserID)
                    SELECT @UserId, @Amount, @TxnType, @AmtType, @Status,
                           (SELECT BankName FROM BankAccount WHERE Id = @BankAccountId),
                           @Chequeno, @Remark, GETDATE(), @UserId, @TrTypeId, @BankAccountId, @PaymentDate,
                           (SELECT ParentID FROM [User] WHERE Id = @UserId);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                    new
                    {
                        UserId        = userId,
                        Amount        = m.Amount,
                        TxnType       = TxnTypeWallet,
                        AmtType       = AmtTypeRequest,
                        Status        = StatusHold,
                        BankAccountId = m.BankAccountId,
                        Chequeno      = m.Chequeno.Trim(),
                        Remark        = string.IsNullOrWhiteSpace(m.PaymentRemark) ? null : m.PaymentRemark.Trim(),
                        TrTypeId      = m.TrTypeId,
                        PaymentDate   = m.PaymentDate?.Date
                    });
        }

        public List<PaymentRequestRow> GetMyRequests(int userId, DateTime fromDate, DateTime toDate)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<PaymentRequestRow>(@"
                    SELECT w.Id, ISNULL(w.Amount, 0) AS Amount, w.StatusId, w.Chequeno,
                           t.Name AS TransferType, ISNULL(b.BankName, w.Bankname) AS BankName,
                           w.PaymentRemark, w.Comment, w.PaymentDate, w.AddedDate, w.UpdatedDate
                    FROM WalletRequest w
                    LEFT JOIN TransferType t ON t.Id = w.TrTypeId
                    LEFT JOIN BankAccount  b ON b.Id = w.BankAccountId
                    WHERE w.UserId = @UserId
                      AND w.AddedDate >= @FromDate AND w.AddedDate < DATEADD(DAY, 1, @ToDate)
                    ORDER BY w.Id DESC",
                    new { UserId = userId, FromDate = fromDate.Date, ToDate = toDate.Date }).ToList();
        }
    }
}
