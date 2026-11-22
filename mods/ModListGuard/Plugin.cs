using System;
using System.IO;
using System.Linq;
using Amplitude.Framework;
using Amplitude.Framework.Runtime;
using Amplitude.Mercury.Runtime;
using BepInEx;
using BepInEx.Configuration;
using HumankindMods.Core;
using HumankindMods.Shared;
using UnityEngine;
using FrameworkRuntimeManager = Amplitude.Framework.Runtime.RuntimeManager;
using Path = System.IO.Path;

namespace ModListGuard
{
    /// <summary>
    /// Stops the game wiping the saved mod list: repairs it when a listed mod is missing, backs up every list
    /// that loses a module to Documents/Humankind/ModListGuard/history.jsonl, and restores the newest backup on a key.
    /// </summary>
    [BepInPlugin(PluginGuid, "ModListGuard", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.zkidz.humankind.modlistguard";

        private static LineLog log = null!;
        private static string historyPath = "";
        private static string? culprit;
        private static string? ownSaveReason;
        private ConfigEntry<KeyboardShortcut> restoreKey = null!;
        private GuardedHook restoreHook = null!;
        private bool active;

        internal static GuardedHook RepairHook { get; } = NewHook("CheckRuntimeConfiguration.Prefix");
        internal static GuardedHook SaveHook { get; } = NewHook("SaveConfiguration.Prefix");
        internal static GuardedHook CulpritHook { get; } = NewHook("Culprit.Prefix");

        internal static string Join(RuntimeModuleConfiguration[] configuration) =>
            ModListHistory.Join(configuration.Select(c => c.ModuleName.ToString()));

        internal static void NoteCulprit(Exception exception)
        {
            culprit = exception.Message;
            log.Write("game is resetting the mod list after: " + exception.Message);
        }

        /// <summary>Only the list the player saved is repaired; save loads and menu previews pass through unchanged.</summary>
        internal static RuntimeModuleConfiguration[]? RepairIfSaved(FrameworkRuntimeManager manager, RuntimeModuleConfiguration[] configuration)
        {
            var service = ConfigurationService();
            if (service == null) return null; // not bound yet during the bootstrap reload
            if (!string.Equals(Join(service.GetSavedConfiguration()), Join(configuration), StringComparison.OrdinalIgnoreCase)) return null;

            var installed = manager.GetRuntimeModuleDatabase().Keys.Select(k => k.ToString()).ToList();
            var result = ModListRepair.Repair(configuration.Select(c => c.ModuleName.ToString()).ToList(), installed);
            if (!result.Changed) return null;

            var repaired = result.Modules.Select(name => new RuntimeModuleConfiguration(name)).ToArray();
            log.Write($"repaired saved list: replaced [{string.Join(", ", result.Replaced)}], dropped [{string.Join(", ", result.Dropped)}]");
            log.Write($"module database ({installed.Count}): {string.Join(" | ", installed)}");
            culprit = result.Dropped.Count > 0 ? "not installed: " + string.Join(", ", result.Dropped) : null;
            ownSaveReason = "repaired by ModListGuard";
            service.SaveConfiguration(repaired);
            return repaired;
        }

        internal static void RecordIfLoss(string newList, Func<string> callers)
        {
            var ownReason = ownSaveReason;
            ownSaveReason = null;
            var service = ConfigurationService();
            if (service == null) return;
            var oldList = Join(service.GetSavedConfiguration());
            if (!ModListHistory.IsLoss(oldList, newList)) return;

            var reason = ownReason ?? callers();
            var line = ModListHistory.ToJsonLine(DateTime.UtcNow, reason, culprit, oldList, newList);
            if (ModListHistory.TryAppend(historyPath, line, log.Write))
                log.Write($"backed up mod list before it lost modules ({reason}): {oldList}");
            culprit = null;
        }

        private static IRuntimeConfigurationService? ConfigurationService() => Services.GetService<IRuntimeConfigurationService>();

        private static GuardedHook NewHook(string name) => new GuardedHook(name, static message => log.Write(message));

        private void Awake()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Humankind", "ModListGuard");
            log = new LineLog(Path.Combine(dir, "guard.log"));
            historyPath = Path.Combine(dir, "history.jsonl");
            restoreHook = NewHook("ModListGuard.Restore");
            restoreKey = Config.Bind("ModListGuard", "RestoreKey", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl), "On the main menu, restores the newest backed-up mod list.");
            active = PluginSupport.TryPatchAll(PluginGuid, log, Logger);
        }

        private void Update()
        {
            if (active && restoreKey.Value.IsDown()) restoreHook.Run(Restore);
        }

        private static void Restore()
        {
            var runtimeService = Services.GetService<Amplitude.Framework.Runtime.IRuntimeService>();
            if (runtimeService?.Runtime?.FiniteStateMachine?.CurrentState?.GetType().Name != "RuntimeState_OutGame")
            {
                log.Write("restore ignored: only works on the main menu");
                return;
            }

            var list = File.Exists(historyPath) ? ModListHistory.FindNewestRestorable(File.ReadAllLines(historyPath)) : null;
            if (list == null)
            {
                log.Write("restore ignored: no backed-up list");
                return;
            }

            var configuration = ModListHistory.Split(list).Select(name => new RuntimeModuleConfiguration(name)).ToArray();
            log.Write("restoring mod list: " + list);
            ConfigurationService()?.SaveConfiguration(configuration);
            runtimeService.ReloadRuntime(configuration);
        }
    }
}
