namespace HumankindMods.Core.Tests;

public class HandlerIsolationTests
{
    [Fact]
    public void A_throwing_handler_does_not_stop_the_ones_after_it()
    {
        var isolation = new HandlerIsolation(10, _ => { });
        var ran = new List<int>();
        Action[] handlers = { () => ran.Add(1), () => throw new ArgumentOutOfRangeException("index"), () => ran.Add(3) };

        foreach (var handler in handlers) isolation.Run(handler);

        Assert.Equal(new[] { 1, 3 }, ran);
    }

    [Fact]
    public void Reports_each_distinct_error_once_with_its_stack()
    {
        var reports = new List<string>();
        var isolation = new HandlerIsolation(10, reports.Add);
        void Throws() => throw new ArgumentOutOfRangeException("index");

        for (var frame = 0; frame < 3; frame++) isolation.Run(Throws);
        isolation.Run(() => throw new NullReferenceException());

        Assert.Equal(2, reports.Count);
        Assert.Contains("ArgumentOutOfRangeException", reports[0]);
        Assert.Contains(nameof(Throws), reports[0]);
        Assert.Contains("NullReferenceException", reports[1]);
    }

    [Fact]
    public void Stops_reporting_new_errors_after_the_cap()
    {
        var reports = new List<string>();
        var isolation = new HandlerIsolation(2, reports.Add);

        for (var i = 0; i < 5; i++)
        {
            var message = "error " + i;
            isolation.Run(() => throw new InvalidOperationException(message));
        }

        Assert.Equal(2, reports.Count);
    }

    [Fact]
    public void Runs_a_healthy_handler_every_time()
    {
        var isolation = new HandlerIsolation(10, _ => { });
        var calls = 0;

        for (var frame = 0; frame < 3; frame++) isolation.Run(() => calls++);

        Assert.Equal(3, calls);
    }
}
