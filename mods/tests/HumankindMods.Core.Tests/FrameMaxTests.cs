namespace HumankindMods.Core.Tests;

public class FrameMaxTests
{
    [Fact]
    public void Keeps_the_largest_value_seen_in_a_frame()
    {
        var max = new FrameMax();
        max.Observe(frame: 1, value: 797, extra: 960);
        max.Observe(frame: 1, value: 1, extra: 960);

        Assert.Equal(797, max.Value);
        Assert.Equal(960, max.Extra);
    }

    [Fact]
    public void A_new_frame_starts_over()
    {
        var max = new FrameMax();
        max.Observe(frame: 1, value: 797, extra: 960);
        max.Observe(frame: 2, value: 640, extra: 960);

        Assert.Equal(640, max.Value);
    }
}
