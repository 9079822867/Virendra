using System;
using System.Collections.Generic;

namespace VIRENDRA.Models
{
    /// <summary>Complaint.StatusId values (StatusType rows marked "Complaint", same as STC).</summary>
    public static class ComplaintStatusCodes
    {
        public const byte PENDING    = 5;
        public const byte PROCESSING = 2;
        public const byte RESOLVED   = 8;

        public static string Name(int statusId)
            => statusId == PENDING    ? "Pending"
             : statusId == PROCESSING ? "Processing"
             : statusId == RESOLVED   ? "Resolved"
             : "Unknown";
    }

    public enum ComplaintBookStatus { Booked, NotFound, NotSuccess, Duplicate }

    public class ComplaintBookResult
    {
        public ComplaintBookStatus Status      { get; set; }
        public long                ComplaintId { get; set; }

        public string Message =>
              Status == ComplaintBookStatus.Booked     ? "Your Complain Successfully Submitted"
            : Status == ComplaintBookStatus.NotFound   ? "Recharge id not found, try again"
            : Status == ComplaintBookStatus.NotSuccess ? "Sorry, Transaction ID Failed or Recharge Not Success"
            : "Already complain done";
    }

    /// <summary>Recharge fields needed to book a complaint.</summary>
    public class ComplaintRecharge
    {
        public long   Id          { get; set; }
        public int    UserId      { get; set; }
        public int?   ApiId       { get; set; }
        public int    StatusId    { get; set; }
        public string CustomerNo  { get; set; }
        public string UserTxnId   { get; set; }
        public string OurRefTxnId { get; set; }
        public string ApiTxnId    { get; set; }
    }

    /// <summary>Vendor ComplainRequest URL + credentials.</summary>
    public class ComplaintApiUrl
    {
        public string Url         { get; set; }
        public string Method      { get; set; }
        public string PostData    { get; set; }
        public string ApiUserId   { get; set; }
        public string ApiPassword { get; set; }
        public string Remark      { get; set; }
    }

    /// <summary>Complaint row needed to resolve it.</summary>
    public class ComplaintInfo
    {
        public long   Id             { get; set; }
        public long   RecId          { get; set; }
        public byte   StatusId       { get; set; }
        public int    RechargeUserId { get; set; }
        public int    RechargeStatus { get; set; }
        public string CustomerNo     { get; set; }
        public decimal Amount        { get; set; }
        public string Remark         { get; set; }
    }

    public class ComplaintReportItem
    {
        public long      Id               { get; set; }
        public long      RecId            { get; set; }
        public string    ComplaintBy      { get; set; }
        public DateTime? ComplaintDate    { get; set; }
        public byte      StatusId         { get; set; }
        public string    Remark           { get; set; }
        public string    Comment          { get; set; }
        public bool      IsRefund         { get; set; }
        public DateTime? ResolvedDate     { get; set; }
        public string    ResolvedBy       { get; set; }
        public string    ApiResponse      { get; set; }
        public string    OurRefTxnId      { get; set; }
        public long?     TxnId            { get; set; }
        public int       RechargeStatusId { get; set; }
        public DateTime? RequestTime      { get; set; }
        public string    CustomerNo       { get; set; }
        public string    Operator         { get; set; }
        public decimal   Amount           { get; set; }
        public string    ApiName          { get; set; }
        public string    ApiTxnId         { get; set; }

        public string StatusName => ComplaintStatusCodes.Name(StatusId);
        public bool   IsOpen     => StatusId == ComplaintStatusCodes.PENDING || StatusId == ComplaintStatusCodes.PROCESSING;
    }

    public class ComplaintReportViewModel
    {
        public DateTime FromDate   { get; set; }
        public DateTime ToDate     { get; set; }
        public int?     StatusId   { get; set; }
        public string   SearchText { get; set; }
        public bool     IsAdmin    { get; set; }
        public List<ComplaintReportItem> Items { get; set; } = new List<ComplaintReportItem>();
    }
}
