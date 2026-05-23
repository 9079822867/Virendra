using System;

namespace VIRENDRA.Models
{
    public class Package
    {
        public int Id { get; set; }
        public string PackageName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime AddedDate { get; set; }
    }
}
