using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace VIRENDRA.Infrastructure
{
    /// <summary>
    /// API IP whitelist kept in User.LoginIP as a comma-separated list, e.g. "103.21.55.100,106.219.174.123".
    /// Empty or "*" = any IP allowed.
    /// </summary>
    public static class IpWhitelist
    {
        public const int MaxLength = 50;   // User.LoginIP is varchar(50)
        public const string AnyIp  = "*";

        private static readonly char[] Separators = { ',', ';', ' ', '\n', '\r', '\t' };

        public static List<string> Parse(string list) =>
            (list ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim()).Where(s => s.Length > 0)
                                  .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>Cleans user input into "a,b,c". Returns an error message, or null when valid.</summary>
        public static string Normalize(string input, out string normalized)
        {
            var ips = Parse(input);
            normalized = string.Join(",", ips);

            if (ips.Count == 0) return "Enter at least one IP address, or * for any IP.";

            // "*" wins over everything else in the list
            if (ips.Contains(AnyIp)) { normalized = AnyIp; return null; }

            var bad = ips.Where(ip => !IPAddress.TryParse(ip, out _)).ToList();
            if (bad.Any()) return "Not a valid IP address: " + string.Join(", ", bad);

            if (normalized.Length > MaxLength)
                return "Too many IPs: the list can be at most " + MaxLength + " characters (about 3 addresses).";

            return null;
        }

        public static bool Allows(string list, string clientIp)
        {
            var ips = Parse(list);
            if (ips.Count == 0 || ips.Contains(AnyIp)) return true;
            if (string.IsNullOrWhiteSpace(clientIp)) return false;

            // Compare parsed addresses so "::ffff:1.2.3.4" and "1.2.3.4" match too
            IPAddress client;
            if (!IPAddress.TryParse(clientIp.Trim(), out client))
                return ips.Any(ip => string.Equals(ip, clientIp.Trim(), StringComparison.OrdinalIgnoreCase));
            if (client.IsIPv4MappedToIPv6) client = client.MapToIPv4();

            return ips.Any(ip =>
            {
                IPAddress allowed;
                if (!IPAddress.TryParse(ip, out allowed)) return false;
                if (allowed.IsIPv4MappedToIPv6) allowed = allowed.MapToIPv4();
                return allowed.Equals(client);
            });
        }
    }
}
