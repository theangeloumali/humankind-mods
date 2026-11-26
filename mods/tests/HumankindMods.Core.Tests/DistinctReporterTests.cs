namespace HumankindMods.Core.Tests;

public class DistinctReporterTests
{
    [Fact]
    public void Reports_a_key_the_first_time_only()
    {
        var reporter = new DistinctReporter(10);

        Assert.True(reporter.ShouldReport("District_CityCenter|Colchis"));
        Assert.False(reporter.ShouldReport("District_CityCenter|Colchis"));
        Assert.True(reporter.ShouldReport("District_CityCenter|Egypt"));
    }

    [Fact]
    public void Stops_at_the_cap()
    {
        var reporter = new DistinctReporter(2);

        Assert.True(reporter.ShouldReport("a"));
        Assert.True(reporter.ShouldReport("b"));
        Assert.False(reporter.ShouldReport("c"));
    }

    [Fact]
    public void Counts_every_occurrence_including_repeats()
    {
        var reporter = new DistinctReporter(1);

        reporter.ShouldReport("a");
        reporter.ShouldReport("a");
        reporter.ShouldReport("b");

        Assert.Equal(3, reporter.Occurrences);
        Assert.Equal(2, reporter.DistinctSeen);
    }
}
