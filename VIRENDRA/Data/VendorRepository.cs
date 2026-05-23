using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class VendorRepository : IVendorRepository
    {
        private readonly string _connectionString;

        private static readonly string[] UrlTypes = new[]
        {
            "RechargeRequest", "BalanceCheck", "StatusCheck",
            "CallBack", "Validation", "ComplainRequest"
        };

        public VendorRepository()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn connection string is missing in Web.config");
        }

        public List<ApiSource> GetAllVendors()
        {
            using (var conn = new SqlConnection(_connectionString))
                return conn.Query<ApiSource>(
                    "SELECT Id, ApiName, ApiUserId, ApiPassword, Remark, IsActive, ApiBal, ActualBal, IsAutoStatusCheck, StatusCheckTime, ApiTypeId, AddedDate FROM ApiSource ORDER BY Id DESC"
                ).ToList();
        }

        public ApiSource GetVendorById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var src = conn.QueryFirstOrDefault<ApiSource>(
                    "SELECT Id, ApiName, ApiUserId, ApiPassword, Remark, IsActive, Balance, VBal, IsAutoStatusCheck, CheckTime, ApiTypeId, AddedDate FROM ApiSource WHERE Id = @Id",
                    new { Id = id });

                if (src != null)
                    src.ApiUrls = GetVendorUrls(id);

                return src;
            }
        }

        public int CreateVendor(ApiSource vendor)
        {
            const string sql = @"
                INSERT INTO ApiSource
                    (ApiName, ApiUserId, ApiPassword, Remark, IsActive,
                     IsAutoStatusCheck, CheckTime, Balance, VBal, AddedDate)
                VALUES
                    (@ApiName, @ApiUserId, @ApiPassword, @Remark, @IsActive,
                     @IsAutoStatusCheck, @CheckTime, 0, 0, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(_connectionString))
                return conn.ExecuteScalar<int>(sql, vendor);
        }

        public void UpdateVendor(ApiSource vendor)
        {
            const string sql = @"
                UPDATE ApiSource SET
                    ApiName=@ApiName, ApiUserId=@ApiUserId, ApiPassword=@ApiPassword,
                    Remark=@Remark, IsActive=@IsActive,
                    IsAutoStatusCheck=@IsAutoStatusCheck, CheckTime=@CheckTime
                WHERE Id=@Id";

            using (var conn = new SqlConnection(_connectionString))
                conn.Execute(sql, vendor);
        }

        public void DeleteVendor(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("DELETE FROM ApiUrl WHERE ApiId = @Id; DELETE FROM ApiSource WHERE Id = @Id", new { Id = id });
        }

        public void ToggleActive(int id, bool isActive)
        {
            using (var conn = new SqlConnection(_connectionString))
                conn.Execute("UPDATE ApiSource SET IsActive = @IsActive WHERE Id = @Id", new { Id = id, IsActive = isActive });
        }

        public List<ApiUrl> GetVendorUrls(int apiSourceId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var existing = conn.Query<ApiUrl>(@"
                    SELECT u.Id, u.ApiId, u.UrlTypeId, t.TypeName AS UrlType,
                           u.URL, u.Method, u.ResType, u.PostData
                    FROM ApiUrl u
                    JOIN ApiUrlType t ON t.Id = u.UrlTypeId
                    WHERE u.ApiId = @ApiId
                    ORDER BY u.UrlTypeId",
                    new { ApiId = apiSourceId }).ToList();

                var result = new List<ApiUrl>();
                foreach (var urlType in UrlTypes)
                {
                    var row = existing.FirstOrDefault(u => u.UrlType == urlType)
                        ?? new ApiUrl
                        {
                            ApiId    = apiSourceId,
                            UrlType  = urlType,
                            Method   = "GET",
                            ResType  = "JSON (application/json)",
                            IsActive = true
                        };
                    result.Add(row);
                }
                return result;
            }
        }

        public void SaveVendorUrls(int apiSourceId, List<ApiUrl> urls)
        {
            const string upsert = @"
                DECLARE @tid INT = (SELECT Id FROM ApiUrlType WHERE TypeName = @UrlType);
                IF @tid IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM ApiUrl WHERE ApiId=@ApiId AND UrlTypeId=@tid)
                        UPDATE ApiUrl
                        SET URL=@URL, Method=@Method, ResType=@ResType, PostData=@PostData, IsActive=1
                        WHERE ApiId=@ApiId AND UrlTypeId=@tid
                    ELSE
                        INSERT INTO ApiUrl (ApiId, UrlTypeId, URL, Method, ResType, PostData, IsActive, AddedDate)
                        VALUES (@ApiId, @tid, @URL, @Method, @ResType, @PostData, 1, GETDATE())
                END";

            using (var conn = new SqlConnection(_connectionString))
            {
                foreach (var url in urls)
                {
                    url.ApiId = apiSourceId;
                    conn.Execute(upsert, new
                    {
                        ApiId   = apiSourceId,
                        url.UrlType,
                        url.URL,
                        url.Method,
                        url.ResType,
                        url.PostData
                    });
                }
            }
        }
    }
}
