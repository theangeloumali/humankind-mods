namespace HumankindMods.Core.Tests;

public class AutoReloadPolicyTests
{
    [Fact]
    public void Reloads_once_layers_reach_the_threshold_after_growing_since_the_load()
    {
        Assert.True(AutoReloadPolicy.ShouldReload(layers: 946, threshold: 945, layersAfterLoad: 800, minGrowth: 20));
    }

    [Fact]
    public void Waits_below_the_threshold()
    {
        Assert.False(AutoReloadPolicy.ShouldReload(layers: 944, threshold: 945, layersAfterLoad: 800, minGrowth: 20));
    }

    [Fact]
    public void Never_reloads_when_the_load_itself_needs_that_many_layers()
    {
        Assert.False(AutoReloadPolicy.ShouldReload(layers: 950, threshold: 945, layersAfterLoad: 940, minGrowth: 20));
    }

    [Fact]
    public void A_zero_threshold_turns_it_off()
    {
        Assert.False(AutoReloadPolicy.ShouldReload(layers: 1000, threshold: 0, layersAfterLoad: 100, minGrowth: 20));
    }
}
