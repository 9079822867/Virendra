using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public class PackageRepository : IPackageRepository
    {
        private readonly string _conn;

        public PackageRepository()
        {
            _conn = ConfigurationManager.ConnectionStrings["sqlconn"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("sqlconn missing in Web.config");
        }

        // ── Package ──────────────────────────────────────────
        public List<Package> GetAllPackages()
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                return db.Query<Package>(
                    "SELECT Id, PackageName, Description, IsActive, AddedDate FROM [Package] ORDER BY PackageName"
                ).ToList();
            }
        }

        public Package GetPackageById(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                return db.QueryFirstOrDefault<Package>(
                    "SELECT Id, PackageName, Description, IsActive, AddedDate FROM [Package] WHERE Id = @Id",
                    new { Id = id });
            }
        }

        public void CreatePackage(Package p)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute(
                    "INSERT INTO [Package] (PackageName, Description, IsActive, AddedDate) VALUES (@PackageName, @Description, @IsActive, GETDATE())",
                    p);
            }
        }

        public void UpdatePackage(Package p)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute(
                    "UPDATE [Package] SET PackageName=@PackageName, Description=@Description, IsActive=@IsActive WHERE Id=@Id",
                    p);
            }
        }

        public void DeletePackage(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute("UPDATE [Package] SET IsActive=0 WHERE Id=@Id", new { Id = id });
            }
        }

        // ── Operator ─────────────────────────────────────────
        public List<Operator> GetAllOperators()
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                return db.Query<Operator>(
                    "SELECT Id, OperatorName, OperatorCode, OperatorType, IsActive, AddedDate FROM [Operator] ORDER BY OperatorType, OperatorName"
                ).ToList();
            }
        }

        public Operator GetOperatorById(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                return db.QueryFirstOrDefault<Operator>(
                    "SELECT Id, OperatorName, OperatorCode, OperatorType, IsActive, AddedDate FROM [Operator] WHERE Id=@Id",
                    new { Id = id });
            }
        }

        public void CreateOperator(Operator op)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute(
                    "INSERT INTO [Operator] (OperatorName, OperatorCode, OperatorType, IsActive, AddedDate) VALUES (@OperatorName, @OperatorCode, @OperatorType, @IsActive, GETDATE())",
                    op);
            }
        }

        public void UpdateOperator(Operator op)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute(
                    "UPDATE [Operator] SET OperatorName=@OperatorName, OperatorCode=@OperatorCode, OperatorType=@OperatorType, IsActive=@IsActive WHERE Id=@Id",
                    op);
            }
        }

        public void DeleteOperator(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                db.Execute("UPDATE [Operator] SET IsActive=0 WHERE Id=@Id", new { Id = id });
            }
        }

        // ── PackageComm ───────────────────────────────────────
        public List<PackageComm> GetCommissionsByPackage(int packageId)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                string sql = @"
                    SELECT o.Id AS OperatorId, o.OperatorName, o.OperatorCode, o.OperatorType,
                           @PackageId AS PackageId,
                           ISNULL(pc.Id, 0)          AS Id,
                           ISNULL(pc.CommType, 'P')  AS CommType,
                           ISNULL(pc.CommValue, 0)   AS CommValue,
                           ISNULL(pc.IsActive, 1)    AS IsActive
                    FROM [Operator] o
                    LEFT JOIN [PackageComm] pc ON pc.OperatorId = o.Id AND pc.PackageId = @PackageId
                    WHERE o.IsActive = 1
                    ORDER BY o.OperatorType, o.OperatorName";
                return db.Query<PackageComm>(sql, new { PackageId = packageId }).ToList();
            }
        }

        public PackageComm GetCommission(int packageId, int operatorId)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                return db.QueryFirstOrDefault<PackageComm>(
                    "SELECT * FROM [PackageComm] WHERE PackageId=@PackageId AND OperatorId=@OperatorId",
                    new { PackageId = packageId, OperatorId = operatorId });
            }
        }

        public void SaveCommission(PackageComm comm)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                string sql = @"
                    IF EXISTS (SELECT 1 FROM [PackageComm] WHERE PackageId=@PackageId AND OperatorId=@OperatorId)
                        UPDATE [PackageComm]
                        SET CommType=@CommType, CommValue=@CommValue, IsActive=@IsActive
                        WHERE PackageId=@PackageId AND OperatorId=@OperatorId
                    ELSE
                        INSERT INTO [PackageComm] (PackageId, OperatorId, CommType, CommValue, IsActive)
                        VALUES (@PackageId, @OperatorId, @CommType, @CommValue, @IsActive)";
                db.Execute(sql, comm);
            }
        }

        public List<PackageComm> GetCommissionsByUser(int userId)
        {
            using (IDbConnection db = new SqlConnection(_conn))
            {
                string sql = @"
                    SELECT o.Id AS OperatorId, o.OperatorName, o.OperatorCode, o.OperatorType,
                           u.PackageId,
                           ISNULL(pc.Id, 0)         AS Id,
                           ISNULL(pc.CommType,'P')  AS CommType,
                           ISNULL(pc.CommValue, 0)  AS CommValue,
                           ISNULL(pc.IsActive, 1)   AS IsActive,
                           p.PackageName
                    FROM [User] u
                    JOIN [Package] p ON p.Id = u.PackageId
                    CROSS JOIN [Operator] o
                    LEFT JOIN [PackageComm] pc ON pc.PackageId = u.PackageId AND pc.OperatorId = o.Id
                    WHERE u.Id = @UserId AND o.IsActive = 1
                    ORDER BY o.OperatorType, o.OperatorName";
                return db.Query<PackageComm>(sql, new { UserId = userId }).ToList();
            }
        }
    }
}
