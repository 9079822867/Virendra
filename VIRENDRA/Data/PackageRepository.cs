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

        // ── Package ──────────────────────────────────────────────────────────

        public List<Package> GetAllPackages()
        {
            using (IDbConnection db = new SqlConnection(_conn))
                return db.Query<Package>(
                    "SELECT Id, PackageName, DefaultComm, LockAmount, PTypeId, AddedDate FROM [Package] ORDER BY PackageName"
                ).ToList();
        }

        public Package GetPackageById(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                return db.QueryFirstOrDefault<Package>(
                    "SELECT Id, PackageName, DefaultComm, LockAmount, PTypeId, AddedDate FROM [Package] WHERE Id = @Id",
                    new { Id = id });
        }

        public void CreatePackage(Package p)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute(
                    "INSERT INTO [Package] (PackageName, DefaultComm, LockAmount, AddedDate) VALUES (@PackageName, @DefaultComm, @LockAmount, GETDATE())",
                    p);
        }

        public void UpdatePackage(Package p)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute(
                    "UPDATE [Package] SET PackageName=@PackageName, DefaultComm=@DefaultComm, LockAmount=@LockAmount WHERE Id=@Id",
                    p);
        }

        public void DeletePackage(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute("DELETE FROM [Package] WHERE Id=@Id", new { Id = id });
        }

        // ── Operator ─────────────────────────────────────────────────────────
        // DB columns: Name (as OperatorName), OpCode (as OperatorCode)

        public List<Operator> GetAllOperators()
        {
            using (IDbConnection db = new SqlConnection(_conn))
                return db.Query<Operator>(
                    "SELECT Id, Name AS OperatorName, OpCode AS OperatorCode, IsActive, IsSwitch, API1_Id, API2_Id, API3_Id, OpTypeId, AddedDate FROM [Operator] ORDER BY Name"
                ).ToList();
        }

        public Operator GetOperatorById(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                return db.QueryFirstOrDefault<Operator>(
                    "SELECT Id, Name AS OperatorName, OpCode AS OperatorCode, IsActive, IsSwitch, API1_Id, API2_Id, API3_Id, OpTypeId, Validate_ApiId, IsPartial, IsFetch, AddedDate FROM [Operator] WHERE Id=@Id",
                    new { Id = id });
        }

        public void CreateOperator(Operator op)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute(
                    "INSERT INTO [Operator] (Name, OpCode, IsActive, AddedDate) VALUES (@OperatorName, @OperatorCode, @IsActive, GETDATE())",
                    op);
        }

        public void UpdateOperator(Operator op)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute(
                    "UPDATE [Operator] SET Name=@OperatorName, OpCode=@OperatorCode, IsActive=@IsActive WHERE Id=@Id",
                    op);
        }

        public void DeleteOperator(int id)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute("UPDATE [Operator] SET IsActive=0 WHERE Id=@Id", new { Id = id });
        }

        // ── PackageComm ───────────────────────────────────────────────────────
        // DB columns: PackId, OpId, CommAmt, CommTypeId (1=Percent/P, 2=Flat/F)

        public List<PackageComm> GetCommissionsByPackage(int packageId)
        {
            const string sql = @"
                SELECT
                    o.Id   AS OperatorId,
                    o.Name AS OperatorName,
                    o.OpCode AS OperatorCode,
                    @PackageId AS PackageId,
                    ISNULL(pc.Id, 0)       AS Id,
                    ISNULL(pc.CommAmt, 0)  AS CommValue,
                    CASE ISNULL(pc.CommTypeId, 1) WHEN 1 THEN 'P' ELSE 'F' END AS CommType,
                    1 AS IsActive
                FROM [Operator] o
                LEFT JOIN [PackageComm] pc ON pc.OpId = o.Id AND pc.PackId = @PackageId
                WHERE o.IsActive = 1
                ORDER BY o.Name";

            using (IDbConnection db = new SqlConnection(_conn))
                return db.Query<PackageComm>(sql, new { PackageId = packageId }).ToList();
        }

        public PackageComm GetCommission(int packageId, int operatorId)
        {
            using (IDbConnection db = new SqlConnection(_conn))
                return db.QueryFirstOrDefault<PackageComm>(
                    "SELECT Id, PackId AS PackageId, OpId AS OperatorId, CommAmt AS CommValue, CASE CommTypeId WHEN 1 THEN 'P' ELSE 'F' END AS CommType FROM [PackageComm] WHERE PackId=@PackageId AND OpId=@OperatorId",
                    new { PackageId = packageId, OperatorId = operatorId });
        }

        public void SaveCommission(PackageComm comm)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM [PackageComm] WHERE PackId=@PackageId AND OpId=@OperatorId)
                    UPDATE [PackageComm]
                    SET CommAmt=@CommValue,
                        CommTypeId=CASE WHEN @CommType='P' THEN 1 ELSE 2 END,
                        UpdatedDate=GETDATE()
                    WHERE PackId=@PackageId AND OpId=@OperatorId
                ELSE
                    INSERT INTO [PackageComm] (PackId, OpId, CommAmt, CommTypeId, AddedDate)
                    VALUES (@PackageId, @OperatorId, @CommValue, CASE WHEN @CommType='P' THEN 1 ELSE 2 END, GETDATE())";

            using (IDbConnection db = new SqlConnection(_conn))
                db.Execute(sql, comm);
        }

        public List<PackageComm> GetCommissionsByUser(int userId)
        {
            const string sql = @"
                SELECT
                    o.Id   AS OperatorId,
                    o.Name AS OperatorName,
                    o.OpCode AS OperatorCode,
                    u.PackageId,
                    ISNULL(pc.Id, 0)       AS Id,
                    ISNULL(pc.CommAmt, 0)  AS CommValue,
                    CASE ISNULL(pc.CommTypeId,'1') WHEN 1 THEN 'P' ELSE 'F' END AS CommType,
                    1 AS IsActive,
                    p.PackageName
                FROM [User] u
                JOIN [Package] p  ON p.Id = u.PackageId
                CROSS JOIN [Operator] o
                LEFT JOIN [PackageComm] pc ON pc.PackId = u.PackageId AND pc.OpId = o.Id
                WHERE u.Id = @UserId AND o.IsActive = 1
                ORDER BY o.Name";

            using (IDbConnection db = new SqlConnection(_conn))
                return db.Query<PackageComm>(sql, new { UserId = userId }).ToList();
        }
    }
}
