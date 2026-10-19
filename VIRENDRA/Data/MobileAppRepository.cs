using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>SQL used only by the mobile app API (App/webservice/*).</summary>
    public class MobileAppRepository
    {
        private readonly string _conn;

        public MobileAppRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        // ── Session token (User.AppToken) ────────────────────────────────────

        public User GetUserByAppToken(string token)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<User>(@"
                    SELECT u.Id, u.Username, u.Password, u.RoleId, r.RoleName, u.IsActive,
                           u.IsLocked, u.IsDeleted, u.PackageId, u.UserBal, u.AppToken, u.UserPin
                    FROM [User] u
                    LEFT JOIN [Role] r ON r.Id = u.RoleId
                    WHERE u.AppToken = @Token",
                    new { Token = token });
        }

        public void SaveAppLogin(int userId, string appToken, string fireToken)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE [User]
                    SET AppToken      = @AppToken,
                        Firebasetoken = CASE WHEN ISNULL(@FireToken, '') = '' THEN Firebasetoken ELSE @FireToken END,
                        RetryCount    = 0,
                        UpdatedDate   = GETDATE()
                    WHERE Id = @UserId",
                    new { UserId = userId, AppToken = appToken, FireToken = fireToken });
        }

        public decimal GetUserBalance(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<decimal?>(
                    "SELECT UserBal FROM [User] WHERE Id = @Id", new { Id = userId }) ?? 0m;
        }

        public (decimal Balance, decimal OutStanding) GetUserBalances(int userId)
        {
            using (var c = new SqlConnection(_conn))
            {
                var row = c.QueryFirstOrDefault(
                    "SELECT ISNULL(UserBal, 0) AS Bal, ISNULL(UserOutStandingBal, 0) AS OutBal FROM [User] WHERE Id = @Id",
                    new { Id = userId });
                return row == null ? (0m, 0m) : ((decimal)row.Bal, (decimal)row.OutBal);
            }
        }

        // ── Operators ────────────────────────────────────────────────────────

        public List<AppOperatorRow> GetOperators(int? opTypeId)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<AppOperatorRow>(@"
                    SELECT o.Id, o.Name AS OperatorName, o.OpCode AS OperatorCode,
                           o.OpTypeId, t.TypeName AS OperatorType
                    FROM [Operator] o
                    LEFT JOIN OperatorType t ON t.Id = o.OpTypeId
                    WHERE o.IsActive = 1
                      AND (@OpTypeId IS NULL OR o.OpTypeId = @OpTypeId)
                    ORDER BY o.Name",
                    new { OpTypeId = opTypeId }).ToList();
        }

        // ── Password / OTP ───────────────────────────────────────────────────

        public void UpdatePassword(int userId, string password)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE [User]
                    SET Password = @Password, OTP = NULL, ResetCode = NULL,
                        RetryCount = 0, UpdatedDate = GETDATE(), UpdatedById = @UserId
                    WHERE Id = @UserId",
                    new { UserId = userId, Password = password });
        }

        /// <summary>Stores the OTP in User.OTP and its expiry (yyyyMMddHHmmss) in User.ResetCode.</summary>
        public void SaveOtp(int userId, string otp, DateTime expiry)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(
                    "UPDATE [User] SET OTP = @Otp, ResetCode = @Expiry WHERE Id = @UserId",
                    new { UserId = userId, Otp = otp, Expiry = expiry.ToString("yyyyMMddHHmmss") });
        }
    }
}
