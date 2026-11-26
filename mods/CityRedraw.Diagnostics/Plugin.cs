using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HumankindMods.Core;
using HumankindMods.Shared;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    /// <summary>Logs map-drawing failures (art lookups that find nothing, a district census on demand) and keeps a
    /// throwing map handler from stopping the ones after it (PresentationSnapshotPatch). It must never hook the
    /// spawn path: patching PresentationLevelBuildComponent.RefreshChannel stopped every building drawing.</summary>
    [BepInPlugin(PluginGuid, "CityRedraw Diagnostics", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.zkidz.humankind.cityredraw.diagnostics";

        private static LineLog log = null!;

        private ConfigEntry<KeyboardShortcut> reportKey = null!;
        private ConfigEntry<KeyboardShortcut> tourKey = null!;
        private readonly CameraTour tour = new CameraTour();
        private GuardedHook updateHook = null!;
        private Action updateBody = null!;
        private string outputDir = "";
        private bool active;
        private int lastTurn = -1;
        private int lastMissingArt;

        internal static GuardedHook NarrativeHook { get; } = NewHook("Narrative.Prefix");
        internal static GuardedHook MissingArtHook { get; } = NewHook("MissingArt.Postfix");
        internal static DistinctReporter MissingArt { get; } = new DistinctReporter(300);

        internal static void Log(string message) => log.Write(message);

        private static GuardedHook NewHook(string name) => new GuardedHook(name, static message => log.Write(message));

        private void Awake()
        {
            reportKey = Config.Bind("Diagnostics", "ReportKey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl), "Writes the district census now.");
            tourKey = Config.Bind("Diagnostics", "CameraTourKey", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl), "Focuses the camera on the next district or construction site.");
            outputDir = Path.Combine(Paths.BepInExRootPath, "CityRedraw");
            log = new LineLog(Path.Combine(outputDir, "diagnostics.log"));
            updateHook = NewHook("Plugin.Update");
            updateBody = UpdateBody;
            active = PluginSupport.TryPatchAll(PluginGuid, log, Logger, GameStateReader.EnsureReady);
        }

        private void Update()
        {
            if (active) updateHook.Run(updateBody);
        }

        private void UpdateBody()
        {
            if (reportKey.Value.IsDown()) WriteReport();
            if (tourKey.Value.IsDown()) tour.Advance();
            var turn = PluginSupport.CurrentTurn();
            if (turn == lastTurn || turn < 0) return;
            lastTurn = turn;
            if (MissingArt.Occurrences == lastMissingArt) return;
            log.Write($"turn {turn}: art lookups with no art {MissingArt.Occurrences}, distinct {MissingArt.DistinctSeen}");
            lastMissingArt = MissingArt.Occurrences;
        }

        private void WriteReport()
        {
            var turn = PluginSupport.CurrentTurn();
            var path = Path.Combine(outputDir, $"report-turn{turn}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var (expected, underConstruction) = DistrictCensusReader.Expected();
            var census = DistrictCensus.Format(DistrictCensusReader.Read(), expected, underConstruction);
            File.WriteAllText(path, $"CityRedraw district census, turn {turn}{Environment.NewLine}{census}");
            log.Write($"report written: {path}; {census.Split('\n')[0].Trim()}");
        }
    }
}
