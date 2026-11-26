using System.Collections.Generic;

namespace HumankindMods.Core
{
    /// <summary>Says whether a key is new and under the cap, so a per-frame failure is logged once, not every frame.</summary>
    public sealed class DistinctReporter
    {
        private readonly HashSet<string> reported = new HashSet<string>();
        private readonly HashSet<string> overCap = new HashSet<string>();
        private readonly int maxReports;

        public DistinctReporter(int maxReports) => this.maxReports = maxReports;

        public int Occurrences { get; private set; }

        public int DistinctSeen => reported.Count + overCap.Count;

        public bool ShouldReport(string key)
        {
            Occurrences++;
            if (reported.Contains(key)) return false;
            if (reported.Count >= maxReports)
            {
                overCap.Add(key);
                return false;
            }

            reported.Add(key);
            return true;
        }
    }
}
