using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    /// <summary>
    /// Fund request raised by an API User / Retailer after paying into a company bank account.
    /// Saved in WalletRequest on Hold (StatusId 5); an admin approves it from Payment Requests,
    /// which credits the wallet through sp_AddUserWallet.
    /// </summary>
    public class PaymentRequestModel
    {
        [Required(ErrorMessage = "Enter the amount you paid")]
        [Range(1, 1000000, ErrorMessage = "Amount must be between ₹1 and ₹10,00,000")]
        [Display(Name = "Amount paid")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Choose the account you paid into")]
        [Display(Name = "Paid into")]
        public int? BankAccountId { get; set; }

        [Required(ErrorMessage = "Choose how you paid")]
        [Display(Name = "Payment mode")]
        public int? TrTypeId { get; set; }

        [Required(ErrorMessage = "Enter the UTR / reference number")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "Reference must be 4–50 characters")]
        [RegularExpression(@"^[A-Za-z0-9\-/]+$", ErrorMessage = "Use letters, numbers, - or / only")]
        [Display(Name = "UTR / Reference no")]
        public string Chequeno { get; set; }

        [Required(ErrorMessage = "Enter the payment date")]
        [DataType(DataType.Date)]
        [Display(Name = "Payment date")]
        public DateTime? PaymentDate { get; set; }

        [StringLength(200)]
        [Display(Name = "Note for admin")]
        public string PaymentRemark { get; set; }
    }

    /// <summary>Company account a user can pay into (BankAccount with BlockUser set = active).</summary>
    public class DepositAccount
    {
        public int    Id         { get; set; }
        public string BankName   { get; set; }
        public string HolderName { get; set; }
        public string AccountNo  { get; set; }
        public string IFSCCode   { get; set; }
        public string UpiAdress  { get; set; }
        public string BranchName { get; set; }
    }

    public class PaymentRequestRow
    {
        public long      Id            { get; set; }
        public decimal   Amount        { get; set; }
        public int?      StatusId      { get; set; }
        public string    Chequeno      { get; set; }
        public string    TransferType  { get; set; }
        public string    BankName      { get; set; }
        public string    PaymentRemark { get; set; }
        public string    Comment       { get; set; }   // admin's note on approve / reject
        public DateTime? PaymentDate   { get; set; }
        public DateTime  AddedDate     { get; set; }
        public DateTime? UpdatedDate   { get; set; }

        // StatusType: 3 pending, 5 Hold, 6 Approved, 7 Reject
        public string Status => StatusId == 6 ? "Approved" : StatusId == 7 ? "Rejected" : "Pending";
    }

    public class PaymentRequestListViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate   { get; set; }
        public string   Status   { get; set; }        // Pending | Approved | Rejected | null = all
        public List<PaymentRequestRow> Items { get; set; } = new List<PaymentRequestRow>();

        public int     PendingCount   { get; set; }
        public decimal PendingAmount  { get; set; }
        public int     ApprovedCount  { get; set; }
        public decimal ApprovedAmount { get; set; }
        public int     RejectedCount  { get; set; }
    }
}
