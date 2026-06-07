using System.Collections.Generic;

namespace VIRENDRA.Models
{
    /// <summary>
    /// One row in the "Edit Operatorcode Margin" grid — combines the vendor's
    /// operator code (OperatorCode table) and the vendor commission (PackageComm table)
    /// for a single operator.
    /// </summary>
    public class OperatorMarginDto
    {
        public int    ApiId        { get; set; }   // ApiSource.Id (vendor)
        public int    OpId         { get; set; }   // Operator.Id
        public string OperatorName { get; set; }   // display only

        // ── OperatorCode columns ─────────────────────────────────────────────
        public string OpCode       { get; set; }   // Vendor Op-Code  [OOO]
        public string ExtraUrl     { get; set; }   // Extra Url       [EEE]
        public string ExtraData    { get; set; }   // Extra Params    [DDD]

        // ── PackageComm columns ──────────────────────────────────────────────
        public decimal CommAmt     { get; set; }   // commission value
        public int     AmtTypeId   { get; set; }   // 5=Discount, 4=Surcharge …
        public int     CommTypeId  { get; set; }   // 1=Flat, 2=Percent, 3=Range
    }

    /// <summary>View model for the vendor operator-code + commission grid.</summary>
    public class VendorMarginViewModel
    {
        public int    ApiId   { get; set; }   // ApiSource.Id
        public string ApiName { get; set; }
        public int    PackId  { get; set; }   // ApiSource.ApiPackid (0 if none yet)

        public List<OperatorMarginDto> Rows { get; set; } = new List<OperatorMarginDto>();
    }
}
