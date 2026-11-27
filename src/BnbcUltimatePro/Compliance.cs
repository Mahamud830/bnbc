using System;
using System.Collections.Generic;
using System.Linq;

namespace BnbcUltimatePro
{
    internal static class ComplianceRules
    {
        /// <summary>Maps a warning written by the seismic calculation to a status word.</summary>
        public static string ClassifyWarning(string warning)
        {
            var l = (warning ?? "").ToLowerInvariant();
            if (l.Contains("not permitted") || l.Contains("exceeds")) return "FAIL";
            if (l.Contains("not confirmed") || l.Contains("no result")) return "INCOMPLETE";
            return "WARN";
        }

        static int Rank(string status)
        {
            switch (status)
            {
                case "FAIL": return 4;
                case "INCOMPLETE": return 3;
                case "WARN": return 2;
                case "NOT CHECKED": return 2;
                default: return 1; // PASS
            }
        }

        /// <summary>Worst status of a list: FAIL, then INCOMPLETE, then WARN (NOT CHECKED counts as WARN), else PASS.</summary>
        public static string Worst(IEnumerable<string> statuses)
        {
            string worst = "PASS"; int best = 1;
            foreach (var s in statuses)
            {
                int r = Rank(s);
                if (r > best) { best = r; worst = (s == "NOT CHECKED") ? "WARN" : s; }
            }
            return worst;
        }
    }
}
