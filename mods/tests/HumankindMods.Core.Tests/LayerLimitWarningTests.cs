namespace HumankindMods.Core.Tests;

public class LayerLimitWarningTests
{
    [Fact]
    public void Says_nothing_below_the_warning_threshold()
    {
        Assert.Null(LayerLimitWarning.Message(layers: 939, warnAt: 940, limit: 960, reloadKey: "Ctrl+F8"));
    }

    [Fact]
    public void Warns_with_the_count_the_limit_and_the_reload_key_from_the_threshold()
    {
        var message = LayerLimitWarning.Message(layers: 945, warnAt: 940, limit: 960, reloadKey: "Ctrl+F8");

        Assert.Equal("Graphics layers 945/960: buildings stop drawing past 960. Press Ctrl+F8 to quick-save and reload.", message);
    }

    [Fact]
    public void Says_buildings_are_already_missing_once_over_the_limit()
    {
        var message = LayerLimitWarning.Message(layers: 975, warnAt: 940, limit: 960, reloadKey: "Ctrl+F8");

        Assert.StartsWith("Graphics layers 975/960: buildings are no longer drawing.", message);
    }
}
