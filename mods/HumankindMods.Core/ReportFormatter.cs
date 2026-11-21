using System.Linq;
using System.Text;

namespace HumankindMods.Core
{
    public static class ReportFormatter
    {
        public static string Format(DistrictSpawnTracker tracker, int turn, int trackedComponents, int maxRows)
        {
            var counts = tracker.GetCounts();
            var report = new StringBuilder();
            report.Append("CityRedraw diagnostics, turn ").Append(turn).Append('\n');
            report.Append("verdict: ").Append(Verdict(counts)).Append('\n');
            report.Append("components tracked: ").Append(trackedComponents).Append('\n');
            report.Append("B-pending: ").Append(counts.BPending).Append('\n');
            report.Append("B-orphaned: ").Append(counts.BOrphaned).Append('\n');
            report.Append("B-stuck: ").Append(counts.BStuck).Append('\n');
            report.Append("A-dropped: ").Append(counts.ADropped).Append('\n');
            report.Append("drops this session: ").Append(counts.TotalDrops).Append('\n');
            if (counts.WorstDropFrameCount > 0)
            {
                report.Append("worst drop frame: ").Append(counts.WorstDropFrame)
                    .Append(" (").Append(counts.WorstDropFrameCount).Append(" drops)\n");
            }

            var invisible = tracker.Invisible().OrderBy(c => c.State).ThenBy(c => c.Row).ThenBy(c => c.Column).ToList();
            foreach (var channel in invisible.Take(maxRows))
            {
                report.Append(channel.State).Append("  col ").Append(channel.Column).Append(" row ").Append(channel.Row)
                    .Append("  component ").Append(channel.ComponentId).Append(" channel ").Append(channel.Channel).Append('\n');
            }

            if (invisible.Count > maxRows)
            {
                report.Append(invisible.Count - maxRows).Append(" more not shown\n");
            }

            return report.ToString();
        }

        private static string Verdict(SpawnCounts counts)
        {
            if (counts.Invisible == 0)
            {
                return "none";
            }

            return string.Join(" ", new[] { counts.ADropped > 0 ? "A" : null, counts.BOrphaned + counts.BStuck > 0 ? "B" : null }.Where(cause => cause != null));
        }
    }
}
