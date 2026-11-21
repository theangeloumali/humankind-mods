using System;
using System.Collections.Generic;
using System.IO;
using Amplitude.Mercury.Presentation;
using BepInEx;
using BepInEx.Configuration;
using HumankindMods.Core;
using HumankindMods.Shared;
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
        private static readonly List<int> Destroyed = new List<int>();
        private static readonly Func<int, int, bool> IsAbleToSpawn = static (id, channel) =>
            Components.TryGetValue(id, out var component) && GameStateReader.IsAbleToSpawn(component, channel);

        private static LineLog log = null!;

        private ConfigEntry<KeyboardShortcut> reportKey = null!;
        private ConfigEntry<float> sweepSeconds = null!;
        private GuardedHook updateHook = null!;
        private Action updateBody = null!;
        private string outputDir = "";
        private bool active;
        private float nextSweep;
        private int lastTurn = -1;
        private int? lastReportTurn;
        private SpawnCounts lastCounts;

        internal static DistrictSpawnTracker Tracker { get; } = new DistrictSpawnTracker(StuckAfterSeconds);
        internal static GuardedHook SpawnPrefixHook { get; } = NewHook("AddParticle.Prefix");
        internal static GuardedHook SpawnPostfixHook { get; } = NewHook("AddParticle.Postfix");
        internal static GuardedHook RefreshHook { get; } = NewHook("RefreshChannel.Postfix");
        internal static GuardedHook UnloadHook { get; } = NewHook("Unload.Postfix");

        internal static void Remember(PresentationLevelBuildComponent component) => Components[component.GetInstanceID()] = component;

        internal static void Forget(int componentId)
        {
            Components.Remove(componentId);
            Tracker.OnUnload(componentId);
        }

        private static GuardedHook NewHook(string name) => new GuardedHook(name, static message => log.Write(message));

        private void Awake()
        {
            reportKey = Config.Bind("Diagnostics", "ReportKey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl), "Writes a report now.");
            sweepSeconds = Config.Bind("Diagnostics", "SweepSeconds", 5f, "How often waiting districts are re-checked.");
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
            if (reportKey.Value.IsDown()) WriteReport("hotkey");
            if (Time.realtimeSinceStartup >= nextSweep)
            {
                nextSweep = Time.realtimeSinceStartup + sweepSeconds.Value;
                Sweep();
            }

            var turn = PluginSupport.CurrentTurn();
            if (turn != lastTurn && turn >= 0) OnNewTurn(turn);
        }

        private static void Sweep()
        {
            Destroyed.Clear();
            foreach (var pair in Components)
            {
                if (pair.Value == null) Destroyed.Add(pair.Key);
            }

            foreach (var id in Destroyed) Forget(id);
            Tracker.Sweep(Time.realtimeSinceStartup, IsAbleToSpawn);
        }

        private void OnNewTurn(int turn)
        {
            lastTurn = turn;
            var counts = Tracker.GetCounts();
            if (!counts.Equals(lastCounts))
            {
                log.Write($"turn {turn}: components {Components.Count}, B-pending {counts.BPending}, B-orphaned {counts.BOrphaned}, " +
                          $"B-stuck {counts.BStuck}, A-dropped {counts.ADropped}, drops {counts.TotalDrops}");
                lastCounts = counts;
            }

            if (counts.Invisible > 0 && (lastReportTurn == null || turn - lastReportTurn >= ReportEveryTurns)) WriteReport("auto");
        }

        private void WriteReport(string trigger)
        {
            var turn = PluginSupport.CurrentTurn();
            lastReportTurn = turn;
            var path = Path.Combine(outputDir, $"report-turn{turn}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, ReportFormatter.Format(Tracker, turn, Components.Count, ReportRows));
            log.Write($"report ({trigger}) written: {path}");
        }
    }
}
