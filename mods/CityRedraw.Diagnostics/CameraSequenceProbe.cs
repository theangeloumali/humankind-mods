using System.Linq;
using Amplitude.Mercury.Presentation;
using HarmonyLib;
using HumankindMods.Core;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    // A camera sequence (e.g. a nuclear explosion, shown to every empire) holds the turn and freezes visibility
    // and district sync until all its actions end (decompiled CameraSequenceExecuter:121-131, 327-355).
    // Read-only: logs each sequence's state changes and, after 20 s running, the actions not yet ended.
    [HarmonyPatch(typeof(CameraSequenceExecuter), nameof(CameraSequenceExecuter.ExecuteActions))]
    internal static class CameraSequenceProbe
    {
        private static readonly AccessTools.FieldRef<CameraSequenceExecuter, Amplitude.Mercury.Data.CameraSequenceDefinition> Definition =
            AccessTools.FieldRefAccess<CameraSequenceExecuter, Amplitude.Mercury.Data.CameraSequenceDefinition>("definition");
        private static readonly AccessTools.FieldRef<CameraSequenceExecuter, CameraSequenceActionProgress[]> Actions =
            AccessTools.FieldRefAccess<CameraSequenceExecuter, CameraSequenceActionProgress[]>("actionProgress");
        private static readonly AccessTools.FieldRef<CameraSequenceExecuter, int> ActionCount = AccessTools.FieldRefAccess<CameraSequenceExecuter, int>("actionProgressCount");
        private static readonly AccessTools.FieldRef<CameraSequenceExecuter, int> InfoIndex = AccessTools.FieldRefAccess<CameraSequenceExecuter, int>("infoIndex");
        private static readonly StateWatch<CameraSequenceExecuter> Watch = new StateWatch<CameraSequenceExecuter>();

        private static void Postfix(CameraSequenceExecuter __instance) =>
            Plugin.CameraSequenceHook.Run(__instance, static executer =>
            {
                var state = executer.State.ToString();
                var name = Definition(executer)?.name ?? "?";
                var change = Watch.Change(executer, state, Time.realtimeSinceStartup, initial: "None");
                if (change != null) Plugin.Log($"camera sequence {name} (#{InfoIndex(executer)}): {change}");
                if (!Watch.HeldTooLong(executer, state, Time.realtimeSinceStartup, limitSeconds: 20f)) return;
                var waiting = (Actions(executer) ?? new CameraSequenceActionProgress[0]).Take(ActionCount(executer))
                    .Where(action => action != null && !action.Ended).Select(action => $"{action.GetType().Name}(started {action.Started}, {action.Progress:0.00})");
                Plugin.Log($"camera sequence {name} (#{InfoIndex(executer)}) still {state} after 20 s; waiting on: {string.Join(", ", waiting)}");
            });
    }
}
