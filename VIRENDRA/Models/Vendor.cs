using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    public class Vendor
    {
        public int Id { get; set; }

        [Required]
        public string VendorName { get; set; }

        public string VendorType { get; set; }

        public string LoginId { get; set; }

        public string Password { get; set; }

        public string Optional { get; set; }

        public bool IsAutoStatusCheck { get; set; }

        public int CheckTime { get; set; }

        public decimal Balance { get; set; }

        public decimal VBal { get; set; }

        public string Remark { get; set; }

        public bool IsActive { get; set; }

        public DateTime AddedDate { get; set; }

        public List<VendorUrl> VendorUrls { get; set; } = new List<VendorUrl>();
    }

    public class VendorUrl
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public string UrlType { get; set; }
        public string Url { get; set; }
        public string Method { get; set; }      // GET | POST
        public string ResponseType { get; set; }
        public string PostParameter { get; set; }
    }
}
