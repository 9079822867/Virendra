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

        public UserKyc Get(string category) => Documents.FirstOrDefault(d => d.Category == category);

        public bool IsApproved => KycConstants.Categories.All(c => Get(c)?.StatusId == KycConstants.Approved);
        public int  UploadedCount => KycConstants.Categories.Count(c => Get(c) != null);
        public int  ApprovedCount => KycConstants.Categories.Count(c => Get(c)?.StatusId == KycConstants.Approved);
        public bool AnyRejected   => Documents.Any(d => d.StatusId == KycConstants.Rejected);

        public string OverallStatus =>
            IsApproved ? "Approved"
            : AnyRejected ? "Rejected"
            : UploadedCount == KycConstants.Categories.Length ? "Under Review"
            : "Incomplete";
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

        /// <summary>Overall status for filtering: Approved only when all 4 approved.</summary>
        public string OverallStatus =>
            ApprovedCount == KycConstants.Categories.Length ? "Approved"
            : RejectedCount > 0 ? "Rejected"
            : PendingCount > 0  ? "Pending"
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
