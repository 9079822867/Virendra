using System;

namespace VIRENDRA.Models
{
    public class PackageComm
    {
        public int Id { get; set; }

        // DB column: PackId  (aliased AS PackageId in queries)
        public int PackageId { get; set; }

        // DB column: OpId  (aliased AS OperatorId in queries)
        public int OperatorId { get; set; }

        // DB column: CommAmt  (aliased AS CommValue in queries)
        public decimal CommValue { get; set; }

        // Derived from CommTypeId: "P"=1 (Percent), "F"=2 (Flat)
        public string CommType { get; set; }

        // UI-only convenience flag (not stored in DB)
        public bool IsActive { get; set; } = true;

        // JOIN-populated from Operator table
        public string PackageName  { get; set; }
        public string OperatorName { get; set; }
        public string OperatorCode { get; set; }
        public string OperatorType { get; set; }

        // Real DB columns
        public decimal CommAmt    { get; set; }
        public int?    AmtTypeId  { get; set; }
        public int?    CommTypeId { get; set; }
        public bool    IsCirclePack { get; set; }
        public int?    DailyLimit  { get; set; }
        public int?    UsedLimit   { get; set; }
        public DateTime? AddedDate   { get; set; }
        public int?    AddedById   { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int?    UpdatedById  { get; set; }
    }
}
