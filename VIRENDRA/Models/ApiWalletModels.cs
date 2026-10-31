using System;
using System.Collections.Generic;

namespace VIRENDRA.Models
{
    /// <summary>Vendor (API) wallet ledger — ported from STC ApiWallet. Table: ApiWalletTxn.</summary>
    public class ApiWalletTxnItem
    {
        public long Id { get; set; }
        public int? ApiId { get; set; }
        public string VendorName { get; set; }     // ApiSource.ApiName
        public DateTime TxnDate { get; set; }
        public long? RecId { get; set; }
        public long? RefTxnId { get; set; }
        public decimal OP_Bal { get; set; }
        public decimal CR_Amt { get; set; }
        public decimal Ins_Amt { get; set; }
        public decimal DB_Amt { get; set; }
        public decimal CL_Bal { get; set; }        // computed: OP + CR + Ins - DB
        public string TxnType { get; set; }
        public string AmtType { get; set; }
        public string Remark { get; set; }
        public decimal ApiClBal { get; set; }      // vendor-reported balance
        public decimal ClBalDiff { get; set; }     // computed: ApiClBal - (OP + CR - DB)
        public string UserName { get; set; }       // recharge owner / wallet user
        public string AddedBy { get; set; }
    }

    public class ApiWalletViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public long? RecId { get; set; }
        public long? TxnId { get; set; }
        public int? TxnTypeId { get; set; }
        public int? AmtTypeId { get; set; }
        public int? ApiId { get; set; }
        public int? UserId { get; set; }
        public string Remark { get; set; }

        public List<ApiWalletTxnItem> Items { get; set; } = new List<ApiWalletTxnItem>();
        public int TotalCount { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalIncentive { get; set; }
        public decimal TotalDebit { get; set; }
    }

    /// <summary>Add Vendor Wallet form (STC ApiWalletDto).</summary>
    public class ApiWalletAddModel
    {
        public int ApiId { get; set; }
        public int TrTypeId { get; set; }          // TransferType (IMPS/NEFT/...)
        public int BankAccountId { get; set; }
        public decimal SentAmount { get; set; }    // money sent to vendor → CR_Amt
        public decimal ReceivedAmount { get; set; } // money taken back from vendor → DB_Amt
        public decimal IncentiveAmount { get; set; } // vendor incentive → Ins_Amt
        public string ChequeNo { get; set; }
        public string Remark { get; set; }
    }

    public enum ApiWalletAddResult { Success, Duplicate, InvalidVendor }
}
