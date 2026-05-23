using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class WalletRepository : IWalletRepository
    {
        private readonly string _connectionString;

        public WalletRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        public List<BankAccount> GetAllBankAccounts()
        {
            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<BankAccount>(
                    "SELECT Id, BankName, AccountNo, HolderName, IFSCCode, UpiAdress, BranchName, Remark FROM BankAccount ORDER BY BankName"
                ).ToList();
        }

        public void AddMoney(WalletRequest req)
        {
            // Map UI helper properties to real DB columns:
            //   WalletType "Main"/"BillPayment"  → AmtTypeId 1/2
            //   IsCredit/IsDebit                 → TxnTypeId 1/2
            //   TransferType string              → TrTypeId 1-6 (IMPS/NEFT/RTGS/UPI/Cash/Cheque)
            //   ChequeRefNo convenience prop     → already writes to Chequeno
            //   Remark convenience prop          → already writes to Comment
            const string sql = @"
                INSERT INTO WalletRequest
                    (UserId, Amount, TxnTypeId, AmtTypeId, StatusId,
                     Chequeno, PaymentRemark, Comment, TrTypeId,
                     BankAccountId, PaymentDate, AddedDate, AddedById)
                VALUES
                    (@UserId, @Amount,
                     CASE WHEN @IsCredit=1 THEN 1 WHEN @IsDebit=1 THEN 2 ELSE 1 END,
                     CASE WHEN @WalletType='BillPayment' THEN 2 ELSE 1 END,
                     1,
                     @Chequeno, @PaymentRemark, @Comment,
                     CASE @TransferType
                         WHEN 'IMPS'   THEN 1
                         WHEN 'NEFT'   THEN 2
                         WHEN 'RTGS'   THEN 3
                         WHEN 'UPI'    THEN 4
                         WHEN 'Cash'   THEN 5
                         WHEN 'Cheque' THEN 6
                         ELSE 1 END,
                     @BankAccountId, @PaymentDate, GETDATE(), @AddedById)";

            using (var conn = new SqlConnection(_connectionString))
                conn.Execute(sql, new
                {
                    req.UserId,
                    req.Amount,
                    req.IsCredit,
                    req.IsDebit,
                    req.WalletType,
                    req.Chequeno,
                    req.PaymentRemark,
                    req.Comment,
                    req.TransferType,
                    req.BankAccountId,
                    req.PaymentDate,
                    req.AddedById
                });
        }

        public List<WalletRequest> GetTransactions(int? userId = null)
        {
            const string sql = @"
                SELECT w.Id, w.UserId, w.Amount, w.TxnTypeId, w.AmtTypeId, w.StatusId,
                       w.Chequeno, w.PaymentRemark, w.Comment, w.TrTypeId,
                       w.BankAccountId, w.PaymentDate, w.AddedDate, w.AddedById,
                       u.Username AS UserName,
                       b.HolderName AS BankAccountName
                FROM WalletRequest w
                LEFT JOIN [User] u ON u.Id = w.UserId
                LEFT JOIN BankAccount b ON b.Id = w.BankAccountId
                WHERE (@UserId IS NULL OR w.UserId = @UserId)
                ORDER BY w.AddedDate DESC";

            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<WalletRequest>(sql, new { UserId = userId }).ToList();
        }
    }
}
