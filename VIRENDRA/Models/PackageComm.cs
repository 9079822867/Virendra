namespace VIRENDRA.Models
{
    public class PackageComm
    {
        public int Id { get; set; }
        public int PackageId { get; set; }
        public int OperatorId { get; set; }
        public string CommType { get; set; }   // "P" = Percent, "F" = Flat
        public decimal CommValue { get; set; }
        public bool IsActive { get; set; }

        // Populated via JOIN
        public string PackageName { get; set; }
        public string OperatorName { get; set; }
        public string OperatorCode { get; set; }
        public string OperatorType { get; set; }
    }
}
