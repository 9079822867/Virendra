using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    // Matches real DB table: ApiSource
    public class ApiSource
    {
        public int Id { get; set; }

        [Required]
        public string ApiName { get; set; }

        public string  ApiUserId         { get; set; }
        public string  ApiPassword       { get; set; }
        public string  Remark            { get; set; }
        public bool    IsActive          { get; set; }
        public DateTime? AddedDate       { get; set; }
        public int?    AddedById         { get; set; }
        public bool    IsAutoStatusCheck { get; set; }
        public int?    CheckTime         { get; set; }
        public decimal Balance           { get; set; }
        public decimal VBal              { get; set; }
        public int?    ApiTypeId         { get; set; }

        public List<ApiUrl> ApiUrls { get; set; } = new List<ApiUrl>();
    }

    // Matches real DB table: ApiUrl
    public class ApiUrl
    {
        public int     Id         { get; set; }
        public int     ApiId      { get; set; }
        public int?    UrlTypeId  { get; set; }
        public string  UrlType    { get; set; }   // JOIN-populated: ApiUrlType.TypeName
        public string  URL        { get; set; }
        public string  Method     { get; set; }   // GET | POST
        public string  ResType    { get; set; }
        public string  PostData   { get; set; }
        public DateTime? AddedDate { get; set; }
        public int?    AddedById  { get; set; }
        public bool    IsActive   { get; set; }
    }
}
