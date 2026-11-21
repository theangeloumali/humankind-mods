using System;
using System.Collections.Generic;

namespace HumankindMods.Core
{
    /// <summary>Runs game hooks so a mod bug can never break the game: a hook that throws is switched off.</summary>
    public sealed class HookGuard
    {
        private readonly Action<string> log;
        private readonly HashSet<string> disabled = new HashSet<string>();

        public HookGuard(Action<string> log)
        {
            this.log = log;
        }

        public bool IsDisabled(string hook) => disabled.Contains(hook);

        public void Run(string hook, Action body)
        {
            if (disabled.Contains(hook)) return;
            try
            {
                body();
            }
            catch (Exception exception)
            {
                disabled.Add(hook);
                log("hook " + hook + " disabled after an exception: " + exception);
            }
        }
    }
}
