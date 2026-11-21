
namespace HumankindMods.Core.Tests;

public class ReportFormatterTests
{
    [Fact]
    public void Report_names_the_cause_counts_and_tiles()
    {
        var tracker = new DistrictSpawnTracker(stuckAfterSeconds: 60);
        tracker.OnRefresh(1, 0, true, false, column: 10, row: 20, now: 0);
        tracker.Sweep(now: 1, isAbleToSpawn: (_, _) => true);
        tracker.OnSpawn(2, 0b1, written: 0, frame: 42);

        var report = ReportFormatter.Format(tracker, turn: 312, trackedComponents: 4200, maxRows: 200);

        Assert.Contains("turn 312", report);
        Assert.Contains("components tracked: 4200", report);
        Assert.Contains("B-orphaned: 1", report);
        Assert.Contains("A-dropped: 1", report);
        Assert.Contains("worst drop frame: 42 (1 drops)", report);
        Assert.Contains("BOrphaned  col 10 row 20", report);
    }

    [Fact]
    public void Report_says_none_when_nothing_is_invisible()
    {
        var report = ReportFormatter.Format(new DistrictSpawnTracker(60), turn: 1, trackedComponents: 0, maxRows: 200);

        Assert.Contains("verdict: none", report);
    }

    [Fact]
    public void Report_caps_the_district_rows()
    {
        var tracker = new DistrictSpawnTracker(60);
        for (var id = 0; id < 5; id++) tracker.OnSpawn(id, 0b1, written: 0, frame: 1);

        var report = ReportFormatter.Format(tracker, turn: 1, trackedComponents: 5, maxRows: 2);

        Assert.Equal(2, report.Split('\n').Count(l => l.StartsWith("ADropped")));
        Assert.Contains("3 more not shown", report);
    }
}
