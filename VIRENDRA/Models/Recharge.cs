using System;

namespace VIRENDRA.Models
{
    // Matches real DB table: Recharge
    public class RechargeRecord
    {
        public long    Id                 { get; set; }
        public int     UserId             { get; set; }
        public int?    ApiId              { get; set; }
        public string  CustomerNo         { get; set; }
        public int?    OpId               { get; set; }
        public int?    CircleId           { get; set; }
        public decimal Amount             { get; set; }
        public byte?   RCTypeId          { get; set; }
        public int?    StatusId           { get; set; }
        public DateTime? RequestTime      { get; set; }
        public int?    MediumId           { get; set; }
        public string  UserTxnId          { get; set; }
        public string  OurRefTxnId        { get; set; }
        public decimal? cashback          { get; set; }
        public long?   TxnId              { get; set; }
        public string  ApiTxnId           { get; set; }
        public string  StatusMsg          { get; set; }
        public DateTime? ResponseTime     { get; set; }
        public decimal? Recharge_Commision{ get; set; }
        public decimal? ApiBal            { get; set; }
    }
}
