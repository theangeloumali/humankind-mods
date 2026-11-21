using System.Collections.Generic;
using System.Reflection;
using Amplitude.Mercury.Fx;
using Amplitude.Mercury.Presentation;
using HarmonyLib;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    internal sealed class SpawnProbe
    {
        public SpawnProbe(List<int> channels, int countBefore)
        {
            Channels = channels;
            CountBefore = countBefore;
        }

        public List<int> Channels { get; }
        public int CountBefore { get; }
    }

    // Cause A: compare how many particles the adder actually wrote with how many were due.
    [HarmonyPatch(typeof(PresentationLevelBuildComponent), nameof(PresentationLevelBuildComponent.AddParticle))]
    internal static class AddParticlePatch
    {
        private static void Prefix(PresentationLevelBuildComponent __instance, FxComponentShurikenNewParticleAdder adder, out SpawnProbe? __state)
        {
            SpawnProbe? probe = null;
            Plugin.Guard.Run("AddParticle.Prefix", () =>
            {
                var channels = GameStateReader.ChannelsToSpawn(__instance);
                if (channels.Count > 0) probe = new SpawnProbe(channels, adder.NewParticleCount);
            });
            __state = probe;
        }

        private static void Postfix(PresentationLevelBuildComponent __instance, FxComponentShurikenNewParticleAdder adder, SpawnProbe? __state)
        {
            if (__state == null) return;
            Plugin.Guard.Run("AddParticle.Postfix", () =>
            {
                Plugin.Remember(__instance);
                Plugin.Tracker.OnSpawn(__instance.GetInstanceID(), __state.Channels, adder.NewParticleCount - __state.CountBefore, Time.frameCount);
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
            Plugin.Guard.Run("RefreshChannel.Postfix", () =>
            {
                Plugin.Remember(__instance);
                var channel = GameStateReader.ReadChannel(__args[0]);
                var tile = __instance.WorldPosition;
                Plugin.Tracker.OnRefresh(
                    __instance.GetInstanceID(), channel.Index, channel.HasMaterial, channel.AbleToSpawn,
                    tile.Column, tile.Row, Time.realtimeSinceStartup);
            });
        }
    }

    [HarmonyPatch(typeof(PresentationLevelBuildComponent), "Unload")]
    internal static class UnloadPatch
    {
        private static void Postfix(PresentationLevelBuildComponent __instance)
        {
            Plugin.Guard.Run("Unload.Postfix", () => Plugin.Forget(__instance.GetInstanceID()));
        }
    }
}
