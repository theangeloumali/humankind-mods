using System.Reflection;
using Amplitude.Mercury.Fx;
using Amplitude.Mercury.Presentation;
using HarmonyLib;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    // Cause A: compare how many particles the adder actually wrote with how many were due.
    [HarmonyPatch(typeof(PresentationLevelBuildComponent), nameof(PresentationLevelBuildComponent.AddParticle))]
    internal static class AddParticlePatch
    {
        private static void Prefix(PresentationLevelBuildComponent __instance, FxComponentShurikenNewParticleAdder adder, out (ulong DueMask, int CountBefore) __state)
        {
            __state = Plugin.SpawnPrefixHook.Run(
                (__instance, adder),
                static s => (GameStateReader.DueChannelMask(s.__instance), s.adder.NewParticleCount),
                fallback: (0UL, 0));
        }

        private static void Postfix(PresentationLevelBuildComponent __instance, FxComponentShurikenNewParticleAdder adder, (ulong DueMask, int CountBefore) __state)
        {
            if (__state.DueMask == 0) return;
            Plugin.SpawnPostfixHook.Run((__instance, adder, __state), static s =>
            {
                Plugin.Remember(s.__instance);
                var written = s.adder.NewParticleCount - s.__state.CountBefore;
                Plugin.Tracker.OnSpawn(s.__instance.GetInstanceID(), s.__state.DueMask, written, Time.frameCount);
            });
        }
    }

    // Cause B: a refresh that bumped the revision while the art wasn't loaded queues nothing.
    [HarmonyPatch]
    internal static class RefreshChannelPatch
    {
        private static MethodBase TargetMethod() => GameStateReader.RefreshChannelMethod();

        private static void Postfix(PresentationLevelBuildComponent __instance, object[] __args)
        {
            Plugin.RefreshHook.Run((__instance, channel: __args[0]), static s =>
            {
                Plugin.Remember(s.__instance);
                var tile = s.__instance.WorldPosition;
                Plugin.Tracker.OnRefresh(
                    s.__instance.GetInstanceID(), GameStateReader.BoxedChannelIndex(s.channel), GameStateReader.BoxedHasMaterial(s.channel),
                    GameStateReader.BoxedAbleToSpawn(s.channel), tile.Column, tile.Row, Time.realtimeSinceStartup);
            });
        }
    }

    [HarmonyPatch(typeof(PresentationLevelBuildComponent), "Unload")]
    internal static class UnloadPatch
    {
        private static void Postfix(PresentationLevelBuildComponent __instance)
        {
            Plugin.UnloadHook.Run(__instance.GetInstanceID(), static id => Plugin.Forget(id));
        }
    }
}
