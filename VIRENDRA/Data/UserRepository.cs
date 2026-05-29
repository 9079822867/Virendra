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
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;

        public UserRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Operator> GetAllOperators()
        {
            using (var db = new SqlConnection(_connectionString))
                return db.Query<Operator>(
                    "SELECT Id, Name AS OperatorName, OpCode AS OperatorCode, IsActive FROM [Operator] ORDER BY Name"
                ).ToList();
        }

        public List<Circle> GetAllCircles()
        {
            using (var db = new SqlConnection(_connectionString))
                return db.Query<Circle>(
                    "SELECT Id, CircleName, CircleCode FROM Circle ORDER BY CircleName"
                ).ToList();
        }

        public List<Role> GetAllRoles()
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                return db.Query<Role>(
                    "SELECT Id, RoleName, IsActive FROM [Role] WHERE IsActive = 1 ORDER BY RoleName"
                ).ToList();
            }
        }

        public List<User> GetAllUsers()
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT u.Id, u.Username, u.Password, u.RoleId, r.RoleName,
                           u.TokenAPI, u.IsActive, u.IsLocked, u.IsDeleted, u.RetryCount,
                           u.OTP, u.PassCode, u.LoginIP, u.AddedDate, u.UpdatedDate,
                           u.AddedById, u.UpdatedById, u.CallbackURL, u.ResetCode,
                           u.PackageId, u.HKey, u.HPass, u.UserBal, u.ParentID,
                           u.UserPin, u.AppToken, u.Firebasetoken, u.ComplainCallbackURL,
                           u.UserOutStandingBal, u.IsComm, u.IsOtpCheck, u.IsJioActiveHigh
                    FROM [User] u
                    LEFT JOIN [Role] r ON r.Id = u.RoleId
                    WHERE u.IsDeleted = 0
                    ORDER BY u.Id DESC";
                return db.Query<User>(query).ToList();
            }
        }

        public User GetUserByUsername(string username)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT u.Id, u.Username, u.Password, u.RoleId, r.RoleName,
                           u.TokenAPI, u.IsActive, u.IsLocked, u.IsDeleted, u.RetryCount,
                           u.OTP, u.PassCode, u.LoginIP, u.AddedDate, u.UpdatedDate,
                           u.AddedById, u.UpdatedById, u.CallbackURL, u.ResetCode,
                           u.PackageId, u.HKey, u.HPass, u.UserBal, u.ParentID,
                           u.UserPin, u.AppToken, u.Firebasetoken, u.ComplainCallbackURL,
                           u.UserOutStandingBal, u.IsComm, u.IsOtpCheck, u.IsJioActiveHigh
                    FROM [User] u
                    LEFT JOIN [Role] r ON r.Id = u.RoleId
                    WHERE u.Username = @Username";
                return db.QueryFirstOrDefault<User>(query, new { Username = username });
            }
        }

        public User GetUserById(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    SELECT u.Id, u.Username, u.Password, u.RoleId, r.RoleName,
                           u.TokenAPI, u.IsActive, u.IsLocked, u.IsDeleted, u.RetryCount,
                           u.OTP, u.PassCode, u.LoginIP, u.AddedDate, u.UpdatedDate,
                           u.AddedById, u.UpdatedById, u.CallbackURL, u.ResetCode,
                           u.PackageId, u.HKey, u.HPass, u.UserBal, u.ParentID,
                           u.UserPin, u.AppToken, u.Firebasetoken, u.ComplainCallbackURL,
                           u.UserOutStandingBal, u.IsComm, u.IsOtpCheck, u.IsJioActiveHigh
                    FROM [User] u
                    LEFT JOIN [Role] r ON r.Id = u.RoleId
                    WHERE u.Id = @Id";
                return db.QueryFirstOrDefault<User>(query, new { Id = id });
            }
        }

        public void CreateUser(User user)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"INSERT INTO [User] (
                                    Username, Password, RoleId, TokenAPI, IsActive, IsLocked,
                                    IsDeleted, RetryCount, OTP, PassCode, LoginIP, AddedDate,
                                    AddedById, CallbackURL, ResetCode, PackageId, UserBal, ParentID,
                                    UserPin, AppToken, Firebasetoken, ComplainCallbackURL,
                                    UserOutStandingBal, IsComm, IsOtpCheck, IsJioActiveHigh
                                )
                                VALUES (
                                    @Username, @Password, @RoleId, @TokenAPI, @IsActive, @IsLocked,
                                    @IsDeleted, @RetryCount, @OTP, @PassCode, @LoginIP, @AddedDate,
                                    @AddedById, @CallbackURL, @ResetCode, @PackageId, @UserBal, @ParentID,
                                    @UserPin, @AppToken, @Firebasetoken, @ComplainCallbackURL,
                                    @UserOutStandingBal, @IsComm, @IsOtpCheck, @IsJioActiveHigh
                                )";
                db.Execute(query, user);
            }
        }

        public void UpdateUser(User user)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"UPDATE [User]
                                SET Username = @Username, Password = @Password, RoleId = @RoleId,
                                    TokenAPI = @TokenAPI, IsActive = @IsActive, IsLocked = @IsLocked,
                                    IsDeleted = @IsDeleted, RetryCount = @RetryCount, OTP = @OTP,
                                    PassCode = @PassCode, LoginIP = @LoginIP, UpdatedDate = @UpdatedDate,
                                    UpdatedById = @UpdatedById, CallbackURL = @CallbackURL, ResetCode = @ResetCode,
                                    PackageId = @PackageId, UserBal = @UserBal, UserPin = @UserPin,
                                    AppToken = @AppToken, Firebasetoken = @Firebasetoken,
                                    ComplainCallbackURL = @ComplainCallbackURL, UserOutStandingBal = @UserOutStandingBal,
                                    IsComm = @IsComm, IsOtpCheck = @IsOtpCheck, IsJioActiveHigh = @IsJioActiveHigh
                                WHERE Id = @Id";
                db.Execute(query, user);
            }
        }

        public void DeleteUser(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                db.Execute("UPDATE [User] SET IsDeleted = 1 WHERE Id = @Id", new { Id = id });
            }
        }

        public void UpdateLoginIP(int userId, string ipAddress)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                db.Execute("UPDATE [User] SET LoginIP = @LoginIP WHERE Id = @UserId",
                    new { LoginIP = ipAddress, UserId = userId });
            }
        }

        public void UpdateApiSettings(int userId, string ipAddress, string callbackUrl, string token)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"UPDATE [User]
                    SET LoginIP = @LoginIP, CallbackURL = @CallbackURL,
                        TokenAPI = @TokenAPI, UpdatedDate = @UpdatedDate,
                        UpdatedById = @UserId
                    WHERE Id = @UserId";
                db.Execute(query, new
                {
                    LoginIP = ipAddress,
                    CallbackURL = callbackUrl,
                    TokenAPI = token,
                    UpdatedDate = DateTime.Now,
                    UserId = userId
                });
            }
        }

        public void UpdateRetryCount(int userId, int retryCount)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                db.Execute("UPDATE [User] SET RetryCount = @RetryCount WHERE Id = @UserId",
                    new { RetryCount = retryCount, UserId = userId });
            }
        }
    }
}
