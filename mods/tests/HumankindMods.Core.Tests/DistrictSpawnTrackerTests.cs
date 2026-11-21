
namespace HumankindMods.Core.Tests;

public class DistrictSpawnTrackerTests
{
    private const int District = 7;
    private const int MainChannel = 0;

    private static DistrictSpawnTracker NewTracker() => new(stuckAfterSeconds: 60);

    [Fact]
    public void Refresh_with_art_not_loaded_marks_channel_b_pending()
    {
        var tracker = NewTracker();

        tracker.OnRefresh(District, MainChannel, hasMaterial: true, ableToSpawn: false, column: 3, row: 4, now: 0);

        Assert.Equal(DistrictRenderState.BPending, tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void Refresh_with_art_loaded_queues_a_spawn_and_is_not_tracked()
    {
        var tracker = NewTracker();

        tracker.OnRefresh(District, MainChannel, hasMaterial: true, ableToSpawn: true, column: 3, row: 4, now: 0);

        Assert.Null(tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void Sweep_marks_b_orphaned_once_the_art_loads_without_a_respawn()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(District, MainChannel, true, false, 3, 4, now: 0);

        tracker.Sweep(now: 5, isAbleToSpawn: (_, _) => true);

        Assert.Equal(DistrictRenderState.BOrphaned, tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void Sweep_marks_b_stuck_when_the_art_is_still_loading_after_the_timeout()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(District, MainChannel, true, false, 3, 4, now: 0);

        tracker.Sweep(now: 59, isAbleToSpawn: (_, _) => false);
        Assert.Equal(DistrictRenderState.BPending, tracker.StateOf(District, MainChannel));

        tracker.Sweep(now: 60, isAbleToSpawn: (_, _) => false);
        Assert.Equal(DistrictRenderState.BStuck, tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void A_later_refresh_with_art_loaded_clears_a_b_mark()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(District, MainChannel, true, false, 3, 4, now: 0);
        tracker.Sweep(now: 5, isAbleToSpawn: (_, _) => true);

        tracker.OnRefresh(District, MainChannel, true, true, 3, 4, now: 6);

        Assert.Null(tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void Refresh_without_material_forgets_the_channel()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(District, MainChannel, true, false, 3, 4, now: 0);

        tracker.OnRefresh(District, MainChannel, hasMaterial: false, ableToSpawn: false, column: 3, row: 4, now: 1);

        Assert.Null(tracker.StateOf(District, MainChannel));
    }

    [Fact]
    public void Spawn_that_writes_fewer_particles_than_expected_marks_the_unwritten_channels_a_dropped()
    {
        var tracker = NewTracker();

        tracker.OnSpawn(District, dueChannelMask: 0b101, written: 1, frame: 100);

        Assert.Null(tracker.StateOf(District, 0));
        Assert.Equal(DistrictRenderState.ADropped, tracker.StateOf(District, 2));
    }

    [Fact]
    public void Full_spawn_clears_a_previous_drop()
    {
        var tracker = NewTracker();
        tracker.OnSpawn(District, 0b1, written: 0, frame: 100);

        tracker.OnSpawn(District, 0b1, written: 1, frame: 101);

        Assert.Null(tracker.StateOf(District, 0));
    }

    [Fact]
    public void Drops_are_counted_per_frame_and_the_worst_frame_is_kept()
    {
        var tracker = NewTracker();
        tracker.OnSpawn(1, 0b1, written: 0, frame: 100);
        tracker.OnSpawn(2, 0b1, written: 0, frame: 101);
        tracker.OnSpawn(3, 0b1, written: 0, frame: 101);

        var counts = tracker.GetCounts();

        Assert.Equal(3, counts.TotalDrops);
        Assert.Equal(101, counts.WorstDropFrame);
        Assert.Equal(2, counts.WorstDropFrameCount);
    }

    [Fact]
    public void Unload_forgets_every_channel_of_the_component()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(District, 0, true, false, 3, 4, now: 0);
        tracker.OnSpawn(District, 0b10, written: 0, frame: 1);

        tracker.OnUnload(District);

        Assert.Null(tracker.StateOf(District, 0));
        Assert.Null(tracker.StateOf(District, 1));
    }

    [Fact]
    public void Invisible_lists_orphaned_stuck_and_dropped_but_not_pending_with_tile()
    {
        var tracker = NewTracker();
        tracker.OnRefresh(1, 0, true, false, column: 10, row: 20, now: 0);
        tracker.OnRefresh(2, 0, true, false, column: 11, row: 21, now: 0);
        tracker.OnRefresh(3, 0, true, false, column: 12, row: 22, now: 50);
        tracker.Sweep(now: 61, isAbleToSpawn: (id, _) => id == 1);
        tracker.OnSpawn(4, 0b1, written: 0, frame: 9);

        var invisible = tracker.Invisible().OrderBy(d => d.ComponentId).ToList();

        Assert.Equal(new[] { 1, 2, 4 }, invisible.Select(d => d.ComponentId));
        Assert.Equal(DistrictRenderState.BOrphaned, invisible[0].State);
        Assert.Equal(DistrictRenderState.BStuck, invisible[1].State);
        Assert.Equal(DistrictRenderState.ADropped, invisible[2].State);
        Assert.Equal((10, 20), (invisible[0].Column, invisible[0].Row));
        Assert.Equal(1, tracker.GetCounts().BPending);
    }
}
