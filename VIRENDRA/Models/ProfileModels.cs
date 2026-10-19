using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    /// <summary>My Profile page: [User] + [UserProfile] (keyed by UserId).</summary>
    public class UserProfileViewModel
    {
        // Read-only account info
        public int     Id          { get; set; }
        public string  Username    { get; set; }
        public string  RoleName    { get; set; }
        public string  PackageName { get; set; }
        public decimal Balance     { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string EmailID { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number")]
        [Display(Name = "Mobile No")]
        public string Mobileno { get; set; }

        [RegularExpression(@"^[0-9+\- ]{6,15}$", ErrorMessage = "Enter a valid phone number")]
        [Display(Name = "Alternate Contact No")]
        public string Contactno { get; set; }

        [StringLength(150)]
        [Display(Name = "Shop / Firm Name")]
        public string ORGName { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Za-z]{5}[0-9]{4}[A-Za-z][0-9A-Za-z]Z[0-9A-Za-z]$",
            ErrorMessage = "Enter a valid 15-character GSTIN")]
        [Display(Name = "GST No")]
        public string GSTNo { get; set; }

        [StringLength(300)]
        [Display(Name = "GST Registered Address")]
        public string GSTADDRESS { get; set; }

        [StringLength(300)]
        public string Address { get; set; }

        [Display(Name = "State")]
        public int? StateID { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter a valid 6-digit pincode")]
        public string Pincode { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Enter your current password")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Enter a new password")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm the new password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; }
    }
}
