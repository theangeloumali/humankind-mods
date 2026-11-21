using HumankindMods.Core;

namespace HumankindMods.Core.Tests;

public class AutoPlayRequestTests
{
    [Theory]
    [InlineData("350", 350)]
    [InlineData(" 350\n", 350)]
    [InlineData("target=120", 120)]
    public void Reads_the_target_turn(string text, int expected)
    {
        Assert.Equal(expected, AutoPlayRequest.ParseTargetTurn(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Rejects_missing_or_non_positive_targets(string text)
    {
        Assert.Null(AutoPlayRequest.ParseTargetTurn(text));
    }

    [Theory]
    [InlineData(349, 350, false)]
    [InlineData(350, 350, true)]
    [InlineData(351, 350, true)]
    public void Target_is_reached_at_or_after_the_turn(int turn, int target, bool reached)
    {
        Assert.Equal(reached, AutoPlayRequest.IsReached(turn, target));
    }
}
