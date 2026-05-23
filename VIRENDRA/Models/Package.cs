using System;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    public class Package
    {
        public int Id { get; set; }

        [Required]
        public string PackageName { get; set; }

        public decimal DefaultComm { get; set; }
        public decimal LockAmount { get; set; }
        public int? PTypeId { get; set; }
        public DateTime? AddedDate { get; set; }
        public int? AddedById { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? UpdatedById { get; set; }
    }
}
