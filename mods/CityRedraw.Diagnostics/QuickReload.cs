using System;
using Amplitude.Framework;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    /// <summary>Quick-saves, then loads that save once it is written: the game's own QuickSave and LoadLatestSave
    /// (decompiled GameManager:1145-1163). A reload rebuilds the FX layer list, so buildings draw again.</summary>
    internal static class QuickReload
    {
        private static readonly Type? ServiceType = AccessTools.TypeByName("Amplitude.Mercury.Game.IGameSerializationService");
        private static bool saving;

        /// <summary>Loads a save by title the way LoadLatestSave does (a state change to the out-game runtime state).</summary>
        internal static void LoadSave(string title)
        {
            var service = ServiceType == null ? null : Services.GetService(ServiceType);
            var descriptor = service == null ? null
                : AccessTools.Method(ServiceType, "RetrieveGameSaveDescriptorFromSave", new[] { typeof(string) }).Invoke(service, new object[] { title });
            if (descriptor == null)
            {
                Plugin.Log($"load save: '{title}' not found");
                return;
            }

            var outGame = AccessTools.TypeByName("Amplitude.Mercury.Runtime.RuntimeState_OutGame");
            Plugin.Log($"load save: loading '{title}'");
            Services.GetService<Amplitude.Framework.Runtime.IRuntimeService>().Runtime.FiniteStateMachine
                .PostStateChange(outGame, Activator.CreateInstance(AccessTools.TypeByName("Amplitude.Mercury.Runtime.RuntimeStateGameSaveDescriptorParameter"), descriptor));
        }

        internal static void Start()
        {
            if (saving) return;
            var service = ServiceType == null ? null : Services.GetService(ServiceType);
            if (service == null)
            {
                Plugin.Log("quick reload: no save service (not in a game)");
                return;
            }

            if (!(AccessTools.Method(ServiceType, "QuickSave").Invoke(service, null) is Amplitude.AsyncOperation save))
            {
                Plugin.Log("quick reload: quick-save did not start");
                return;
            }

            saving = true;
            Plugin.Log("quick reload: saving");
            save.UponCompletion(() => Plugin.QuickReloadHook.Run(service, static saved =>
            {
                saving = false;
                var loading = AccessTools.Method(ServiceType, "LoadLatestSave").Invoke(saved, null) is true;
                Plugin.Log(loading ? "quick reload: saved, loading it" : "quick reload: saved, but no valid save to load");
            }));
            save.UponException(exception =>
            {
                saving = false;
                Plugin.Log("quick reload: save failed: " + exception.Message);
            });
            save.UponCancel(() => saving = false);
        }
    }
}
