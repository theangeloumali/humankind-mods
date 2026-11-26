using System.Collections.Generic;

namespace HumankindMods.Core
{
    /// <summary>Per-key state tracking for things polled every frame: reports a change once, and a state held past a
    /// limit once (until the state changes again).</summary>
    public sealed class StateWatch<TKey>
    {
        private sealed class Entry
        {
            public string State = "";
            public float Since;
            public bool Flagged;
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();

        /// <summary>"previous -> current" when the state changed since the last call, else null.</summary>
        public string? Change(TKey key, string state, float now, string initial)
        {
            if (!entries.TryGetValue(key, out var entry)) entries[key] = entry = new Entry { State = initial, Since = now };
            if (entry.State == state) return null;
            var change = entry.State + " -> " + state;
            entry.State = state;
            entry.Since = now;
            entry.Flagged = false;
            return change;
        }

        public bool HeldTooLong(TKey key, string state, float now, float limitSeconds)
        {
            if (!entries.TryGetValue(key, out var entry) || entry.State != state || entry.Flagged || now - entry.Since < limitSeconds) return false;
            entry.Flagged = true;
            return true;
        }
    }
}
