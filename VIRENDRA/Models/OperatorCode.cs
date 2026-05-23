namespace VIRENDRA.Models
{
    // Matches real DB table: OperatorCode
    public class OperatorCode
    {
        public int    Id          { get; set; }
        public int    OpId        { get; set; }
        public int    ApiId       { get; set; }
        public string OpCode      { get; set; }
        public string ExtraUrl    { get; set; }
        public string ExtraUrlData{ get; set; }
        public int?   MaxQSize    { get; set; }
    }
}
