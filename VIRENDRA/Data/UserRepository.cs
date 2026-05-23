using System;
using System.Collections.Generic;
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

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
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
                string query = @"SELECT Id, Username, Password, RoleId, TokenAPI, IsActive, IsLocked, 
                                IsDeleted, RetryCount, OTP, PassCode, LoginIP, AddedDate, UpdatedDate, 
                                AddedById, UpdatedById, CallbackURL, ResetCode, PackageId, HKey, HPass, 
                                UserBal, ParentID, UserPin, AppToken, Firebasetoken, ComplainCallbackURL, 
                                UserOutStandingBal, IsComm, IsOtpCheck, IsJioActiveHigh 
                                FROM [User]
                                WHERE IsDeleted = 0
                                ORDER BY Id DESC";
                return db.Query<User>(query).ToList();
            }
        }

        public User GetUserByUsername(string username)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"SELECT Id, Username, Password, RoleId, TokenAPI, IsActive, IsLocked, 
                                IsDeleted, RetryCount, OTP, PassCode, LoginIP, AddedDate, UpdatedDate, 
                                AddedById, UpdatedById, CallbackURL, ResetCode, PackageId, HKey, HPass, 
                                UserBal, ParentID, UserPin, AppToken, Firebasetoken, ComplainCallbackURL, 
                                UserOutStandingBal, IsComm, IsOtpCheck, IsJioActiveHigh 
                                FROM [User] WHERE Username = @Username";
                return db.QueryFirstOrDefault<User>(query, new { Username = username });
            }
        }

        public User GetUserById(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"SELECT Id, Username, Password, RoleId, TokenAPI, IsActive, IsLocked, 
                                IsDeleted, RetryCount, OTP, PassCode, LoginIP, AddedDate, UpdatedDate, 
                                AddedById, UpdatedById, CallbackURL, ResetCode, PackageId, HKey, HPass, 
                                UserBal, ParentID, UserPin, AppToken, Firebasetoken, ComplainCallbackURL, 
                                UserOutStandingBal, IsComm, IsOtpCheck, IsJioActiveHigh 
                                FROM [User] WHERE Id = @Id";
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
                string query = "UPDATE [User] SET IsDeleted = 1 WHERE Id = @Id";
                db.Execute(query, new { Id = id });
            }
        }

        public void UpdateLoginIP(int userId, string ipAddress)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = "UPDATE [User] SET LoginIP = @LoginIP WHERE Id = @UserId";
                db.Execute(query, new { LoginIP = ipAddress, UserId = userId });
            }
        }

        public void UpdateApiSettings(int userId, string ipAddress, string callbackUrl, string token)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Open();
                string query = @"
                    UPDATE [User]
                    SET LoginIP = @LoginIP,
                        CallbackURL = @CallbackURL,
                        TokenAPI = @TokenAPI,
                        UpdatedDate = @UpdatedDate,
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
                string query = "UPDATE [User] SET RetryCount = @RetryCount WHERE Id = @UserId";
                db.Execute(query, new { RetryCount = retryCount, UserId = userId });
            }
        }
    }
}
