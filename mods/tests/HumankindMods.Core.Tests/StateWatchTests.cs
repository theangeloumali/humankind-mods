namespace HumankindMods.Core.Tests;

public class StateWatchTests
{
    [Fact]
    public void Reports_each_change_of_state_with_the_previous_one()
    {
        var watch = new StateWatch<int>();

        Assert.Equal("None -> Running", watch.Change(1, "Running", now: 0f, initial: "None"));
        Assert.Null(watch.Change(1, "Running", now: 5f, initial: "None"));
        Assert.Equal("Running -> End", watch.Change(1, "End", now: 6f, initial: "None"));
    }

    [Fact]
    public void Flags_a_state_held_too_long_once()
    {
        var watch = new StateWatch<int>();
        watch.Change(1, "Running", now: 0f, initial: "None");

        Assert.False(watch.HeldTooLong(1, "Running", now: 19f, limitSeconds: 20f));
        Assert.True(watch.HeldTooLong(1, "Running", now: 21f, limitSeconds: 20f));
        Assert.False(watch.HeldTooLong(1, "Running", now: 40f, limitSeconds: 20f));
    }

    [Fact]
    public void A_new_state_restarts_the_clock()
    {
        var watch = new StateWatch<int>();
        watch.Change(1, "Running", now: 0f, initial: "None");
        watch.Change(1, "End", now: 30f, initial: "None");
        watch.Change(1, "Running", now: 31f, initial: "None");

        Assert.False(watch.HeldTooLong(1, "Running", now: 40f, limitSeconds: 20f));
    }
}
