using System;

namespace VIRENDRA.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public byte? RoleId { get; set; }
        public string TokenAPI { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public bool IsDeleted { get; set; }
        public int RetryCount { get; set; }
        public string OTP { get; set; }
        public string PassCode { get; set; }
        public string LoginIP { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? AddedById { get; set; }
        public int? UpdatedById { get; set; }
        public string CallbackURL { get; set; }
        public string ResetCode { get; set; }
        public int? PackageId { get; set; }
        public Guid? HKey { get; set; }
        public Guid? HPass { get; set; }
        public decimal? UserBal { get; set; }
        public int? ParentID { get; set; }
        public string UserPin { get; set; }
        public string AppToken { get; set; }
        public string Firebasetoken { get; set; }
        public string ComplainCallbackURL { get; set; }
        public decimal? UserOutStandingBal { get; set; }
        public bool IsComm { get; set; }
        public bool IsOtpCheck { get; set; }
        public bool? IsJioActiveHigh { get; set; }
    }
}
