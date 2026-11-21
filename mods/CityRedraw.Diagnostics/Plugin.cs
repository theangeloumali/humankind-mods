using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amplitude.Mercury.Presentation;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using HumankindMods.Core;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    /// <summary>Read-only: logs districts whose model was retired and never respawned. Changes nothing in the game.</summary>
    [BepInPlugin(PluginGuid, "CityRedraw Diagnostics", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.zkidz.humankind.cityredraw.diagnostics";
        private const int ReportEveryTurns = 10;
        private const int ReportRows = 200;
        private const double StuckAfterSeconds = 60;

        private static readonly Dictionary<int, PresentationLevelBuildComponent> Components = new Dictionary<int, PresentationLevelBuildComponent>();
        private static LineLog? log;

        private ConfigEntry<KeyboardShortcut> reportKey = null!;
        private ConfigEntry<float> sweepSeconds = null!;
        private string outputDir = "";
        private float nextSweep;
        private int lastTurn = -1;
        private int lastReportTurn = int.MinValue;
        private SpawnCounts lastCounts;

        internal static DistrictSpawnTracker Tracker { get; } = new DistrictSpawnTracker(StuckAfterSeconds);
        internal static HookGuard Guard { get; } = new HookGuard(message => Write(message));

        internal static void Remember(PresentationLevelBuildComponent component) => Components[component.GetInstanceID()] = component;

        internal static void Forget(int componentId)
        {
            Components.Remove(componentId);
            Tracker.OnUnload(componentId);
        }

        private static void Write(string message) => log?.Write(message);

        private void Awake()
        {
            reportKey = Config.Bind("Diagnostics", "ReportKey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl), "Writes a report now.");
            sweepSeconds = Config.Bind("Diagnostics", "SweepSeconds", 5f, "How often waiting districts are re-checked.");
            outputDir = Path.Combine(Paths.BepInExRootPath, "CityRedraw");
            log = new LineLog(Path.Combine(outputDir, "diagnostics.log"));

            var harmony = new Harmony(PluginGuid);
            try
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception exception)
            {
                Write("patching failed, diagnostics inactive: " + exception);
                Logger.LogError("CityRedraw Diagnostics patching failed: " + exception.Message);
                return;
            }

            var patched = harmony.GetPatchedMethods().Select(m => m.Name).ToList();
            Write("session start, patched: " + string.Join(", ", patched));
            Logger.LogInfo("CityRedraw Diagnostics loaded, " + patched.Count + " patches applied: " + string.Join(", ", patched));
        }

        private void Update()
        {
            Guard.Run("Plugin.Update", () =>
            {
                if (reportKey.Value.IsDown()) WriteReport("hotkey");
                if (Time.realtimeSinceStartup >= nextSweep)
                {
                    nextSweep = Time.realtimeSinceStartup + sweepSeconds.Value;
                    Sweep();
                }

                var turn = CurrentTurn();
                if (turn != lastTurn && turn >= 0) OnNewTurn(turn);
            });
        }

        private void Sweep()
        {
            foreach (var destroyed in Components.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
            {
                Forget(destroyed);
            }

            Tracker.Sweep(Time.realtimeSinceStartup, (id, channel) =>
                Components.TryGetValue(id, out var component) && GameStateReader.IsAbleToSpawn(component, channel));
        }

        private void OnNewTurn(int turn)
        {
            lastTurn = turn;
            var counts = Tracker.GetCounts();
            if (!counts.Equals(lastCounts))
            {
                Write($"turn {turn}: components {Components.Count}, B-pending {counts.BPending}, B-orphaned {counts.BOrphaned}, " +
                      $"B-stuck {counts.BStuck}, A-dropped {counts.ADropped}, drops {counts.TotalDrops}");
                lastCounts = counts;
            }

            var firstInvisible = counts.Invisible > 0 && lastReportTurn == int.MinValue;
            if (firstInvisible || (counts.Invisible > 0 && turn - lastReportTurn >= ReportEveryTurns))
            {
                WriteReport("auto");
            }
        }

        private void WriteReport(string trigger)
        {
            var turn = CurrentTurn();
            lastReportTurn = turn;
            var path = Path.Combine(outputDir, $"report-turn{turn}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, ReportFormatter.Format(Tracker, turn, Components.Count, ReportRows));
            Write($"report ({trigger}) written: {path}");
        }

        private static int CurrentTurn() => Amplitude.Mercury.Interop.AI.Snapshots.Game?.Turn ?? -1;
    }
}
