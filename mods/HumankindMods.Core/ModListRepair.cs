using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HumankindMods.Core
{
    public sealed class RepairResult
    {
        public RepairResult(IReadOnlyList<string> modules, IReadOnlyList<string> replaced, IReadOnlyList<string> dropped)
        {
            Modules = modules;
            Replaced = replaced;
            Dropped = dropped;
        }

        public IReadOnlyList<string> Modules { get; }
        public IReadOnlyList<string> Replaced { get; }
        public IReadOnlyList<string> Dropped { get; }
        public bool Changed => Replaced.Count > 0 || Dropped.Count > 0;
    }

    /// <summary>
    /// One saved module the game can't find makes it reset the whole list to vanilla
    /// (Framework RuntimeManager.cs:237-247, then Mercury RuntimeManager.cs:688-701). This keeps every
    /// installed module, resolves "name.guid.major.minor" to the installed short name or newest version,
    /// and drops only what is really missing.
    /// </summary>
    public static class ModListRepair
    {
        private static readonly Regex VersionedName = new Regex(@"\.([0-9a-f]{32})\.(\d+)\.(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static RepairResult Repair(IReadOnlyList<string> configured, IEnumerable<string> installed)
        {
            var installedList = installed.ToList();
            var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in installedList) canonical[name] = name;
            var modules = new List<string>();
            var replaced = new List<string>();
            var dropped = new List<string>();

            foreach (var name in configured)
            {
                var resolved = Installed(name, canonical) ?? Installed(ShortName(name), canonical) ?? NewestWithSameGuid(name, installedList);
                if (resolved == null)
                {
                    dropped.Add(name);
                    continue;
                }

                if (!string.Equals(resolved, name, StringComparison.OrdinalIgnoreCase)) replaced.Add(name + " -> " + resolved);
                if (!modules.Contains(resolved, StringComparer.OrdinalIgnoreCase)) modules.Add(resolved);
            }

            return new RepairResult(modules, replaced, dropped);
        }

        private static string? Installed(string? name, Dictionary<string, string> canonical) =>
            name != null && canonical.TryGetValue(name, out var installedName) ? installedName : null;

        // The game registers most modules under the short name (RuntimeUtils.TrySplitRuntimeModuleName).
        private static string? ShortName(string name)
        {
            var match = VersionedName.Match(name);
            return match.Success ? name.Substring(0, match.Index) : null;
        }

        private static string? NewestWithSameGuid(string name, IEnumerable<string> installed)
        {
            var wanted = VersionedName.Match(name);
            if (!wanted.Success) return null;
            return installed
                .Select(candidate => (candidate, match: VersionedName.Match(candidate)))
                .Where(c => c.match.Success && string.Equals(c.match.Groups[1].Value, wanted.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => int.Parse(c.match.Groups[2].Value, CultureInfo.InvariantCulture))
                .ThenByDescending(c => int.Parse(c.match.Groups[3].Value, CultureInfo.InvariantCulture))
                .Select(c => c.candidate)
                .FirstOrDefault();
        }
    }
}
