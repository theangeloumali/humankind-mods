using System;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using HumankindMods.Core;

namespace HumankindMods.Shared
{
    /// <summary>Linked into every plugin (Plugin.props), since Core has no Harmony or game references.</summary>
    internal static class PluginSupport
    {
        /// <summary>Patches the calling plugin's assembly; on failure the plugin stays inert instead of half-patched.</summary>
        public static bool TryPatchAll(string pluginGuid, LineLog log, ManualLogSource logger, Action? prepare = null)
        {
            var harmony = new Harmony(pluginGuid);
            try
            {
                prepare?.Invoke();
                harmony.PatchAll(typeof(PluginSupport).Assembly);
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                log.Write("patching failed, plugin inactive: " + exception);
                logger.LogError("patching failed, plugin inactive: " + exception.Message);
                return false;
            }

            var patched = string.Join(", ", harmony.GetPatchedMethods().Select(m => m.Name));
            log.Write("session start, patched: " + patched);
            logger.LogInfo("loaded, patches applied: " + patched);
            return true;
        }

        public static int CurrentTurn() => Amplitude.Mercury.Interop.AI.Snapshots.Game?.Turn ?? -1;
    }
}
