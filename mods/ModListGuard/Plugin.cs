using System;
using System.IO;
using System.Linq;
using Amplitude.Framework;
using Path = System.IO.Path;
using Amplitude.Framework.Runtime;
using Amplitude.Mercury.Runtime;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using HumankindMods.Core;
using UnityEngine;
using FrameworkRuntimeManager = Amplitude.Framework.Runtime.RuntimeManager;

namespace ModListGuard
{
    /// <summary>
    /// Stops the game wiping the saved mod list: repairs outdated module names, backs up every list that
    /// loses a module to Documents/Humankind/ModListGuard/history.jsonl, and restores the newest backup on a key.
    /// </summary>
    [BepInPlugin("com.zkidz.humankind.modlistguard", "ModListGuard", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static LineLog? log;
        private static string historyPath = "";
        private static string? culprit;
        private static string? ownSaveReason;
        private ConfigEntry<KeyboardShortcut> restoreKey = null!;

        internal static HookGuard Guard { get; } = new HookGuard(message => log?.Write(message));

        internal static string Join(RuntimeModuleConfiguration[] configuration) =>
            string.Join("/", configuration.Select(c => c.ModuleName.ToString()));

        internal static void NoteCulprit(Exception exception)
        {
            culprit = exception.Message;
            log?.Write("game is resetting the mod list after: " + exception.Message);
        }

        /// <summary>Only the list the player saved is repaired; save loads and menu previews pass through unchanged.</summary>
        internal static RuntimeModuleConfiguration[]? RepairIfSaved(FrameworkRuntimeManager manager, RuntimeModuleConfiguration[] configuration)
        {
            var service = ConfigurationService();
            if (service == null) return null; // not bound yet during the bootstrap reload
            var saved = Join(service.GetSavedConfiguration());
            if (!string.Equals(saved, Join(configuration), StringComparison.OrdinalIgnoreCase)) return null;

            var installed = manager.GetRuntimeModuleDatabase().Keys.Select(k => k.ToString()).ToList();
            var result = ModListRepair.Repair(configuration.Select(c => c.ModuleName.ToString()).ToList(), installed);
            if (!result.Changed) return null;
            log?.Write($"module database ({installed.Count}): {string.Join(" | ", installed)}");

            var repaired = result.Modules.Select(name => new RuntimeModuleConfiguration(name)).ToArray();
            log?.Write($"repaired saved list: replaced [{string.Join(", ", result.Replaced)}], dropped [{string.Join(", ", result.Dropped)}]");
            culprit = result.Dropped.Count > 0 ? "not installed: " + string.Join(", ", result.Dropped) : null;
            ownSaveReason = "repaired by ModListGuard";
            service.SaveConfiguration(repaired);
            return repaired;
        }

        internal static void RecordIfLoss(string newList, Func<string> callers)
        {
            var service = ConfigurationService();
            if (service == null) return;
            var oldList = Join(service.GetSavedConfiguration());
            var reason = ownSaveReason ?? callers();
            ownSaveReason = null;
            if (!ModListHistory.IsLoss(oldList, newList)) return;

            File.AppendAllText(historyPath, ModListHistory.ToJsonLine(DateTime.UtcNow, reason, culprit, oldList, newList) + "\n");
            log?.Write($"backed up mod list before it lost modules ({reason}): {oldList}");
            culprit = null;
        }

        private static IRuntimeConfigurationService? ConfigurationService() => Services.GetService<IRuntimeConfigurationService>();

        private void Awake()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Humankind", "ModListGuard");
            log = new LineLog(Path.Combine(dir, "guard.log"));
            historyPath = Path.Combine(dir, "history.jsonl");
            restoreKey = Config.Bind("ModListGuard", "RestoreKey", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl), "On the main menu, restores the newest backed-up mod list.");

            var harmony = new Harmony("com.zkidz.humankind.modlistguard");
            try
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception exception)
            {
                log.Write("patching failed, guard inactive: " + exception);
                Logger.LogError("ModListGuard patching failed: " + exception.Message);
                return;
            }

            var patched = harmony.GetPatchedMethods().Select(m => m.Name).ToList();
            log.Write("session start, patched: " + string.Join(", ", patched));
            Logger.LogInfo("ModListGuard loaded, " + patched.Count + " patches applied: " + string.Join(", ", patched));
        }

        private void Update()
        {
            if (!restoreKey.Value.IsDown()) return;
            Guard.Run("ModListGuard.Restore", Restore);
        }

        private void Restore()
        {
            var runtimeService = Services.GetService<Amplitude.Framework.Runtime.IRuntimeService>();
            if (runtimeService?.Runtime?.FiniteStateMachine?.CurrentState?.GetType().Name != "RuntimeState_OutGame")
            {
                log?.Write("restore ignored: only works on the main menu");
                return;
            }

            var list = File.Exists(historyPath) ? ModListHistory.FindNewestRestorable(File.ReadAllLines(historyPath)) : null;
            if (list == null)
            {
                log?.Write("restore ignored: no backed-up list");
                return;
            }

            var configuration = list.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(name => new RuntimeModuleConfiguration(name)).ToArray();
            log?.Write("restoring mod list: " + list);
            ConfigurationService()?.SaveConfiguration(configuration);
            runtimeService.ReloadRuntime(configuration);
        }
    }
}
