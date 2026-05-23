using System;

namespace VIRENDRA.Models
{
    public class Operator
    {
        public int Id { get; set; }
        public string OperatorName { get; set; }
        public string OperatorCode { get; set; }
        public string OperatorType { get; set; }
        public bool IsActive { get; set; }
        public DateTime AddedDate { get; set; }
    }
}
