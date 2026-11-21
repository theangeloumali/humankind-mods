namespace HumankindMods.Core.Tests;

public class GuardedHookTests
{
    [Fact]
    public void Runs_the_hook_while_it_succeeds()
    {
        var hook = new GuardedHook("refresh", _ => { });
        var calls = 0;

        hook.Run(() => calls++);
        hook.Run(() => calls++);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Disables_after_its_first_exception_and_logs_once()
    {
        var logged = new List<string>();
        var hook = new GuardedHook("refresh", logged.Add);
        var calls = 0;

        hook.Run(() => { calls++; throw new InvalidOperationException("boom"); });
        hook.Run(() => calls++);

        Assert.Equal(1, calls);
        Assert.Single(logged);
        Assert.Contains("refresh", logged[0]);
        Assert.Contains("boom", logged[0]);
        Assert.True(hook.IsDisabled);
    }

    [Fact]
    public void State_overload_passes_the_state_without_a_closure()
    {
        var hook = new GuardedHook("spawn", _ => { });
        var seen = new List<int>();

        hook.Run((seen, 42), static s => s.seen.Add(s.Item2));

        Assert.Equal(new[] { 42 }, seen);
    }

    [Fact]
    public void Result_overload_returns_the_fallback_after_a_failure_and_once_disabled()
    {
        var hook = new GuardedHook("probe", _ => { });

        Assert.Equal(7, hook.Run(3, static s => s + 4, fallback: -1));
        Assert.Equal(-1, hook.Run(3, static int (_) => throw new InvalidOperationException(), fallback: -1));
        Assert.Equal(-1, hook.Run(3, static s => s + 4, fallback: -1));
    }

    [Fact]
    public void Separate_hooks_fail_independently()
    {
        var refresh = new GuardedHook("refresh", _ => { });
        var spawn = new GuardedHook("spawn", _ => { });
        var calls = 0;

        refresh.Run(() => throw new InvalidOperationException());
        spawn.Run(() => calls++);

        Assert.Equal(1, calls);
        Assert.False(spawn.IsDisabled);
    }
}
