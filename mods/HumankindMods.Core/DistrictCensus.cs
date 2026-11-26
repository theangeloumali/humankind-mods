using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HumankindMods.Core
{
    /// <summary>One district's (or construction site's) main art channel: did the lookup find art, is it loaded.</summary>
    public readonly struct DistrictArt
    {
        public DistrictArt(string definition, string style, int empire, bool hasArt, bool loaded, bool isConstructionSite = false,
            bool componentLoaded = true)
        {
            ComponentLoaded = componentLoaded;
            Definition = definition;
            Style = style;
            Empire = empire;
            HasArt = hasArt;
            Loaded = loaded;
            IsConstructionSite = isConstructionSite;
        }

        public string Definition { get; }
        public string Style { get; }
        public int Empire { get; }
        public bool HasArt { get; }
        public bool Loaded { get; }
        public bool IsConstructionSite { get; }
        /// <summary>The level-build component only draws once it has loaded (its dependencies resolved).</summary>
        public bool ComponentLoaded { get; }
    }

    /// <summary>Counts what the map holds by art state, lists what can't draw, and compares against the simulation.</summary>
    public static class DistrictCensus
    {
        /// <param name="expected">Districts the simulation snapshot says the map should show now.</param>
        /// <param name="underConstruction">Districts hidden on purpose while something is built over them.</param>
        public static string Format(IReadOnlyCollection<DistrictArt> entries, int expected, int underConstruction)
        {
            var districts = entries.Where(e => !e.IsConstructionSite).ToList();
            var text = new StringBuilder();
            AppendSection(text, "districts", districts);
            AppendSection(text, "construction sites", entries.Where(e => e.IsConstructionSite).ToList());
            text.AppendLine($"simulation expects {expected} ({underConstruction} more under construction): {expected - districts.Count} never built");
            return text.ToString();
        }

        private static void AppendSection(StringBuilder text, string label, List<DistrictArt> entries)
        {
            var notLoaded = entries.Where(e => e.HasArt && !e.Loaded).ToList();
            var noArt = entries.Where(e => !e.HasArt).ToList();
            var componentDown = entries.Where(e => e.HasArt && e.Loaded && !e.ComponentLoaded).ToList();
            var ready = entries.Count - notLoaded.Count - noArt.Count - componentDown.Count;
            text.AppendLine($"{label} {entries.Count}: ready {ready}, art found but not loaded {notLoaded.Count}, no art {noArt.Count}, component not loaded {componentDown.Count}");
            AppendGroups(text, "not loaded", notLoaded);
            AppendGroups(text, "no art", noArt);
            AppendGroups(text, "component not loaded", componentDown);
        }

        private static void AppendGroups(StringBuilder text, string label, List<DistrictArt> failed)
        {
            foreach (var group in failed.GroupBy(d => (d.Definition, d.Style)).OrderByDescending(g => g.Count()))
            {
                var empires = string.Join(",", group.Select(d => d.Empire).Distinct().OrderBy(e => e));
                text.AppendLine($"  {group.Count()} x {label}: {group.Key.Definition} / {group.Key.Style} (empires {empires})");
            }
        }
    }
}
