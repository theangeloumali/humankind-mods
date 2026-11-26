namespace HumankindMods.Core.Tests;

public class TurnShotPolicyTests
{
    [Fact]
    public void Captures_on_turns_divisible_by_the_interval()
    {
        Assert.True(TurnShotPolicy.ShouldCapture(turn: 150, everyTurns: 2));
        Assert.False(TurnShotPolicy.ShouldCapture(turn: 151, everyTurns: 2));
    }

    [Fact]
    public void A_zero_interval_never_captures()
    {
        Assert.False(TurnShotPolicy.ShouldCapture(turn: 150, everyTurns: 0));
    }
}
