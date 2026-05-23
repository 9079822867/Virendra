using System;
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
            {
                return conn.Query<BankAccount>(
                    "SELECT Id, AccountName, AccountNumber, BankName, IsActive FROM BankAccount WHERE IsActive = 1"
                ).ToList();
            }
        }

        public void AddMoney(WalletTransaction txn)
        {
            const string sql = @"
                INSERT INTO WalletTransaction
                    (WalletType, UserId, Amount, IsPullOut, IsCredit, IsDebit, SMS,
                     TransferType, PaymentDate, PaymentRemark, BankAccountId,
                     ChequeRefNo, Remark, AddedDate, AddedBy)
                VALUES
                    (@WalletType, @UserId, @Amount, @IsPullOut, @IsCredit, @IsDebit, @SMS,
                     @TransferType, @PaymentDate, @PaymentRemark, @BankAccountId,
                     @ChequeRefNo, @Remark, GETDATE(), @AddedBy)";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Execute(sql, txn);
            }
        }

        public List<WalletTransaction> GetTransactions(int? userId = null)
        {
            var sql = @"
                SELECT w.*, u.Username AS UserName, b.AccountName AS BankAccountName
                FROM WalletTransaction w
                LEFT JOIN [User] u ON u.Id = w.UserId
                LEFT JOIN BankAccount b ON b.Id = w.BankAccountId
                WHERE (@UserId IS NULL OR w.UserId = @UserId)
                ORDER BY w.AddedDate DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                return conn.Query<WalletTransaction>(sql, new { UserId = userId }).ToList();
            }
        }
    }
}
