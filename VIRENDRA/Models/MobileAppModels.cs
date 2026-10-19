using System;
using System.Collections.Generic;

namespace VIRENDRA.Models
{
    // Request / response contracts for the mobile app API (App/webservice/*).
    // Field names follow the STC app contract so the same app can call this server.

    // ── Requests ─────────────────────────────────────────────────────────────

    public class AppBaseRequest
    {
        public string Version  { get; set; }
        public string Location { get; set; }
    }

    public class AppLoginRequest : AppBaseRequest
    {
        public string Name      { get; set; }   // username
        public string Pass      { get; set; }
        public string IMEI      { get; set; }
        public string FireToken { get; set; }
    }

    public class AppTokenRequest : AppBaseRequest
    {
        public string Tokenid { get; set; }
    }

    public class AppChangePassRequest : AppTokenRequest
    {
        public string OldPassword { get; set; }
        public string Password    { get; set; }
    }

    public class AppResendOtpRequest : AppBaseRequest
    {
        public string Mobileno { get; set; }   // username
    }

    public class AppForgetPassRequest : AppBaseRequest
    {
        public string Mobileno { get; set; }   // username
        public string OTP      { get; set; }
        public string Password { get; set; }
    }

    public class AppRechargeReportRequest : AppTokenRequest
    {
        public string Startdate { get; set; }
        public string Enddate   { get; set; }
        public string status    { get; set; }   // Success | Failed | Pending (blank = all)
        public string MobileNo  { get; set; }
    }

    public class AppTxnReportRequest : AppTokenRequest
    {
        public int    PageIndex { get; set; }   // 1-based
        public int    PageSize  { get; set; }
        public string Startdate { get; set; }
        public string Enddate   { get; set; }
    }

    public class AppComplainRequest : AppTokenRequest
    {
        public string RecID  { get; set; }      // Recharge.Id (or our / user txn id)
        public string Remark { get; set; }
    }

    public class AppRechargeRequest : AppBaseRequest
    {
        public string TokenID  { get; set; }
        public string RefTxnId { get; set; }
        public string MobileNo { get; set; }
        public string Operator { get; set; }   // Operator.Id
        public string CricleID { get; set; }   // STC spelling
        public string CircleId { get; set; }
        public string Amount   { get; set; }
        public string Pin      { get; set; }
    }

    public class AppOperatorRequest : AppTokenRequest
    {
        public string mode { get; set; }       // OperatorType.Id (blank = all)
    }

    public class AppSlabCommissionRequest : AppTokenRequest
    {
        public string Operator { get; set; }   // Operator.Id
    }

    public class AppDashboardRequest : AppTokenRequest
    {
        public string FormDate { get; set; }
        public string ToDate   { get; set; }
    }

    // ── Responses ────────────────────────────────────────────────────────────

    public class AppRechargeResponse
    {
        public string ERROR         { get; set; }
        public string STATUS        { get; set; }   // "Recharge SUCCESS" | "Recharge Under Processing" | "Recharge FAILED"
        public string STATUSCODE    { get; set; }   // 1 success, 2 processing, 3 failed
        public string MESSAGE       { get; set; }
        public string WBalance      { get; set; }
        public string RECID         { get; set; }
        public string TXNID         { get; set; }
        public string OPERATORTXNID { get; set; }
    }

    public class AppBalanceResponse
    {
        public string ERROR              { get; set; }
        public string STATUSCODE         { get; set; }
        public string MESSAGE            { get; set; }
        public string WBalance           { get; set; }   // User.UserBal
        public string OutStandingBalance { get; set; }   // User.UserOutStandingBal
    }

    public class AppOperatorResponse
    {
        public string ERROR      { get; set; }
        public string STATUSCODE { get; set; }
        public string MESSAGE    { get; set; }
        public List<AppOperatorRow> Operator { get; set; }
    }

    public class AppOperatorRow
    {
        public int    Id           { get; set; }
        public string OperatorName { get; set; }
        public string OperatorCode { get; set; }
        public int?   OpTypeId     { get; set; }
        public string OperatorType { get; set; }
    }

    public class AppDashboardResponse
    {
        public string ERROR          { get; set; }
        public string STATUSCODE     { get; set; }
        public string MESSAGE        { get; set; }
        public string Commission     { get; set; }   // today's earning
        public string OpeningBalance { get; set; }
        public string ClosingBalance { get; set; }
        public string Notification   { get; set; }
        public string TopUpAmount    { get; set; }   // today's credits
        public AppDashboardToday     TodayData    { get; set; }
        public List<AppRechargeRow>  LastRecharge { get; set; }
    }

    public class AppDashboardToday
    {
        public decimal SuccessAmount { get; set; }
        public decimal FailedAmount  { get; set; }
        public int     TotalCount    { get; set; }
        public int     SuccessCount  { get; set; }
        public int     FailedCount   { get; set; }
        public int     PendingCount  { get; set; }
    }

    public class AppUserLogResponse
    {
        public string ERROR        { get; set; }
        public string STATUSCODE   { get; set; }
        public string TOKENID      { get; set; }
        public string MESSAGE      { get; set; }
        public string USERID       { get; set; }
        public string MOBILENUMBER { get; set; }
        public string EMAIL        { get; set; }
        public string USERTYPE     { get; set; }
        public string ROLEID       { get; set; }
        public string SHOPNAME     { get; set; }
        public string BALANCE      { get; set; }
        public string OTPCheck     { get; set; }
        public string KYCAPPROVED  { get; set; }   // "True" | "False" (admins always True)
        public string KYCSTATUS    { get; set; }   // Approved | Under Review | Rejected | Incomplete
    }

    public class AppSignupResponse
    {
        public string Error   { get; set; }
        public string Status  { get; set; }
        public string Time    { get; set; }
        public string Message { get; set; }
    }

    public class AppReportResponse<T>
    {
        public string  ERROR      { get; set; }
        public string  STATUSCODE { get; set; }
        public string  MESSAGE    { get; set; }
        public int     TOTAL      { get; set; }
        public List<T> REPORT     { get; set; }
    }

    public class AppCommissionResponse
    {
        public string ERROR      { get; set; }
        public string STATUSCODE { get; set; }
        public string MESSAGE    { get; set; }
        public List<AppCommissionRow> Commissionlist { get; set; }
    }

    // ── Rows ─────────────────────────────────────────────────────────────────

    public class AppCommissionRow
    {
        public int     OperatorId   { get; set; }
        public string  OperatorName { get; set; }
        public string  OperatorCode { get; set; }
        public string  PackageName  { get; set; }
        public decimal Commission   { get; set; }
        public string  CommType     { get; set; }   // P = percent, F = flat
    }

    public class AppRechargeRow
    {
        public long     RecId         { get; set; }
        public string   TxnId         { get; set; }
        public string   OurRefTxnId   { get; set; }
        public string   OperatorTxnId { get; set; }
        public string   MobileNo      { get; set; }
        public string   Operator      { get; set; }
        public decimal  Amount        { get; set; }
        public decimal  Commission    { get; set; }
        public string   Status        { get; set; }
        public string   StatusMsg     { get; set; }
        public DateTime Date          { get; set; }
    }

    public class AppTxnRow
    {
        public string   TxnId       { get; set; }
        public string   Type        { get; set; }
        public string   Description { get; set; }
        public decimal  Credit      { get; set; }
        public decimal  Debit       { get; set; }
        public decimal  Balance     { get; set; }
        public DateTime Date        { get; set; }
    }

    // ── KYC ──────────────────────────────────────────────────────────────────

    /// <summary>KycDocument request; KycUpload sends the same fields plus the file.</summary>
    public class AppKycDocumentRequest : AppTokenRequest
    {
        public string Category { get; set; }        // AadhaarFront | AadhaarBack | PanCard | Selfie
    }

    /// <summary>JSON variant of KycUpload (multipart/form-data is also accepted).</summary>
    public class AppKycUploadRequest : AppKycDocumentRequest
    {
        public string FileName   { get; set; }      // e.g. "front.jpg" (extension decides the type)
        public string FileBase64 { get; set; }      // file content, base64 (data: prefix allowed)
    }

    public class AppKycAadhaarRequest : AppTokenRequest
    {
        public string AadhaarNumber { get; set; }   // 12 digits (spaces allowed)
    }

    public class AppKycPanRequest : AppTokenRequest
    {
        public string PanNumber    { get; set; }    // ABCDE1234F
        public string NameAsPerPan { get; set; }
        public string Dob          { get; set; }    // dd/MM/yyyy, dd-MM-yyyy or yyyy-MM-dd
        public string Consent      { get; set; }    // "Y" required
    }

    public class AppKycStatusResponse
    {
        public string ERROR       { get; set; }
        public string STATUSCODE  { get; set; }
        public string MESSAGE     { get; set; }
        public string KYCAPPROVED { get; set; }
        public string KYCSTATUS   { get; set; }
        public string PANAPI      { get; set; }     // "True" when PAN is verified instantly by API
        public AppKycNumber AADHAAR { get; set; }
        public AppKycNumber PAN     { get; set; }
        public List<AppKycDocument> DOCUMENTS { get; set; }
    }

    public class AppKycNumber
    {
        public string NUMBER    { get; set; }       // Aadhaar masked (XXXX XXXX 1234); PAN in full
        public string NAME      { get; set; }       // PAN only
        public string DOB       { get; set; }       // PAN only, dd/MM/yyyy
        public string STATUSID  { get; set; }       // 0 not submitted, 1 pending, 2 verified, 3 rejected
        public string STATUS    { get; set; }
        public string MODE      { get; set; }       // API | Manual (when verified)
        public string REMARK    { get; set; }       // rejection reason
        public string CANSUBMIT { get; set; }       // "True" unless already verified
    }

    public class AppKycDocument
    {
        public string CATEGORY   { get; set; }
        public string TITLE      { get; set; }
        public string STATUSID   { get; set; }      // 0 not uploaded, 1 pending, 2 approved, 3 rejected
        public string STATUS     { get; set; }
        public string REMARK     { get; set; }      // rejection reason
        public string UPLOADEDON { get; set; }
        public string CANUPLOAD  { get; set; }      // "True" unless already approved
    }
}
