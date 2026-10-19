using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IComplaintRepository
    {
        /// <summary>RecID may be Recharge.Id, OurRefTxnId or UserTxnId. userId = null searches all users (admin).</summary>
        ComplaintRecharge FindRecharge(string recRef, int? userId);
        bool              ComplaintExists(long recId);
        long              InsertComplaint(long recId, int complaintById, string remark, byte statusId);
        void              UpdateApiResponse(long complaintId, string apiResponse);
        ComplaintApiUrl   GetComplainApiUrl(int apiId);

        ComplaintInfo     GetComplaint(long complaintId);
        void              ResolveComplaint(long complaintId, byte statusId, string comment, bool isRefund, int resolvedById);

        /// <summary>userId = null → all users (admin). Filters on ComplaintDate.</summary>
        List<ComplaintReportItem> GetComplaintReport(int? userId, DateTime fromDate, DateTime toDate,
            int? statusId, string searchText);
    }

    /// <summary>STC-style Complaint table (RecId, Remark, ComplaintDate, ComplaintById, StatusId, …).</summary>
    public class ComplaintRepository : IComplaintRepository
    {
        private readonly string _conn;

        public ComplaintRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        public ComplaintRecharge FindRecharge(string recRef, int? userId)
        {
            long.TryParse(recRef, out long recId);
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<ComplaintRecharge>(@"
                    SELECT TOP 1 Id, UserId, ApiId, StatusId, CustomerNo, UserTxnId, OurRefTxnId, ApiTxnId
                    FROM Recharge
                    WHERE (@UserId IS NULL OR UserId = @UserId)
                      AND (Id = @RecId OR OurRefTxnId = @Ref OR UserTxnId = @Ref)
                    ORDER BY Id DESC",
                    new { UserId = userId, RecId = recId, Ref = recRef });
        }

        public bool ComplaintExists(long recId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM Complaint WHERE RecId = @RecId", new { RecId = recId }) > 0;
        }

        public long InsertComplaint(long recId, int complaintById, string remark, byte statusId)
        {
            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<long>(@"
                    INSERT INTO Complaint
                        (RecId, Remark, ComplaintDate, ComplaintById, StatusId, IsRead, Priorty, IsRefund)
                    VALUES
                        (@RecId, @Remark, GETDATE(), @UserId, @StatusId, 0, 1, 0);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
                    new { RecId = recId, Remark = remark, UserId = complaintById, StatusId = statusId });
        }

        public void UpdateApiResponse(long complaintId, string apiResponse)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(
                    "UPDATE Complaint SET ApiResponse = @Resp, UpdatedDate = GETDATE() WHERE Id = @Id",
                    new { Id = complaintId, Resp = apiResponse });
        }

        public ComplaintApiUrl GetComplainApiUrl(int apiId)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<ComplaintApiUrl>(@"
                    SELECT TOP 1 u.URL AS Url, ISNULL(u.Method, 'GET') AS Method, u.PostData,
                           a.ApiUserId, a.ApiPassword, a.Remark
                    FROM ApiUrl u
                    INNER JOIN ApiUrlType t ON t.Id = u.UrlTypeId AND t.TypeName = 'ComplainRequest'
                    INNER JOIN ApiSource a  ON a.Id = u.ApiId
                    WHERE u.ApiId = @ApiId AND u.IsActive = 1 AND ISNULL(u.URL, '') <> ''",
                    new { ApiId = apiId });
        }

        public ComplaintInfo GetComplaint(long complaintId)
        {
            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<ComplaintInfo>(@"
                    SELECT cp.Id, ISNULL(cp.RecId, 0) AS RecId, ISNULL(cp.StatusId, 0) AS StatusId, cp.Remark,
                           r.UserId AS RechargeUserId, r.StatusId AS RechargeStatus,
                           r.CustomerNo, r.Amount
                    FROM Complaint cp
                    INNER JOIN Recharge r ON r.Id = cp.RecId
                    WHERE cp.Id = @Id",
                    new { Id = complaintId });
        }

        public void ResolveComplaint(long complaintId, byte statusId, string comment, bool isRefund, int resolvedById)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute(@"
                    UPDATE Complaint
                    SET StatusId     = @StatusId,
                        Comment      = @Comment,
                        IsRefund     = CASE WHEN @IsRefund = 1 THEN 1 ELSE IsRefund END,
                        ResolvedById = CASE WHEN @StatusId = @Resolved THEN @UserId ELSE ResolvedById END,
                        ResolvedDate = CASE WHEN @StatusId = @Resolved THEN GETDATE() ELSE ResolvedDate END,
                        UpdatedById  = @UserId,
                        UpdatedDate  = GETDATE(),
                        IsRead       = 1
                    WHERE Id = @Id",
                    new
                    {
                        Id = complaintId, StatusId = statusId, Comment = comment,
                        IsRefund = isRefund, UserId = resolvedById,
                        Resolved = ComplaintStatusCodes.RESOLVED
                    });
        }

        public List<ComplaintReportItem> GetComplaintReport(int? userId, DateTime fromDate, DateTime toDate,
            int? statusId, string searchText)
        {
            const string sql = @"
                SELECT cp.Id, ISNULL(cp.RecId, 0) AS RecId,
                       CASE WHEN cp.ComplaintById = r.UserId THEN cb.Username
                            ELSE ISNULL(cb.Username, 'Admin') END AS ComplaintBy,
                       cp.ComplaintDate, ISNULL(cp.StatusId, 0) AS StatusId,
                       cp.Remark, cp.Comment, ISNULL(cp.IsRefund, 0) AS IsRefund,
                       cp.ResolvedDate, rb.Username AS ResolvedBy, cp.ApiResponse,
                       r.OurRefTxnId, r.TxnId, r.StatusId AS RechargeStatusId, r.RequestTime,
                       r.CustomerNo, ISNULL(o.Name, '-') AS Operator, r.Amount,
                       a.ApiName, r.ApiTxnId
                FROM Complaint cp
                INNER JOIN Recharge r       ON r.Id  = cp.RecId
                LEFT JOIN [User] cb         ON cb.Id = cp.ComplaintById
                LEFT JOIN [User] rb         ON rb.Id = cp.ResolvedById
                LEFT JOIN [Operator] o      ON o.Id  = r.OpId
                LEFT JOIN ApiSource a       ON a.Id  = r.ApiId
                WHERE cp.ComplaintDate >= @FromDate
                  AND cp.ComplaintDate <  DATEADD(DAY, 1, @ToDate)
                  AND (@UserId   IS NULL OR r.UserId    = @UserId)
                  AND (@StatusId IS NULL OR cp.StatusId = @StatusId)
                  AND (@Search   IS NULL OR
                       r.CustomerNo  LIKE '%' + @Search + '%' OR
                       r.OurRefTxnId LIKE '%' + @Search + '%' OR
                       r.UserTxnId   LIKE '%' + @Search + '%' OR
                       CAST(cp.RecId AS VARCHAR(20)) = @Search)
                ORDER BY cp.ComplaintDate DESC";

            using (var c = new SqlConnection(_conn))
                return c.Query<ComplaintReportItem>(sql, new
                {
                    FromDate = fromDate.Date,
                    ToDate   = toDate.Date,
                    UserId   = userId,
                    StatusId = statusId,
                    Search   = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim()
                }).ToList();
        }
    }
}
