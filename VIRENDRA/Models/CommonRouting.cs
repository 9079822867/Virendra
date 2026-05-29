using System;
using System.ComponentModel.DataAnnotations;

namespace VIRENDRA.Models
{
    /// <summary>Matches DB table: CommanRouting</summary>
    public class CommonRouting
    {
        public int  Id         { get; set; }

        [Required(ErrorMessage = "Operator is required")]
        public int? OpId       { get; set; }     // FK → Operator.Id

        public string CircleFilter  { get; set; } = "All"; // "All" or circle name
        public int?   ApiId         { get; set; }           // FK → ApiSource.Id (Vendor)

        [Required(ErrorMessage = "Priority is required")]
        [Range(1, 9999, ErrorMessage = "Priority must be 1-9999")]
        public int?   Priority      { get; set; }

        public byte?  WaitMinute    { get; set; }

        [Required(ErrorMessage = "Amount filter is required")]
        public string AmountFilter  { get; set; } // "2-4000" (Range) or "10,20,30" (Amounts)

        public string UserFilter    { get; set; } = "All"; // "All" or comma-sep user IDs
        public int?   FTypeId       { get; set; } = 1;     // 1=Range, 2=Amounts
        public string Optional1     { get; set; }

        [Range(0, 100, ErrorMessage = "RO% must be 0-100")]
        public decimal? MinRO       { get; set; } = 0;     // RO%

        public string BlockUser     { get; set; }           // comma-sep user IDs to block
        public string RouteOP1      { get; set; }           // OP1
        public string RouteOP2      { get; set; }           // OP2
        public bool   IsActive      { get; set; } = true;

        // Audit
        public DateTime  AddedDate  { get; set; }
        public int?      AddedById  { get; set; }
        public DateTime? UpdatedDate{ get; set; }
        public int?      UpdatedById{ get; set; }

        // ── JOIN-populated (not DB columns) ──────────────────────
        public string OperatorName  { get; set; }
        public string VendorName    { get; set; }

        // ── Display helpers ───────────────────────────────────────
        public string FilterTypeName => FTypeId == 2 ? "Amounts" : "Range";
    }
}
