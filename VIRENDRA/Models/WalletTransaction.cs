using System;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    public class WalletTransaction
    {
        public int Id { get; set; }

        [Required]
        public string WalletType { get; set; }   // "Main" | "BillPayment"

        [Required]
        public int UserId { get; set; }
        public string UserName { get; set; }      // JOIN-populated

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        public bool IsPullOut { get; set; }
        public bool IsCredit { get; set; }
        public bool IsDebit { get; set; }
        public bool SMS { get; set; }

        [Required]
        public string TransferType { get; set; }

        public DateTime? PaymentDate { get; set; }
        public string PaymentRemark { get; set; }

        public int? BankAccountId { get; set; }
        public string BankAccountName { get; set; } // JOIN-populated

        [Required]
        public string ChequeRefNo { get; set; }

        [Required]
        public string Remark { get; set; }

        public DateTime AddedDate { get; set; }
        public int? AddedBy { get; set; }
    }

    public class BankAccount
    {
        public int Id { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        public string BankName { get; set; }
        public bool IsActive { get; set; }
    }
}
