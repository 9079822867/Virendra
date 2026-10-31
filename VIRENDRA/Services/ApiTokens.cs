using System.Collections.Generic;

namespace VIRENDRA.Services
{
    /// <summary>
    /// Placeholders used in vendor API URLs and POST bodies, e.g.
    /// <c>https://vendor.in/recharge?mobile={{mobile}}&amp;amount={{amount}}</c>.
    /// Each token also has its old bracket form (<c>[MMM]</c>…) so URLs already saved in
    /// ApiUrl keep working. One list feeds both the replacement and the Vendor form legend.
    /// </summary>
    public static class ApiTokens
    {
        public const string UserId      = "user_id";
        public const string Password    = "password";
        public const string ApiKey      = "api_key";
        public const string Mobile      = "mobile";
        public const string Account     = "account";
        public const string Operator    = "operator";
        public const string Circle      = "circle";
        public const string Amount      = "amount";
        public const string RefId       = "ref_id";
        public const string TxnId       = "txn_id";
        public const string VendorTxnId = "vendor_txn_id";
        public const string ExtraUrl    = "extra_url";
        public const string ExtraData   = "extra_data";
        public const string Commission  = "commission";
        public const string Timestamp   = "timestamp";
        public const string Random      = "random";
        public const string Remark      = "remark";

        public class Token
        {
            public string Key    { get; set; }
            public string Legacy { get; set; }   // old bracket form, still accepted
            public string Label  { get; set; }
            public string Group  { get; set; }   // Credentials | Recharge | Reference | Other
            public string Text => "{{" + Key + "}}";
        }

        public static readonly IReadOnlyList<Token> All = new List<Token>
        {
            new Token { Key = UserId,      Legacy = "[UUU]",         Label = "Vendor user ID",        Group = "Credentials" },
            new Token { Key = Password,    Legacy = "[PPP]",         Label = "Vendor password",       Group = "Credentials" },
            new Token { Key = ApiKey,      Legacy = "[RRR]",         Label = "API key / token",       Group = "Credentials" },
            new Token { Key = Mobile,      Legacy = "[MMM]",         Label = "Mobile / customer no",  Group = "Recharge" },
            new Token { Key = Account,     Legacy = "[HHH]",         Label = "Account no (bill pay)", Group = "Recharge" },
            new Token { Key = Operator,    Legacy = "[OOO]",         Label = "Vendor operator code",  Group = "Recharge" },
            new Token { Key = Circle,      Legacy = "[CCC]",         Label = "Circle code",           Group = "Recharge" },
            new Token { Key = Amount,      Legacy = "[AAA]",         Label = "Amount",                Group = "Recharge" },
            new Token { Key = RefId,       Legacy = "[TTT]",         Label = "Our reference no",      Group = "Reference" },
            new Token { Key = TxnId,       Legacy = "[VVV]",         Label = "Our transaction no",    Group = "Reference" },
            new Token { Key = VendorTxnId, Legacy = "[OPTXN]",       Label = "Vendor txn no",         Group = "Reference" },
            new Token { Key = ExtraUrl,    Legacy = "[EEE]",         Label = "Operator extra URL",    Group = "Other" },
            new Token { Key = ExtraData,   Legacy = "[DDD]",         Label = "Operator extra data",   Group = "Other" },
            new Token { Key = Commission,  Legacy = "[COMM1]",       Label = "Commission",            Group = "Other" },
            new Token { Key = Timestamp,   Legacy = "[FFFT]",        Label = "Time yyyyMMddHHmmss",   Group = "Other" },
            new Token { Key = Random,      Legacy = "[FFFR]",        Label = "Random 6 digits",       Group = "Other" },
            new Token { Key = Remark,      Legacy = "[Description]", Label = "Complaint remark",      Group = "Other" },
        };

        // Old tokens that never carried a value; cleared so they don't reach the vendor
        private static readonly string[] Blank = { "[NNN]", "[FFF1]", "[FFF2]", "[FFF3]", "[FFF4]" };

        /// <summary>
        /// Replaces every {{token}} (and its old [XXX] form) present in <paramref name="values"/>.
        /// Tokens without a value are left as they are.
        /// </summary>
        public static string Fill(string template, IDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            string s = template;
            foreach (var t in All)
            {
                if (!values.TryGetValue(t.Key, out string v)) continue;
                v = v ?? string.Empty;
                s = s.Replace(t.Text, v).Replace(t.Legacy, v);
            }
            foreach (var b in Blank) s = s.Replace(b, string.Empty);
            return s;
        }
    }
}
