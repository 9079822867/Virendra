using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class CommonRoutingRepository : ICommonRoutingRepository
    {
        private readonly string _conn;

        public CommonRoutingRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        public CommonRoutingRepository(string connectionString)
        {
            _conn = connectionString;
        }

        public List<CommonRouting> GetAll()
        {
            const string sql = @"
                SELECT cr.Id, cr.OpId, cr.CircleFilter, cr.ApiId,
                       cr.Priority, cr.WaitMinute, cr.AmountFilter, cr.UserFilter,
                       cr.FTypeId, cr.LapuFilter, cr.Optional1, cr.MinRO,
                       cr.BlockUser, cr.RouteOP1, cr.RouteOP2, cr.IsActive,
                       cr.AddedDate, cr.AddedById, cr.UpdatedDate, cr.UpdatedById,
                       o.Name  AS OperatorName,
                       a.ApiName AS VendorName
                FROM CommanRouting cr
                LEFT JOIN [Operator]  o ON o.Id = cr.OpId
                LEFT JOIN ApiSource   a ON a.Id = cr.ApiId
                ORDER BY cr.Priority, cr.Id";

            using (var c = new SqlConnection(_conn))
                return c.Query<CommonRouting>(sql).ToList();
        }

        public CommonRouting GetById(int id)
        {
            const string sql = @"
                SELECT cr.Id, cr.OpId, cr.CircleFilter, cr.ApiId,
                       cr.Priority, cr.WaitMinute, cr.AmountFilter, cr.UserFilter,
                       cr.FTypeId, cr.LapuFilter, cr.Optional1, cr.MinRO,
                       cr.BlockUser, cr.RouteOP1, cr.RouteOP2, cr.IsActive,
                       cr.AddedDate, cr.AddedById, cr.UpdatedDate, cr.UpdatedById,
                       o.Name  AS OperatorName,
                       a.ApiName AS VendorName
                FROM CommanRouting cr
                LEFT JOIN [Operator]  o ON o.Id = cr.OpId
                LEFT JOIN ApiSource   a ON a.Id = cr.ApiId
                WHERE cr.Id = @Id";

            using (var c = new SqlConnection(_conn))
                return c.QueryFirstOrDefault<CommonRouting>(sql, new { Id = id });
        }

        public int Create(CommonRouting m)
        {
            const string sql = @"
                INSERT INTO CommanRouting
                    (OpId, CircleFilter, ApiId, Priority, WaitMinute,
                     AmountFilter, UserFilter, FTypeId, LapuFilter, Optional1,
                     MinRO, BlockUser, RouteOP1, RouteOP2, IsActive, AddedById)
                VALUES
                    (@OpId, @CircleFilter, @ApiId, @Priority, @WaitMinute,
                     @AmountFilter, @UserFilter, @FTypeId, @LapuFilter, @Optional1,
                     @MinRO, @BlockUser, @RouteOP1, @RouteOP2, @IsActive, @AddedById);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var c = new SqlConnection(_conn))
                return c.ExecuteScalar<int>(sql, m);
        }

        public void Update(CommonRouting m)
        {
            const string sql = @"
                UPDATE CommanRouting SET
                    OpId          = @OpId,
                    CircleFilter  = @CircleFilter,
                    ApiId         = @ApiId,
                    Priority      = @Priority,
                    WaitMinute    = @WaitMinute,
                    AmountFilter  = @AmountFilter,
                    UserFilter    = @UserFilter,
                    FTypeId       = @FTypeId,
                    LapuFilter    = @LapuFilter,
                    Optional1     = @Optional1,
                    MinRO         = @MinRO,
                    BlockUser     = @BlockUser,
                    RouteOP1      = @RouteOP1,
                    RouteOP2      = @RouteOP2,
                    IsActive      = @IsActive,
                    UpdatedById   = @UpdatedById,
                    UpdatedDate   = GETDATE()
                WHERE Id = @Id";

            using (var c = new SqlConnection(_conn))
                c.Execute(sql, m);
        }

        public void Delete(int id)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute("DELETE FROM CommanRouting WHERE Id = @Id", new { Id = id });
        }

        public void ToggleActive(int id, bool isActive)
        {
            using (var c = new SqlConnection(_conn))
                c.Execute("UPDATE CommanRouting SET IsActive = @IsActive WHERE Id = @Id",
                    new { Id = id, IsActive = isActive });
        }
    }
}
