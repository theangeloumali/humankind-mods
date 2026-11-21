using System;

namespace HumankindMods.Core
{
    /// <summary>
    /// Runs one game hook so a mod bug can never break the game: after its first exception the hook is
    /// switched off and logged once. State overloads let callers pass static lambdas (no per-call closure).
    /// </summary>
    public sealed class GuardedHook
    {
        private readonly string name;
        private readonly Action<string> log;

        public GuardedHook(string name, Action<string> log)
        {
            this.name = name;
            this.log = log;
        }

        internal bool IsDisabled { get; private set; }

        public void Run(Action body)
        {
            if (IsDisabled) return;
            try
            {
                body();
            }
            catch (Exception exception)
            {
                Disable(exception);
            }
        }

        public void Run<TState>(TState state, Action<TState> body)
        {
            if (IsDisabled) return;
            try
            {
                body(state);
            }
            catch (Exception exception)
            {
                Disable(exception);
            }
        }

        public TResult Run<TState, TResult>(TState state, Func<TState, TResult> body, TResult fallback)
        {
            if (IsDisabled) return fallback;
            try
            {
                return body(state);
            }
            catch (Exception exception)
            {
                Disable(exception);
                return fallback;
            }
        }

        private void Disable(Exception exception)
        {
            IsDisabled = true;
            log("hook " + name + " disabled after an exception: " + exception);
        }
    }
}
