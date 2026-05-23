using System;

namespace VIRENDRA.Models
{
    // Matches real DB table: TxnLedger
    public class TxnLedger
    {
        public long    Id        { get; set; }
        public long?   RecId     { get; set; }
        public int     UserId    { get; set; }
        public string  RefTxnId  { get; set; }
        public DateTime? TxnDate { get; set; }
        public decimal OP_Bal    { get; set; }
        public decimal CR_Amt    { get; set; }
        public decimal DB_Amt    { get; set; }
        public decimal CL_Bal    { get; set; }   // persisted computed: OP_Bal + CR_Amt - DB_Amt
        public int?    TxnTypeId { get; set; }
        public int?    AmtTypeId { get; set; }
        public string  Remark    { get; set; }
        public int?    AddedById { get; set; }
    }
}
