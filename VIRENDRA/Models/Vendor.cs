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
        public int? StatusCheckTime { get; set; }
        public decimal ApiBal           { get; set; }
        public decimal ActualBal { get; set; }
        public int?    ApiTypeId         { get; set; }
        public string  CallbackId        { get; set; }

        public List<ApiUrl> ApiUrls { get; set; } = new List<ApiUrl>();
    }

    // Matches real DB table: TagValue — how to read one value out of a vendor's response:
    // text between PreTxt (+PreMargin chars) and PostText (+PostMargin chars), optionally split by
    // ResSeparator and taken at TagIndex; CompareTxt = value that means this tag (e.g. "1" for SUCCESS).
    public class TagValueRow
    {
        public int    Id           { get; set; }
        public int    TagId        { get; set; }
        public string TagName      { get; set; }   // JOIN-populated: Tag.Name
        public string CompareTxt   { get; set; }
        public string PreTxt       { get; set; }
        public string PostText     { get; set; }
        public int?   PreMargin    { get; set; }
        public int?   PostMargin   { get; set; }
        public string TagMsg       { get; set; }
        public string ResSeparator { get; set; }
        public int?   TagIndex     { get; set; }
    }

    public class ResponseSettingViewModel
    {
        public int    UrlId   { get; set; }
        public int    ApiId   { get; set; }
        public string ApiName { get; set; }
        public string UrlType { get; set; }
        public string URL     { get; set; }
        public string ResType { get; set; }

        public List<ApiTag>      Tags { get; set; } = new List<ApiTag>();
        public List<TagValueRow> Rows { get; set; } = new List<TagValueRow>();
    }

    // Matches real DB table: Tag
    public class ApiTag
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
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
