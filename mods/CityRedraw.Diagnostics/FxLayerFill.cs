using System.Linq;
using Amplitude.Graphics.Fx;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    /// <summary>Reproduces the late-game layer overflow on demand: clones the renderer's empty debug layer until the
    /// busiest FX renderer holds the target count, as a long session does with building sets.</summary>
    internal static class FxLayerFill
    {
        private static readonly AccessTools.FieldRef<FxComponentRenderer, FxOutputLayer[]> Layers = AccessTools.FieldRefAccess<FxComponentRenderer, FxOutputLayer[]>("fxOutputLayers");

        internal static void FillTo(int target)
        {
            var renderer = UnityEngine.Object.FindObjectsOfType<FxComponentRenderer>().OrderByDescending(candidate => Layers(candidate)?.Length ?? 0).FirstOrDefault();
            var template = renderer == null ? null : Layers(renderer)?.FirstOrDefault(layer => layer != null && layer.Name.Contains("Debug"));
            if (renderer == null || template == null)
            {
                Plugin.Log("layer fill: no renderer with a debug layer");
                return;
            }

            var before = Layers(renderer).Length;
            while (Layers(renderer).Length < target) renderer.GetLayerIndexAddItIFN(UnityEngine.Object.Instantiate(template));
            Plugin.Log($"layer fill: {renderer.name} {before} -> {Layers(renderer).Length} layers");
        }
    }
}
