using Amplitude.Graphics.Fx;
using HarmonyLib;
using HumankindMods.Core;

namespace CityRedraw.Diagnostics
{
    // Every building, tree and prop lives in one GPU particle pool sized 2^24 uints (FxComponentEvolveSettings:15).
    // A late game with ~7,500 districts needs more (16.9M at turn 150 with order 14), so each frame the overflow
    // is dropped; building particles spawn once, so dropped buildings stay gone until a reload. The shaders read
    // the buffer's real size (FxComponentEvolve:458-463), so creating it larger is safe.
    [HarmonyPatch(typeof(FxComponentEvolve), "Load")]
    internal static class ParticlePoolGuard
    {
        private static void Prefix(FxComponentEvolve __instance) =>
            Plugin.ParticlePoolHook.Run(__instance, static evolve =>
            {
                var settings = evolve.Settings;
                if (settings == null) return;
                var size = Plugin.ParticlePoolTestSize > 0 ? Plugin.ParticlePoolTestSize : ParticlePoolSizing.Choose(settings.ParticleBufferSize, Plugin.ParticlePoolSize);
                if (size == settings.ParticleBufferSize) return;
                Plugin.Log($"particle pool raised from {settings.ParticleBufferSize} to {size} on {evolve.name}");
                settings.ParticleBufferSize = size;
            });
    }
}
