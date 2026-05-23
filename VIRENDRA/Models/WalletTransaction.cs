using System;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    // Matches real DB table: WalletRequest
    public class WalletRequest
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        public string UserName { get; set; }        // JOIN-populated

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        // Real DB columns
        public int?      TxnTypeId    { get; set; }   // 1=Credit, 2=Debit
        public int?      AmtTypeId    { get; set; }   // 1=Main,   2=BillPayment
        public int?      StatusId     { get; set; }   // 1=Pending, 2=Approved, 3=Rejected
        public long?     TxnId        { get; set; }
        public string    ImagePath    { get; set; }
        public string    Bankname     { get; set; }
        public string    Chequeno     { get; set; }
        public string    PaymentRemark{ get; set; }
        public string    Comment      { get; set; }
        public DateTime? AddedDate    { get; set; }
        public int?      AddedById    { get; set; }
        public DateTime? UpdatedDate  { get; set; }
        public int?      UpdatedById  { get; set; }
        public int?      TrTypeId     { get; set; }
        public int?      BankAccountId{ get; set; }
        public string    BankAccountName { get; set; }  // JOIN-populated
        public DateTime? PaymentDate  { get; set; }
        public int?      ParentUserID { get; set; }
        public string    WalletSID    { get; set; }
        public string    GoogleResponse { get; set; }

        // ── UI helper properties (not DB columns, used in AddMoney form) ──

        [Required]
        public string WalletType   { get; set; } = "Main";   // "Main" | "BillPayment"
        public bool   IsPullOut    { get; set; }
        public bool   IsCredit     { get; set; }
        public bool   IsDebit      { get; set; }
        public bool   SMS          { get; set; }

        [Required]
        public string TransferType { get; set; } = "IMPS";   // IMPS|NEFT|RTGS|UPI|Cash|Cheque

        // Convenience aliases that delegate to the real columns
        [Required]
        public string ChequeRefNo
        {
            get => Chequeno;
            set => Chequeno = value;
        }

        [Required]
        public string Remark
        {
            get => Comment;
            set => Comment = value;
        }

        public int? AddedBy
        {
            get => AddedById;
            set => AddedById = value;
        }
    }

    // Matches real DB table: BankAccount
    public class BankAccount
    {
        public int     Id            { get; set; }

        [Required(ErrorMessage = "Bank name is required")]
        public string  BankName      { get; set; }

        [Required(ErrorMessage = "Account number is required")]
        public string  AccountNo     { get; set; }

        [Required(ErrorMessage = "Account holder name is required")]
        public string  HolderName    { get; set; }

        [Required(ErrorMessage = "IFSC code is required")]
        public string  IFSCCode      { get; set; }

        public string  UpiAdress     { get; set; }
        public string  BranchName    { get; set; }
        public string  BranchAddress { get; set; }
        public decimal? BlockAmount  { get; set; }
        public int?    AccountTypeId { get; set; }
        public int?    UserId        { get; set; }
        public int?    ApiId         { get; set; }
        public string  Remark        { get; set; }
        public int?    AddedById     { get; set; }
        public DateTime? AddedDate   { get; set; }
        public int?    UpdatedById   { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string  ImageUrl      { get; set; }
        public bool    BlockUser     { get; set; }

        // JOIN-populated (not a DB column)
        public string  AccountTypeName { get; set; }
    }
}
