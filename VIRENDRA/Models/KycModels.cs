using System;
using System.Collections.Generic;
using System.Linq;

namespace VIRENDRA.Models
{
    /// <summary>UserKyc.StatusId values and the 4 required document categories.</summary>
    public static class KycConstants
    {
        // UserKyc.StatusId (no row = not uploaded yet)
        public const int Pending  = 1;
        public const int Approved = 2;
        public const int Rejected = 3;

        // UserKyc.Category
        public const string AadhaarFront = "AadhaarFront";
        public const string AadhaarBack  = "AadhaarBack";
        public const string PanCard      = "PanCard";
        public const string Selfie       = "Selfie";

        public static readonly string[] Categories = { AadhaarFront, AadhaarBack, PanCard, Selfie };

        public const int    MaxFileBytes      = 2 * 1024 * 1024;   // 2 MB
        public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };

        public static bool IsValidCategory(string category) => Categories.Contains(category);

        public static string CategoryTitle(string category)
        {
            switch (category)
            {
                case AadhaarFront: return "Aadhaar Card (Front)";
                case AadhaarBack:  return "Aadhaar Card (Back)";
                case PanCard:      return "PAN Card";
                case Selfie:       return "Selfie / Photo";
                default:           return category;
            }
        }

        // UserKycDetail.*VerifyMode
        public const string ModeApi    = "API";
        public const string ModeManual = "Manual";

        /// <summary>Same as StatusName but "Verified" instead of "Approved" (Aadhaar / PAN numbers).</summary>
        public static string NumberStatusName(int? statusId) =>
            statusId == Approved ? "Verified" : statusId == null ? "Not Submitted" : StatusName(statusId);

        public static string StatusName(int? statusId)
        {
            switch (statusId)
            {
                case Pending:  return "Pending";
                case Approved: return "Approved";
                case Rejected: return "Rejected";
                default:       return "Not Uploaded";
            }
        }
    }

    /// <summary>A row of [UserKyc].</summary>
    public class UserKyc
    {
        public int      Id          { get; set; }
        public int      UserId      { get; set; }
        public string   Category    { get; set; }
        public string   ImgPath     { get; set; }
        public int      StatusId    { get; set; }
        public DateTime AddedbyDate { get; set; }
        public string   Remark      { get; set; }
        public int?     ActionById  { get; set; }
        public DateTime? ActionDate { get; set; }

        public bool IsPdf => ImgPath != null && ImgPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The logged-in user's KYC page: one slot per required category.</summary>
    public class KycStatusViewModel
    {
        public int UserId { get; set; }
        public List<UserKyc> Documents { get; set; } = new List<UserKyc>();

        /// <summary>Aadhaar / PAN numbers ([UserKycDetail]); null until the user submits one.</summary>
        public UserKycDetail Detail { get; set; }

        public UserKyc Get(string category) => Documents.FirstOrDefault(d => d.Category == category);

        public bool DocsApproved    => KycConstants.Categories.All(c => Get(c)?.StatusId == KycConstants.Approved);
        public bool AadhaarVerified => Detail?.AadhaarStatusId == KycConstants.Approved;
        public bool PanVerified     => Detail?.PanStatusId == KycConstants.Approved;

        /// <summary>Portal access: all 4 documents approved and both numbers verified.</summary>
        public bool IsApproved => DocsApproved && AadhaarVerified && PanVerified;

        public int  UploadedCount => KycConstants.Categories.Count(c => Get(c) != null);
        public int  ApprovedCount => KycConstants.Categories.Count(c => Get(c)?.StatusId == KycConstants.Approved);
        public bool AnyRejected   => Documents.Any(d => d.StatusId == KycConstants.Rejected)
                                     || Detail?.AadhaarStatusId == KycConstants.Rejected
                                     || Detail?.PanStatusId == KycConstants.Rejected;

        /// <summary>Steps done out of 6 (4 documents + Aadhaar + PAN), for the progress bar.</summary>
        public int VerifiedSteps => ApprovedCount + (AadhaarVerified ? 1 : 0) + (PanVerified ? 1 : 0);
        public int TotalSteps    => KycConstants.Categories.Length + 2;

        public bool AllSubmitted => UploadedCount == KycConstants.Categories.Length
                                    && !string.IsNullOrEmpty(Detail?.AadhaarNumber)
                                    && !string.IsNullOrEmpty(Detail?.PanNumber);

        public string OverallStatus =>
            IsApproved ? "Approved"
            : AnyRejected ? "Rejected"
            : AllSubmitted ? "Under Review"
            : "Incomplete";
    }

    /// <summary>A row of [UserKycDetail]: Aadhaar and PAN numbers with their verification.</summary>
    public class UserKycDetail
    {
        public int       UserId              { get; set; }

        public string    AadhaarNumber       { get; set; }
        public int?      AadhaarStatusId     { get; set; }
        public string    AadhaarVerifyMode   { get; set; }
        public string    AadhaarRemark       { get; set; }
        public DateTime? AadhaarVerifiedDate { get; set; }

        public string    PanNumber           { get; set; }
        public string    PanName             { get; set; }
        public DateTime? PanDob              { get; set; }
        public int?      PanStatusId         { get; set; }
        public string    PanVerifyMode       { get; set; }
        public string    PanRemark           { get; set; }
        public DateTime? PanVerifiedDate     { get; set; }

        public int?      ActionById          { get; set; }
        public DateTime  AddedDate           { get; set; }
        public DateTime? UpdatedDate         { get; set; }

        /// <summary>"XXXX XXXX 1234" — shown to the user / app instead of the full number.</summary>
        public string AadhaarMasked =>
            string.IsNullOrEmpty(AadhaarNumber) || AadhaarNumber.Length < 4
                ? AadhaarNumber
                : "XXXX XXXX " + AadhaarNumber.Substring(AadhaarNumber.Length - 4);

        public string AadhaarFormatted =>
            string.IsNullOrEmpty(AadhaarNumber) || AadhaarNumber.Length != 12
                ? AadhaarNumber
                : AadhaarNumber.Substring(0, 4) + " " + AadhaarNumber.Substring(4, 4) + " " + AadhaarNumber.Substring(8);
    }

    /// <summary>A user row on the admin KYC approval list.</summary>
    public class KycUserSummary
    {
        public int      UserId        { get; set; }
        public string   Username      { get; set; }
        public string   FullName      { get; set; }
        public string   MobileNumber  { get; set; }
        public string   RoleName      { get; set; }
        public int      UploadedCount { get; set; }
        public int      PendingCount  { get; set; }
        public int      ApprovedCount { get; set; }
        public int      RejectedCount { get; set; }
        public DateTime LastUpload    { get; set; }
        public int?     AadhaarStatusId { get; set; }
        public int?     PanStatusId     { get; set; }

        /// <summary>Overall status for filtering: Approved only when all 4 documents and both numbers are verified.</summary>
        public string OverallStatus =>
            ApprovedCount == KycConstants.Categories.Length
                && AadhaarStatusId == KycConstants.Approved && PanStatusId == KycConstants.Approved ? "Approved"
            : RejectedCount > 0 || AadhaarStatusId == KycConstants.Rejected || PanStatusId == KycConstants.Rejected ? "Rejected"
            : PendingCount > 0 || AadhaarStatusId == KycConstants.Pending || PanStatusId == KycConstants.Pending ? "Pending"
            : "Incomplete";
    }

    public class KycAdminIndexViewModel
    {
        public string Status { get; set; }
        public List<KycUserSummary> Users { get; set; } = new List<KycUserSummary>();
        public int PendingTotal  { get; set; }
        public int ApprovedTotal { get; set; }
        public int RejectedTotal { get; set; }
    }

    public class KycAdminReviewViewModel
    {
        public KycUserSummary User { get; set; }
        public KycStatusViewModel Kyc { get; set; }
    }
}
