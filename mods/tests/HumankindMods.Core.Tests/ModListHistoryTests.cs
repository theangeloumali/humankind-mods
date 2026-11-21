
namespace HumankindMods.Core.Tests;

public class ModListHistoryTests
{
    private const string Vanilla = "project mercury";
    private const string Modded = "project mercury/eventful.6ec391347e1e47ab825f149942d7b1dd.0.7/eq's in \"quotes\"";

    [Fact]
    public void Reset_to_vanilla_is_a_loss()
    {
        Assert.True(ModListHistory.IsLoss(Modded, Vanilla));
    }

    [Fact]
    public void Adding_a_mod_or_reordering_is_not_a_loss()
    {
        Assert.False(ModListHistory.IsLoss(Vanilla, Modded));
        Assert.False(ModListHistory.IsLoss("project mercury/a/b", "project mercury/b/a"));
    }

    [Fact]
    public void Nothing_saved_before_is_not_a_loss()
    {
        Assert.False(ModListHistory.IsLoss(null, Vanilla));
        Assert.False(ModListHistory.IsLoss("", Vanilla));
    }

    [Fact]
    public void Removing_one_mod_is_a_loss()
    {
        Assert.True(ModListHistory.IsLoss("project mercury/a/b", "project mercury/b"));
    }

    [Fact]
    public void Entry_round_trips_through_json_with_quotes_and_backslashes()
    {
        var line = ModListHistory.ToJsonLine(
            new DateTime(2026, 11, 21, 3, 4, 5, DateTimeKind.Utc), "reset-after-mod-error", "bad\\mod \"x\"", Modded, Vanilla);

        Assert.DoesNotContain("\n", line);
        Assert.Equal(Modded, ModListHistory.FindNewestRestorable(new[] { line }));
    }

    [Fact]
    public void Restore_picks_the_newest_entry()
    {
        var older = ModListHistory.ToJsonLine(DateTime.UtcNow, "a", null, "project mercury/old", Vanilla);
        var newer = ModListHistory.ToJsonLine(DateTime.UtcNow, "b", null, Modded, Vanilla);

        Assert.Equal(Modded, ModListHistory.FindNewestRestorable(new[] { older, newer, "" }));
    }

    [Fact]
    public void Restore_returns_null_without_history_or_on_garbage()
    {
        Assert.Null(ModListHistory.FindNewestRestorable(Array.Empty<string>()));
        Assert.Null(ModListHistory.FindNewestRestorable(new[] { "not json" }));
    }
}
