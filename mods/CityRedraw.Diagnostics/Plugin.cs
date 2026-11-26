using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HumankindMods.Core;
using HumankindMods.Shared;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    /// <summary>Logs map-drawing failures (art lookups that find nothing, a district census on demand, the GPU
    /// particle budgets that draw buildings, per turn) and sandbox
    /// exceptions with their stack. Three guards change the game: a throwing map handler no longer stops the
    /// ones after it (PresentationSnapshotPatch), the independents' army upgrade survives a rung no technology
    /// unlocks (MinorFactionUpgradeGuard), an army with a zero speed stops instead of breaking the turn
    /// (ArmyMoveGuard). Near the FX layer limit (960; past it buildings stop drawing) it warns on screen and, at a new turn,
    /// quick-saves and reloads (QuickReload), which rebuilds the layer list. It must never hook the spawn path: patching
    /// PresentationLevelBuildComponent.RefreshChannel stopped every building drawing.</summary>
    [BepInPlugin(PluginGuid, "CityRedraw Diagnostics", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.zkidz.humankind.cityredraw.diagnostics";

        private static LineLog log = null!;

        private ConfigEntry<KeyboardShortcut> reportKey = null!;
        private ConfigEntry<KeyboardShortcut> tourKey = null!;
        private ConfigEntry<KeyboardShortcut> layerFillKey = null!;
        private ConfigEntry<KeyboardShortcut> reloadKey = null!;
        private ConfigEntry<int> warnAt = null!;
        private ConfigEntry<int> autoReloadAt = null!;
        private ConfigEntry<KeyboardShortcut> loadSaveKey = null!;
        private ConfigEntry<string> loadSaveTitle = null!;
        private ConfigEntry<int> shotEveryTurns = null!;
        private int layersAfterLoad = -1;
        private int lastLayers;
        private float nextShotRequestCheck;
        private GuardedHook guiHook = null!;
        private Action guiBody = null!;
        private GUIStyle? bannerStyle;
        private bool warningLogged;
        private ConfigEntry<int> layerFillTarget = null!;
        private readonly CameraTour tour = new CameraTour();
        private GuardedHook updateHook = null!;
        private Action updateBody = null!;
        private string outputDir = "";
        private bool active;
        private int lastTurn = -1;
        private int lastMissingArt;
        private bool contractsWritten;

        internal static GuardedHook NarrativeHook { get; } = NewHook("Narrative.Prefix");
        internal static GuardedHook MissingArtHook { get; } = NewHook("MissingArt.Postfix");
        internal static GuardedHook ParticleBudgetHook { get; } = NewHook("ParticleBudget");
        internal static GuardedHook QuickReloadHook { get; } = NewHook("QuickReload");
        internal static GuardedHook CameraSequenceHook { get; } = NewHook("CameraSequence.Postfix");
        internal static DistinctReporter MissingArt { get; } = new DistinctReporter(300);
        internal static DistinctReporter UpgradeStops { get; } = new DistinctReporter(50);
        internal static DistinctReporter ZeroSpeedArmies { get; } = new DistinctReporter(50);

        internal static void Log(string message) => log.Write(message);

        private static GuardedHook NewHook(string name) => new GuardedHook(name, static message => log.Write(message));

        private void Awake()
        {
            reportKey = Config.Bind("Diagnostics", "ReportKey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl), "Writes the district census now.");
            reloadKey = Config.Bind("LayerLimit", "QuickReloadKey", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Quick-saves and loads that save, which brings back buildings lost to the FX layer limit.");
            warnAt = Config.Bind("LayerLimit", "WarnAtLayers", 940, "Shows the on-screen warning from this many FX layers (the renderer draws 960).");
            autoReloadAt = Config.Bind("LayerLimit", "AutoReloadAtLayers", 945,
                "At a new turn with this many FX layers, quick-saves and reloads so buildings keep drawing. 0 turns it off.");
            loadSaveKey = Config.Bind("Diagnostics", "LoadSaveKey", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftControl), "Loads the save named in LoadSaveTitle.");
            shotEveryTurns = Config.Bind("Diagnostics", "ScreenshotEveryTurns", 0,
                "Saves the game's own frame (works behind a locked desktop) to CityRedraw/shots every N turns. 0 = off.");
            loadSaveTitle = Config.Bind("Diagnostics", "LoadSaveTitle", "", "Save the load key opens, e.g. 'auto-save 917'. Empty: the key does nothing.");
            tourKey = Config.Bind("Diagnostics", "CameraTourKey", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl), "Focuses the camera on the next district or construction site.");
            layerFillKey = Config.Bind("Diagnostics", "LayerFillKey", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Adds empty FX layers up to LayerFillTarget, to reproduce the late-game layer overflow.");
            layerFillTarget = Config.Bind("Diagnostics", "LayerFillTarget", 1000, "Layer count the fill key brings the busiest FX renderer to.");
            outputDir = Path.Combine(Paths.BepInExRootPath, "CityRedraw");
            log = new LineLog(Path.Combine(outputDir, "diagnostics.log"));
            updateHook = NewHook("Plugin.Update");
            updateBody = UpdateBody;
            guiHook = NewHook("Plugin.OnGUI");
            guiBody = DrawLayerWarning;
            active = PluginSupport.TryPatchAll(PluginGuid, log, Logger, GameStateReader.EnsureReady);
        }

        private void Update()
        {
            if (active) updateHook.Run(updateBody);
        }

        private void OnGUI()
        {
            if (active) guiHook.Run(guiBody);
        }

        private void DrawLayerWarning()
        {
            var message = LayerLimitWarning.Message(ParticleBudgetProbe.CurrentLayers, warnAt.Value, ParticleBudgetProbe.LayersThatFit, reloadKey.Value.ToString());
            if (message == null) return;
            if (!warningLogged)
            {
                warningLogged = true;
                log.Write("layer limit warning shown: " + message);
            }

            bannerStyle ??= new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.yellow;
            var width = Screen.width * 0.5f;
            GUI.Box(new Rect((Screen.width - width) / 2, Screen.height * 0.08f, width, Screen.height * 0.06f), message, bannerStyle);
        }

        private void UpdateBody()
        {
            if (reportKey.Value.IsDown()) WriteReport();
            if (tourKey.Value.IsDown()) tour.Advance();
            if (layerFillKey.Value.IsDown()) FxLayerFill.FillTo(layerFillTarget.Value);
            if (reloadKey.Value.IsDown()) QuickReload.Start();
            if (loadSaveKey.Value.IsDown() && loadSaveTitle.Value.Length > 0) QuickReload.LoadSave(loadSaveTitle.Value);
            CaptureRequestedShot();
            var turn = PluginSupport.CurrentTurn();
            if (!contractsWritten && turn >= 0 && EntityContractDump.TryWrite(Path.Combine(outputDir, "entity-contracts.json")))
            {
                contractsWritten = true;
                log.Write("entity contracts written");
            }

            if (turn == lastTurn || turn < 0) return;
            lastTurn = turn;
            ReloadIfLayersNearLimit();
            CaptureTurnShot(turn);
            log.Write($"turn {turn}: particle budget: {ParticleBudgetProbe.TakeSummary()}");
            if (MissingArt.Occurrences == lastMissingArt) return;
            log.Write($"turn {turn}: art lookups with no art {MissingArt.Occurrences}, distinct {MissingArt.DistinctSeen}");
            lastMissingArt = MissingArt.Occurrences;
        }

        // shoot.txt in the output folder asks for a frame now: the only way to see the game behind a locked desktop.
        private void CaptureRequestedShot()
        {
            if (Time.realtimeSinceStartup < nextShotRequestCheck) return;
            nextShotRequestCheck = Time.realtimeSinceStartup + 2f;
            var request = Path.Combine(outputDir, "shoot.txt");
            if (!File.Exists(request)) return;
            File.Delete(request);
            var dir = Path.Combine(outputDir, "shots");
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"requested-{DateTime.Now:HHmmss}.png"));
        }

        private void CaptureTurnShot(int turn)
        {
            if (!TurnShotPolicy.ShouldCapture(turn, shotEveryTurns.Value)) return;
            var dir = Path.Combine(outputDir, "shots");
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"turn-{turn}-{DateTime.Now:HHmmss}.png"));
        }

        // A load rebuilds the renderer, so a drop in the layer count marks a new baseline.
        private void ReloadIfLayersNearLimit()
        {
            var layers = ParticleBudgetProbe.CurrentLayers;
            if (layersAfterLoad < 0 || layers < lastLayers) layersAfterLoad = layers;
            lastLayers = layers;
            if (!AutoReloadPolicy.ShouldReload(layers, autoReloadAt.Value, layersAfterLoad, minGrowth: 20)) return;
            log.Write($"auto reload: {layers} FX layers (after load {layersAfterLoad}, limit {ParticleBudgetProbe.LayersThatFit})");
            QuickReload.Start();
        }

        private void WriteReport()
        {
            var turn = PluginSupport.CurrentTurn();
            var path = Path.Combine(outputDir, $"report-turn{turn}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var (expected, underConstruction) = DistrictCensusReader.Expected();
            var census = DistrictCensus.Format(DistrictCensusReader.Read(), expected, underConstruction);
            File.WriteAllText(path, $"CityRedraw district census, turn {turn}{Environment.NewLine}{census}");
            log.Write($"report written: {path}; {census.Split('\n')[0].Trim()}");
            log.Write($"particle budget now: {ParticleBudgetProbe.TakeSummary()}");
        }
    }
}
