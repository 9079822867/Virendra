using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using VIRENDRA.Models;

namespace VIRENDRA.Infrastructure
{
    /// <summary>Id-list filters stored as "All" or "1,4,7" (BlockRoute, StopRouteMessage).</summary>
    public static class IdFilter
    {
        public static bool IsAll(string filter) =>
            string.IsNullOrWhiteSpace(filter) || filter.IndexOf("all", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Selected ids; {0} means All (the "All" option's value).</summary>
        public static HashSet<int> Ids(string filter)
        {
            if (IsAll(filter)) return new HashSet<int> { 0 };
            var set = new HashSet<int>();
            foreach (var s in filter.Split(','))
                if (int.TryParse(s.Trim(), out int id)) set.Add(id);
            return set;
        }

        /// <summary>Comma list of names for display (HTML-encoded), or an "All" badge.</summary>
        public static IHtmlString Names(string filter, IEnumerable<LookupItem> items)
        {
            if (IsAll(filter))
                return new HtmlString("<span class=\"badge bg-light text-dark border\">All</span>");

            var list  = items?.ToList() ?? new List<LookupItem>();
            var names = filter.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Select(s =>
                int.TryParse(s, out int id) ? (list.FirstOrDefault(i => i.Id == id)?.Name ?? "#" + id) : s);
            return new HtmlString(HttpUtility.HtmlEncode(string.Join(", ", names)));
        }
    }
}
