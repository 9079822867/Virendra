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
                    "SELECT Id, ApiName, ApiUserId, ApiPassword, Remark, IsActive, ApiBal, ActualBal, IsAutoStatusCheck, StatusCheckTime, ApiTypeId, AddedDate FROM ApiSource WHERE Id = @Id",
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
                     IsAutoStatusCheck, StatusCheckTime, ApiBal, ActualBal, AddedDate)
                VALUES
                    (@ApiName, @ApiUserId, @ApiPassword, @Remark, @IsActive,
                     @IsAutoStatusCheck, @StatusCheckTime, 0, 0, GETDATE());
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
                    IsAutoStatusCheck=@IsAutoStatusCheck, StatusCheckTime=@StatusCheckTime
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

        // ── Operator-code margin (vendor op-codes + commission) ──────────────

        public VendorMarginViewModel GetVendorMargins(int apiId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var vendor = conn.QueryFirstOrDefault(
                    "SELECT ApiName, ApiPackid FROM ApiSource WHERE Id = @Id",
                    new { Id = apiId });

                if (vendor == null) return null;

                int packId = vendor.ApiPackid == null ? 0 : (int)vendor.ApiPackid;

                var vm = new VendorMarginViewModel
                {
                    ApiId   = apiId,
                    ApiName = (string)vendor.ApiName,
                    PackId  = packId
                };

                // Every active operator, left-joined with its op-code + the vendor
                // package's commission row.
                const string sql = @"
                    SELECT
                        o.Id              AS OpId,
                        o.Name            AS OperatorName,
                        @ApiId            AS ApiId,
                        oc.OpCode         AS OpCode,
                        oc.ExtraUrl       AS ExtraUrl,
                        oc.ExtraUrlData   AS ExtraData,
                        ISNULL(pc.CommAmt, 0)    AS CommAmt,
                        ISNULL(pc.AmtTypeId, 5)  AS AmtTypeId,
                        ISNULL(pc.CommTypeId, 2) AS CommTypeId
                    FROM [Operator] o
                    LEFT JOIN OperatorCode oc ON oc.OpId = o.Id AND oc.ApiId = @ApiId
                    LEFT JOIN PackageComm  pc ON pc.OpId = o.Id AND pc.PackId = @PackId
                    WHERE o.IsActive = 1
                    ORDER BY o.Id";

                vm.Rows = conn.Query<OperatorMarginDto>(sql,
                    new { ApiId = apiId, PackId = packId }).ToList();

                return vm;
            }
        }

        public void SaveVendorMargins(int apiId, int userId, List<OperatorMarginDto> rows)
        {
            if (rows == null) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Ensure the vendor has a dedicated package (name = "{ApiName}-{ApiId}")
                        var vendor = conn.QueryFirstOrDefault(
                            "SELECT ApiName, ApiPackid FROM ApiSource WHERE Id = @Id",
                            new { Id = apiId }, tx);

                        string packName = ((string)vendor?.ApiName ?? "Vendor") + "-" + apiId;
                        int packId = (vendor?.ApiPackid == null) ? 0 : (int)vendor.ApiPackid;

                        if (packId > 0)
                        {
                            conn.Execute(@"
                                UPDATE [Package]
                                SET PackageName = @Name, UpdatedById = @Uid, UpdatedDate = GETDATE()
                                WHERE Id = @Id",
                                new { Name = packName, Uid = userId, Id = packId }, tx);
                        }
                        else
                        {
                            packId = conn.ExecuteScalar<int>(@"
                                INSERT INTO [Package]
                                    (PackageName, DefaultComm, LockAmount, PTypeId, AddedById, AddedDate)
                                VALUES
                                    (@Name, 0, 0, 1, @Uid, GETDATE());
                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                new { Name = packName, Uid = userId }, tx);

                            conn.Execute(
                                "UPDATE ApiSource SET ApiPackid = @PackId WHERE Id = @ApiId",
                                new { PackId = packId, ApiId = apiId }, tx);
                        }

                        // 2. Upsert each row
                        const string upsertComm = @"
                            IF EXISTS (SELECT 1 FROM PackageComm WHERE PackId=@PackId AND OpId=@OpId)
                                UPDATE PackageComm
                                SET CommAmt=@CommAmt, AmtTypeId=@AmtTypeId, CommTypeId=@CommTypeId,
                                    UpdatedById=@Uid, UpdatedDate=GETDATE()
                                WHERE PackId=@PackId AND OpId=@OpId
                            ELSE
                                INSERT INTO PackageComm
                                    (PackId, OpId, CommAmt, AmtTypeId, CommTypeId, AddedById, AddedDate)
                                VALUES
                                    (@PackId, @OpId, @CommAmt, @AmtTypeId, @CommTypeId, @Uid, GETDATE())";

                        const string upsertOpCode = @"
                            IF EXISTS (SELECT 1 FROM OperatorCode WHERE ApiId=@ApiId AND OpId=@OpId)
                                UPDATE OperatorCode
                                SET OpCode=@OpCode, ExtraUrl=@ExtraUrl, ExtraUrlData=@ExtraData,
                                    UpdatedById=@Uid, UpdatedDate=GETDATE()
                                WHERE ApiId=@ApiId AND OpId=@OpId
                            ELSE
                                INSERT INTO OperatorCode
                                    (OpId, ApiId, OpCode, ExtraUrl, ExtraUrlData, AddedById, AddedDate)
                                VALUES
                                    (@OpId, @ApiId, @OpCode, @ExtraUrl, @ExtraData, @Uid, GETDATE())";

                        foreach (var r in rows)
                        {
                            // Commission: save only when a positive amount is set
                            if (r.CommAmt > 0)
                            {
                                conn.Execute(upsertComm, new
                                {
                                    PackId     = packId,
                                    r.OpId,
                                    r.CommAmt,
                                    AmtTypeId  = r.AmtTypeId  > 0 ? r.AmtTypeId  : 5,
                                    CommTypeId = r.CommTypeId > 0 ? r.CommTypeId : 2,
                                    Uid        = userId
                                }, tx);
                            }

                            // Operator code: save only when a code is provided
                            if (!string.IsNullOrWhiteSpace(r.OpCode))
                            {
                                conn.Execute(upsertOpCode, new
                                {
                                    ApiId     = apiId,
                                    r.OpId,
                                    OpCode    = r.OpCode.Trim(),
                                    ExtraUrl  = string.IsNullOrWhiteSpace(r.ExtraUrl)  ? null : r.ExtraUrl.Trim(),
                                    ExtraData = string.IsNullOrWhiteSpace(r.ExtraData) ? null : r.ExtraData.Trim(),
                                    Uid       = userId
                                }, tx);
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
