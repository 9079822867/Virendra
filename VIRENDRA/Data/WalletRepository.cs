using System.Collections.Generic;
using System.Configuration;
using System.Data;
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
                return conn.Query<BankAccount>(@"
                    SELECT b.Id, b.BankName, b.AccountNo, b.HolderName, b.IFSCCode,
                           b.UpiAdress, b.BranchName, b.BranchAddress,
                           b.BlockAmount, b.AccountTypeId, b.UserId, b.ApiId,
                           b.Remark, b.AddedById, b.AddedDate, b.BlockUser, b.ImageUrl
                    FROM BankAccount b
                    ORDER BY b.BankName"
                ).ToList();
        }

        public BankAccount GetBankAccountById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
                return conn.QueryFirstOrDefault<BankAccount>(@"
                    SELECT Id, BankName, AccountNo, HolderName, IFSCCode,
                           UpiAdress, BranchName, BranchAddress,
                           BlockAmount, AccountTypeId, UserId, ApiId,
                           Remark, AddedById, AddedDate, UpdatedById, UpdatedDate,
                           BlockUser, ImageUrl
                    FROM BankAccount WHERE Id = @Id",
                    new { Id = id });
        }

        public int CreateBankAccount(BankAccount bank)
        {
            const string sql = @"
                INSERT INTO BankAccount
                    (BankName, AccountNo, HolderName, IFSCCode, UpiAdress,
                     BranchName, BranchAddress, BlockAmount, AccountTypeId,
                     Remark, AddedById, AddedDate, BlockUser)
                VALUES
                    (@BankName, @AccountNo, @HolderName, @IFSCCode, @UpiAdress,
                     @BranchName, @BranchAddress, @BlockAmount, @AccountTypeId,
                     @Remark, @AddedById, GETDATE(), @BlockUser);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(_connectionString))
                return conn.ExecuteScalar<int>(sql, bank);
        }

        public void UpdateBankAccount(BankAccount bank)
        {
            const string sql = @"
                UPDATE BankAccount SET
                    BankName=@BankName, AccountNo=@AccountNo, HolderName=@HolderName,
                    IFSCCode=@IFSCCode, UpiAdress=@UpiAdress,
                    BranchName=@BranchName, BranchAddress=@BranchAddress,
                    BlockAmount=@BlockAmount, AccountTypeId=@AccountTypeId,
                    Remark=@Remark, UpdatedById=@UpdatedById, UpdatedDate=GETDATE(),
                    BlockUser=@BlockUser
                WHERE Id=@Id";

            using (var conn = new SqlConnection(_connectionString))
                conn.Execute(sql, bank);
        }

        public void DeleteBankAccount(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("DELETE FROM BankAccount WHERE Id = @Id", new { Id = id });
        }

        public void ToggleBlockUser(int id, bool blockUser)
        {
            // blockUser=true means "block the account" → store NULL (empty = Blocked in UI)
            // blockUser=false means "activate the account" → store "Yes" (non-empty = Active in UI)
            var value = blockUser ? (string)null : "Yes";
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("UPDATE BankAccount SET BlockUser = @BlockUser WHERE Id = @Id",
                    new { Id = id, BlockUser = value });
        }

        public (bool Success, string Error, string Log) AddMoney(WalletRequest req)
        {
            // Map TransferType string → TrTypeId int for the SP
            int trTypeId;
            switch (req.TransferType?.ToUpper())
            {
                case "IMPS":   trTypeId = 1; break;
                case "NEFT":   trTypeId = 2; break;
                case "RTGS":   trTypeId = 3; break;
                case "UPI":    trTypeId = 4; break;
                case "CASH":   trTypeId = 5; break;
                case "CHEQUE": trTypeId = 6; break;
                default:       trTypeId = 1; break;
            }

            var p = new DynamicParameters();
            p.Add("@IsPullOut",      req.IsPullOut ? "Yes" : "No");
            p.Add("@IsDebit",        req.IsDebit   ? "Yes" : "No");
            p.Add("@IsCredit",       req.IsCredit  ? "Yes" : "No");
            p.Add("@UserId",         req.UserId);
            p.Add("@Amount",         req.Amount);
            p.Add("@Remark",         req.Comment);          // Remark alias → Comment column
            p.Add("@AddedById",      req.AddedById ?? 0);
            p.Add("@WRID",           req.Chequeno);          // Wallet Request ID = ref/cheque no
            p.Add("@IsCreditClear",  "0");
            p.Add("@TrTypeId",       trTypeId);
            p.Add("@BankAccountId",  req.BankAccountId ?? 0);
            p.Add("@gateWay",        1);
            p.Add("@ChequeNo",       req.Chequeno);
            p.Add("@PaymentDate",    req.PaymentDate);
            p.Add("@error", dbType: DbType.String, direction: ParameterDirection.Output, size: 100);
            p.Add("@Log",   dbType: DbType.String, direction: ParameterDirection.Output, size: 250);

            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("sp_AddUserWallet", p, commandType: CommandType.StoredProcedure);

            var error   = p.Get<string>("@error") ?? "0";
            var log     = p.Get<string>("@Log")   ?? string.Empty;
            var success = error == "0" || string.IsNullOrWhiteSpace(error);

            return (success, error, log);
        }

        public List<WalletRequest> GetTransactions(int? userId = null, int? statusId = null)
        {
            const string sql = @"
                SELECT w.Id, w.UserId, w.Amount, w.TxnTypeId, w.AmtTypeId, w.StatusId,
                       w.Chequeno, w.PaymentRemark, w.Comment, w.TrTypeId,
                       w.BankAccountId, w.PaymentDate, w.AddedDate, w.AddedById,
                       w.UpdatedDate, w.UpdatedById,
                       u.Username AS UserName,
                       b.HolderName AS BankAccountName
                FROM WalletRequest w
                LEFT JOIN [User] u ON u.Id = w.UserId
                LEFT JOIN BankAccount b ON b.Id = w.BankAccountId
                WHERE (@UserId   IS NULL OR w.UserId   = @UserId)
                  AND (@StatusId IS NULL OR w.StatusId = @StatusId)
                ORDER BY w.AddedDate DESC";

            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<WalletRequest>(sql, new { UserId = userId, StatusId = statusId }).ToList();
        }

        public WalletRequest GetWalletRequestById(int id)
        {
            const string sql = @"
                SELECT w.Id, w.UserId, w.Amount, w.TxnTypeId, w.AmtTypeId, w.StatusId,
                       w.Chequeno, w.PaymentRemark, w.Comment, w.TrTypeId,
                       w.BankAccountId, w.PaymentDate, w.AddedDate, w.AddedById,
                       w.UpdatedDate, w.UpdatedById,
                       u.Username AS UserName,
                       b.HolderName AS BankAccountName
                FROM WalletRequest w
                LEFT JOIN [User] u ON u.Id = w.UserId
                LEFT JOIN BankAccount b ON b.Id = w.BankAccountId
                WHERE w.Id = @Id";

            using (var conn = new SqlConnection(_connectionString))
                return conn.QueryFirstOrDefault<WalletRequest>(sql, new { Id = id });
        }

        public void UpdateWalletRequest(WalletRequest req)
        {
            const string sql = @"
                UPDATE WalletRequest SET
                    UserId        = @UserId,
                    Amount        = @Amount,
                    BankAccountId = @BankAccountId,
                    Chequeno      = @Chequeno,
                    PaymentRemark = @PaymentRemark,
                    Comment       = @Comment,
                    PaymentDate   = @PaymentDate,
                    TrTypeId      = CASE @TransferType
                                        WHEN 'IMPS'   THEN 1
                                        WHEN 'NEFT'   THEN 2
                                        WHEN 'RTGS'   THEN 3
                                        WHEN 'UPI'    THEN 4
                                        WHEN 'Cash'   THEN 5
                                        WHEN 'Cheque' THEN 6
                                        ELSE TrTypeId END,
                    AmtTypeId     = CASE WHEN @WalletType = 'BillPayment' THEN 2 ELSE 1 END,
                    TxnTypeId     = CASE WHEN @IsCredit = 1 THEN 1
                                         WHEN @IsDebit  = 1 THEN 2
                                         ELSE TxnTypeId END,
                    UpdatedDate   = GETDATE(),
                    UpdatedById   = @UpdatedById
                WHERE Id = @Id";

            using (var conn = new SqlConnection(_connectionString))
                conn.Execute(sql, new
                {
                    req.Id,
                    req.UserId,
                    req.Amount,
                    req.BankAccountId,
                    req.Chequeno,
                    req.PaymentRemark,
                    req.Comment,
                    req.PaymentDate,
                    req.TransferType,
                    req.WalletType,
                    req.IsCredit,
                    req.IsDebit,
                    req.UpdatedById
                });
        }

        public void DeleteWalletRequest(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("DELETE FROM WalletRequest WHERE Id = @Id", new { Id = id });
        }

        public void UpdateRequestStatus(int id, int statusId, int updatedById)
        {
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute(@"UPDATE WalletRequest
                               SET StatusId = @StatusId, UpdatedDate = GETDATE(), UpdatedById = @UpdatedById
                               WHERE Id = @Id",
                    new { Id = id, StatusId = statusId, UpdatedById = updatedById });
        }
    }
}
