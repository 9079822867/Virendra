using System.Collections.Generic;
using VIRENDRA.Models;

namespace VIRENDRA.Data
{
    public interface IPackageRepository
    {
        // Package
        List<Package> GetAllPackages();
        Package GetPackageById(int id);
        void CreatePackage(Package package);
        void UpdatePackage(Package package);
        void DeletePackage(int id);

        // Operator
        List<Operator> GetAllOperators();
        Operator GetOperatorById(int id);
        void CreateOperator(Operator op);
        void UpdateOperator(Operator op);
        void DeleteOperator(int id);

        // PackageComm
        List<PackageComm> GetCommissionsByPackage(int packageId);
        PackageComm GetCommission(int packageId, int operatorId);
        void SaveCommission(PackageComm comm);   // upsert
        List<PackageComm> GetCommissionsByUser(int userId);
    }
}
