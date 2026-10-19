using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    /// <summary>[UserKyc] documents: user upload and admin approve / reject.</summary>
    public class KycRepository
    {
        private readonly string _conn;

        public KycRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        public List<UserKyc> GetByUser(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<UserKyc>(
                    "SELECT * FROM UserKyc WHERE UserId = @UserId", new { UserId = userId }).ToList();
        }

        public UserKyc GetDocument(int userId, string category)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<UserKyc>(
                    "SELECT * FROM UserKyc WHERE UserId = @UserId AND Category = @Category",
                    new { UserId = userId, Category = category });
        }

        /// <summary>True when all required categories are approved.</summary>
        public bool IsApproved(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(@"
                    SELECT COUNT(DISTINCT Category) FROM UserKyc
                    WHERE UserId = @UserId AND StatusId = @Approved AND Category IN @Categories",
                    new { UserId = userId, Approved = KycConstants.Approved, Categories = KycConstants.Categories })
                    == KycConstants.Categories.Length;
        }

        /// <summary>Insert, or replace the file of an existing row and reset it to Pending.</summary>
        public void SaveUpload(int userId, string category, string fileName)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    IF EXISTS (SELECT 1 FROM UserKyc WHERE UserId = @UserId AND Category = @Category)
                        UPDATE UserKyc
                        SET ImgPath = @ImgPath, StatusId = @Pending, AddedbyDate = GETDATE(),
                            Remark = NULL, ActionById = NULL, ActionDate = NULL
                        WHERE UserId = @UserId AND Category = @Category
                    ELSE
                        INSERT INTO UserKyc (UserId, Category, ImgPath, StatusId, AddedbyDate)
                        VALUES (@UserId, @Category, @ImgPath, @Pending, GETDATE())",
                    new { UserId = userId, Category = category, ImgPath = fileName, Pending = KycConstants.Pending });
        }

        /// <summary>Approve / reject one document, or all documents of the user when category is null.</summary>
        public int SetStatus(int userId, string category, int statusId, string remark, int adminId)
        {
            using (var c = new SqlConnection(_conn))
                return c.Execute(@"
                    UPDATE UserKyc
                    SET StatusId = @StatusId, Remark = @Remark, ActionById = @AdminId, ActionDate = GETDATE()
                    WHERE UserId = @UserId AND (@Category IS NULL OR Category = @Category)",
                    new { UserId = userId, Category = category, StatusId = statusId, Remark = remark, AdminId = adminId });
        }

        /// <summary>Every user who has uploaded at least one KYC document.</summary>
        public List<KycUserSummary> GetUserSummaries(int? userId = null)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<KycUserSummary>(@"
                    SELECT u.Id AS UserId, u.Username, up.FullName, up.MobileNumber, r.RoleName,
                           COUNT(k.Id) AS UploadedCount,
                           SUM(CASE WHEN k.StatusId = @Pending  THEN 1 ELSE 0 END) AS PendingCount,
                           SUM(CASE WHEN k.StatusId = @Approved THEN 1 ELSE 0 END) AS ApprovedCount,
                           SUM(CASE WHEN k.StatusId = @Rejected THEN 1 ELSE 0 END) AS RejectedCount,
                           MAX(k.AddedbyDate) AS LastUpload
                    FROM UserKyc k
                    INNER JOIN [User] u     ON u.Id = k.UserId
                    LEFT JOIN UserProfile up ON up.UserId = u.Id
                    LEFT JOIN [Role] r       ON r.Id = u.RoleId
                    WHERE u.IsDeleted = 0 AND (@UserId IS NULL OR u.Id = @UserId)
                    GROUP BY u.Id, u.Username, up.FullName, up.MobileNumber, r.RoleName
                    ORDER BY MAX(k.AddedbyDate) DESC",
                    new
                    {
                        UserId   = userId,
                        Pending  = KycConstants.Pending,
                        Approved = KycConstants.Approved,
                        Rejected = KycConstants.Rejected
                    }).ToList();
        }
    }
}
