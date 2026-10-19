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
    /// [UserKyc] documents and [UserKycDetail] Aadhaar / PAN numbers:
    /// user upload / submit and admin approve / reject.
    /// </summary>
    public class KycRepository
    {
        private readonly string _conn;

        public KycRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        // ── Documents ([UserKyc]) ───────────────────────────────────────────

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

        /// <summary>Documents + numbers in one model.</summary>
        public KycStatusViewModel GetStatus(int userId) => new KycStatusViewModel
        {
            UserId    = userId,
            Documents = GetByUser(userId),
            Detail    = GetDetail(userId)
        };

        /// <summary>True when all required documents are approved and Aadhaar + PAN are verified.</summary>
        public bool IsApproved(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(@"
                    SELECT CASE WHEN
                        (SELECT COUNT(DISTINCT Category) FROM UserKyc
                         WHERE UserId = @UserId AND StatusId = @Approved AND Category IN @Categories) = @DocCount
                        AND EXISTS (SELECT 1 FROM UserKycDetail
                                    WHERE UserId = @UserId AND AadhaarStatusId = @Approved AND PanStatusId = @Approved)
                    THEN 1 ELSE 0 END",
                    new
                    {
                        UserId     = userId,
                        Approved   = KycConstants.Approved,
                        Categories = KycConstants.Categories,
                        DocCount   = KycConstants.Categories.Length
                    }) == 1;
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

        // ── Aadhaar / PAN numbers ([UserKycDetail]) ─────────────────────────

        public UserKycDetail GetDetail(int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<UserKycDetail>(
                    "SELECT * FROM UserKycDetail WHERE UserId = @UserId", new { UserId = userId });
        }

        /// <summary>True when another (not deleted) user already has this Aadhaar number.</summary>
        public bool AadhaarInUse(string aadhaar, int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(@"
                    SELECT COUNT(1) FROM UserKycDetail d INNER JOIN [User] u ON u.Id = d.UserId
                    WHERE d.AadhaarNumber = @Value AND d.UserId <> @UserId AND u.IsDeleted = 0",
                    new { Value = aadhaar, UserId = userId }) > 0;
        }

        /// <summary>True when another (not deleted) user already has this PAN.</summary>
        public bool PanInUse(string pan, int userId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(@"
                    SELECT COUNT(1) FROM UserKycDetail d INNER JOIN [User] u ON u.Id = d.UserId
                    WHERE d.PanNumber = @Value AND d.UserId <> @UserId AND u.IsDeleted = 0",
                    new { Value = pan, UserId = userId }) > 0;
        }

        /// <summary>Sets the Aadhaar number with its status (Pending for manual review, Verified by API / admin).</summary>
        public void SaveAadhaar(int userId, string aadhaar, int statusId, string mode, string remark, int? adminId)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM UserKycDetail WHERE UserId = @UserId)
                        INSERT INTO UserKycDetail (UserId, AddedDate) VALUES (@UserId, GETDATE());

                    UPDATE UserKycDetail
                    SET AadhaarNumber = @Aadhaar, AadhaarStatusId = @StatusId, AadhaarVerifyMode = @Mode,
                        AadhaarRemark = @Remark,
                        AadhaarVerifiedDate = CASE WHEN @StatusId = @Approved THEN GETDATE() ELSE NULL END,
                        ActionById = ISNULL(@AdminId, ActionById), UpdatedDate = GETDATE()
                    WHERE UserId = @UserId",
                    new
                    {
                        UserId = userId, Aadhaar = aadhaar, StatusId = statusId, Mode = mode,
                        Remark = remark, AdminId = adminId, Approved = KycConstants.Approved
                    });
        }

        /// <summary>Sets PAN details with their status (Pending for manual review, Verified by API / admin).</summary>
        public void SavePan(int userId, string pan, string name, DateTime? dob,
                            int statusId, string mode, string remark, int? adminId)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM UserKycDetail WHERE UserId = @UserId)
                        INSERT INTO UserKycDetail (UserId, AddedDate) VALUES (@UserId, GETDATE());

                    UPDATE UserKycDetail
                    SET PanNumber = @Pan, PanName = @Name, PanDob = @Dob, PanStatusId = @StatusId,
                        PanVerifyMode = @Mode, PanRemark = @Remark,
                        PanVerifiedDate = CASE WHEN @StatusId = @Approved THEN GETDATE() ELSE NULL END,
                        ActionById = ISNULL(@AdminId, ActionById), UpdatedDate = GETDATE()
                    WHERE UserId = @UserId",
                    new
                    {
                        UserId = userId, Pan = pan, Name = name, Dob = dob, StatusId = statusId, Mode = mode,
                        Remark = remark, AdminId = adminId, Approved = KycConstants.Approved
                    });
        }

        /// <summary>Admin verify / reject of a submitted number, keeping the number as it is.</summary>
        public void SetNumberStatus(int userId, bool aadhaar, int statusId, string remark, int adminId)
        {
            string p = aadhaar ? "Aadhaar" : "Pan";
            using (var c = new SqlConnection(_conn))
                c.Execute($@"
                    UPDATE UserKycDetail
                    SET {p}StatusId = @StatusId, {p}Remark = @Remark,
                        {p}VerifyMode = CASE WHEN @StatusId = @Approved THEN @Manual ELSE {p}VerifyMode END,
                        {p}VerifiedDate = CASE WHEN @StatusId = @Approved THEN GETDATE() ELSE NULL END,
                        ActionById = @AdminId, UpdatedDate = GETDATE()
                    WHERE UserId = @UserId AND {p}Number IS NOT NULL",
                    new
                    {
                        UserId = userId, StatusId = statusId, Remark = remark, AdminId = adminId,
                        Approved = KycConstants.Approved, Manual = KycConstants.ModeManual
                    });
        }

        // ── Admin list ──────────────────────────────────────────────────────

        /// <summary>Every user who has uploaded a KYC document or submitted an Aadhaar / PAN number.</summary>
        public List<KycUserSummary> GetUserSummaries(int? userId = null)
        {
            using (var c = new SqlConnection(_conn))
                return c.Query<KycUserSummary>(@"
                    SELECT u.Id AS UserId, u.Username, up.FullName, up.MobileNumber, r.RoleName,
                           ISNULL(k.UploadedCount, 0) AS UploadedCount,
                           ISNULL(k.PendingCount, 0)  AS PendingCount,
                           ISNULL(k.ApprovedCount, 0) AS ApprovedCount,
                           ISNULL(k.RejectedCount, 0) AS RejectedCount,
                           COALESCE(k.LastUpload, d.UpdatedDate, d.AddedDate) AS LastUpload,
                           d.AadhaarStatusId, d.PanStatusId
                    FROM [User] u
                    LEFT JOIN (
                        SELECT UserId,
                               COUNT(Id) AS UploadedCount,
                               SUM(CASE WHEN StatusId = @Pending  THEN 1 ELSE 0 END) AS PendingCount,
                               SUM(CASE WHEN StatusId = @Approved THEN 1 ELSE 0 END) AS ApprovedCount,
                               SUM(CASE WHEN StatusId = @Rejected THEN 1 ELSE 0 END) AS RejectedCount,
                               MAX(AddedbyDate) AS LastUpload
                        FROM UserKyc GROUP BY UserId
                    ) k ON k.UserId = u.Id
                    LEFT JOIN UserKycDetail d ON d.UserId = u.Id
                    LEFT JOIN UserProfile up  ON up.UserId = u.Id
                    LEFT JOIN [Role] r        ON r.Id = u.RoleId
                    WHERE u.IsDeleted = 0 AND (k.UserId IS NOT NULL OR d.UserId IS NOT NULL)
                      AND (@UserId IS NULL OR u.Id = @UserId)
                    ORDER BY COALESCE(k.LastUpload, d.UpdatedDate, d.AddedDate) DESC",
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
