using HumankindMods.Core;

namespace HumankindMods.Core.Tests;

public class HookGuardTests
{
    [Fact]
    public void Runs_the_hook_while_it_succeeds()
    {
        var guard = new HookGuard(_ => { });
        var calls = 0;

        guard.Run("refresh", () => calls++);
        guard.Run("refresh", () => calls++);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Disables_a_hook_after_its_first_exception_and_logs_once()
    {
        var logged = new List<string>();
        var guard = new HookGuard(logged.Add);
        var calls = 0;

        guard.Run("refresh", () => { calls++; throw new InvalidOperationException("boom"); });
        guard.Run("refresh", () => calls++);

        Assert.Equal(1, calls);
        Assert.Single(logged);
        Assert.Contains("refresh", logged[0]);
        Assert.Contains("boom", logged[0]);
        Assert.True(guard.IsDisabled("refresh"));
    }

    [Fact]
    public void Other_hooks_keep_running_after_one_is_disabled()
    {
        var guard = new HookGuard(_ => { });
        var calls = 0;

        guard.Run("refresh", () => throw new InvalidOperationException());
        guard.Run("spawn", () => calls++);

        Assert.Equal(1, calls);
        Assert.False(guard.IsDisabled("spawn"));
    }
}
