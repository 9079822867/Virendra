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

        public List<Vendor> GetAllVendors()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                return conn.Query<Vendor>(
                    "SELECT * FROM Vendor ORDER BY Id DESC"
                ).ToList();
            }
        }

        public Vendor GetVendorById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var vendor = conn.QueryFirstOrDefault<Vendor>(
                    "SELECT * FROM Vendor WHERE Id = @Id", new { Id = id });

                if (vendor != null)
                    vendor.VendorUrls = GetVendorUrls(id);

                return vendor;
            }
        }

        public int CreateVendor(Vendor vendor)
        {
            const string sql = @"
                INSERT INTO Vendor (VendorName, VendorType, LoginId, Password, Optional,
                    IsAutoStatusCheck, CheckTime, Balance, VBal, Remark, IsActive, AddedDate)
                VALUES (@VendorName, @VendorType, @LoginId, @Password, @Optional,
                    @IsAutoStatusCheck, @CheckTime, 0, 0, @Remark, @IsActive, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(_connectionString))
            {
                return conn.ExecuteScalar<int>(sql, vendor);
            }
        }

        public void UpdateVendor(Vendor vendor)
        {
            const string sql = @"
                UPDATE Vendor SET
                    VendorName = @VendorName, VendorType = @VendorType,
                    LoginId = @LoginId, Password = @Password, Optional = @Optional,
                    IsAutoStatusCheck = @IsAutoStatusCheck, CheckTime = @CheckTime,
                    Remark = @Remark, IsActive = @IsActive
                WHERE Id = @Id";

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Execute(sql, vendor);
            }
        }

        public void DeleteVendor(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Execute("DELETE FROM VendorUrl WHERE VendorId = @Id; DELETE FROM Vendor WHERE Id = @Id", new { Id = id });
            }
        }

        public void ToggleActive(int id, bool isActive)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Execute("UPDATE Vendor SET IsActive = @IsActive WHERE Id = @Id", new { Id = id, IsActive = isActive });
            }
        }

        public List<VendorUrl> GetVendorUrls(int vendorId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var existing = conn.Query<VendorUrl>(
                    "SELECT * FROM VendorUrl WHERE VendorId = @VendorId ORDER BY Id", new { VendorId = vendorId }
                ).ToList();

                // Ensure all 6 URL types are present
                var result = new List<VendorUrl>();
                foreach (var urlType in UrlTypes)
                {
                    var row = existing.FirstOrDefault(u => u.UrlType == urlType)
                        ?? new VendorUrl
                        {
                            VendorId = vendorId,
                            UrlType = urlType,
                            Method = "GET",
                            ResponseType = "JSON (application/json)"
                        };
                    result.Add(row);
                }
                return result;
            }
        }

        public void SaveVendorUrls(int vendorId, List<VendorUrl> urls)
        {
            const string upsert = @"
                IF EXISTS (SELECT 1 FROM VendorUrl WHERE VendorId = @VendorId AND UrlType = @UrlType)
                    UPDATE VendorUrl SET Url = @Url, Method = @Method, ResponseType = @ResponseType,
                        PostParameter = @PostParameter
                    WHERE VendorId = @VendorId AND UrlType = @UrlType
                ELSE
                    INSERT INTO VendorUrl (VendorId, UrlType, Url, Method, ResponseType, PostParameter)
                    VALUES (@VendorId, @UrlType, @Url, @Method, @ResponseType, @PostParameter)";

            using (var conn = new SqlConnection(_connectionString))
            {
                foreach (var url in urls)
                {
                    url.VendorId = vendorId;
                    conn.Execute(upsert, url);
                }
            }
        }
    }
}
