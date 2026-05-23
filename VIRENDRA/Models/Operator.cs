using System;

namespace VIRENDRA.Models
{
    public class Operator
    {
        public int Id { get; set; }

        // DB column: Name  (aliased in queries as OperatorName)
        public string OperatorName { get; set; }

        // DB column: OpCode  (aliased in queries as OperatorCode)
        public string OperatorCode { get; set; }

        // Not a real DB column; populated via JOIN with OpType when available
        public string OperatorType { get; set; }

        public bool IsActive { get; set; }
        public bool IsSwitch { get; set; }
        public int? SwitchTypeId { get; set; }
        public int? API1_Id { get; set; }
        public int? API2_Id { get; set; }
        public int? API3_Id { get; set; }
        public DateTime? AddedDate { get; set; }
        public int? AddedById { get; set; }
        public int? OpTypeId { get; set; }
        public int? Validate_ApiId { get; set; }
        public bool IsPartial { get; set; }
        public bool IsFetch { get; set; }
        public int? ValidationLevel { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? UpdatedById { get; set; }
        public string operator_img { get; set; }
        public int? StateID { get; set; }
    }
}
