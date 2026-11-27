using System;
using System.Reflection;
using Amplitude.Graphics;
using Amplitude.Graphics.Fx;
using Amplitude.Mercury.Fx.GPUType;
using HarmonyLib;
using HumankindMods.Core;

namespace CityRedraw.Diagnostics
{
    // Late-game cities lose their buildings until a reload, while every district still has its art loaded.
    // Read-only: samples the GPU particle budgets that draw them (decompiled FxOutputLayer:1161-1174,
    // FxComponentRenderer:647-685, FxComponentCPUParticleAdder:48-68) so the one that fills up shows in the log.
    internal static class ParticleBudgetProbe
    {
        internal static readonly ParticleBudget Budget = new ParticleBudget();

        /// <summary>The busiest renderer's layer count and how many its header buffer holds, as of the last frame.</summary>
        internal static int CurrentLayers { get; private set; }
        internal static int LayersThatFit { get; private set; }
        private static int textureUsedPixels, textureTotalPixels;

        /// <summary>Peaks since the last call; the texture use is carried over because it only changes on a reallocation.</summary>
        internal static string TakeSummary()
        {
            Budget.RecordTexture(textureUsedPixels, textureTotalPixels);
            var summary = Budget.Summary(maxLayers: 6);
            Budget.Reset();
            return summary;
        }

        [HarmonyPatch(typeof(FxOutputLayer), nameof(FxOutputLayer.NeedCommandOrAllocationUpdate))]
        private static class LayerSample
        {
            private static readonly AccessTools.FieldRef<FxOutputLayer, int> Requested = AccessTools.FieldRefAccess<FxOutputLayer, int>("particleCountToAllocate");
            private static readonly AccessTools.FieldRef<FxOutputLayer, int> UpperBound = AccessTools.FieldRefAccess<FxOutputLayer, int>("allocatedParticleCountUpperBound");

            private static bool exceptionLogged;

            private static System.Exception? Finalizer(System.Exception? __exception, FxOutputLayer __instance, int layerIndex)
            {
                if (__exception != null && !exceptionLogged)
                {
                    exceptionLogged = true;
                    Plugin.Log($"layer {layerIndex} ({__instance.Name}) readback threw: {__exception.GetType().Name}: {__exception.Message}");
                }

                return __exception;
            }

            private static void Postfix(FxOutputLayer __instance, int layerIndex, ReadWriteBuffer1D<int> bufferInt) =>
                Plugin.ParticleBudgetHook.Run((__instance, layerIndex, bufferInt), static sample =>
                {
                    var (layer, index, counters) = sample;
                    var slot = FxComponentRenderer.GIRRenderSourceHeaderStart + index * FxComponentRenderer.GIRRenderSourceGeaderIndexPerLayer
                        + FxComponentRenderer.GIRREnderSourceHeaderOffsetPixelCount;
                    var content = counters?.ReadContent;
                    if (content == null || slot >= content.Length) return;
                    var rendered = content[slot] / Math.Max(1, layer.VisualParticlePixelCount);
                    if (index == 0) RecordPool(content);
                    Budget.RecordLayer(layer.Name, rendered, layer.AllocatedParticleCount, Requested(layer), UpperBound(layer));
                });
        }

        private static FxComponentEvolve? evolve;

        // Slot 0 restarts at NewParticleBufferSize each frame and the GPU counts each particle kept in the pool
        // (decompiled FxComponentEvolve:251-258, 308), so its readback is the pool's fill.
        private static void RecordPool(int[] content)
        {
            if (evolve == null) evolve = UnityEngine.Object.FindObjectOfType<FxComponentEvolve>();
            var settings = evolve == null ? null : evolve.Settings;
            if (settings == null || content.Length <= FxComponentEvolve.GlobalAllocatorIndexParticle) return;
            Budget.RecordParticlePool(content[FxComponentEvolve.GlobalAllocatorIndexParticle], settings.ParticleBufferSize);
        }

        [HarmonyPatch(typeof(FxComponentRenderer), "AllocAllOutputLayers")]
        private static class TextureSample
        {
            private static readonly AccessTools.FieldRef<FxComponentRenderer, int> UsedPixels = AccessTools.FieldRefAccess<FxComponentRenderer, int>("visualCurrentPixelIndex");
            private static readonly AccessTools.FieldRef<FxComponentRenderer, AbstractReadWriteBuffer2D> Texture =
                AccessTools.FieldRefAccess<FxComponentRenderer, AbstractReadWriteBuffer2D>("rwRenderTextureVisualParticle");

            private static void Postfix(FxComponentRenderer __instance) =>
                Plugin.ParticleBudgetHook.Run(__instance, static renderer =>
                {
                    var texture = Texture(renderer);
                    var side = renderer.Settings.VisualParticleBufferTextureSize;
                    textureTotalPixels = texture != null ? texture.Width * texture.Height : side * side;
                    textureUsedPixels = UsedPixels(renderer);
                });
        }

        // Records how many layers the renderer has against how many headers the buffer holds, and logs once if the
        // header loop throws (it does past the buffer's last layer, every frame).
        [HarmonyPatch(typeof(FxComponentRenderer), nameof(FxComponentRenderer.FillSharedGlobalData))]
        private static class LayerCountSample
        {
            private static readonly AccessTools.FieldRef<FxComponentRenderer, FxOutputLayer[]> Layers = AccessTools.FieldRefAccess<FxComponentRenderer, FxOutputLayer[]>("fxOutputLayers");
            private static bool exceptionLogged;

            private static System.Exception? Finalizer(System.Exception? __exception, FxComponentRenderer __instance, int[] globalIndexBuffer)
            {
                Plugin.ParticleBudgetHook.Run((__instance, globalIndexBuffer, __exception), static sample =>
                {
                    var (renderer, buffer, error) = sample;
                    var fit = FxLayerCapacity.LayersThatFit(buffer?.Length ?? 0, FxComponentRenderer.GIRRenderSourceHeaderStart,
                        FxComponentRenderer.GIRRenderSourceGeaderIndexPerLayer);
                    var count = Layers(renderer)?.Length ?? 0;
                    Budget.RecordLayerCount(count, fit);
                    CurrentLayers = count;
                    LayersThatFit = fit;
                    if (error == null || exceptionLogged) return;
                    exceptionLogged = true;
                    Plugin.Log($"FillSharedGlobalData threw with {Layers(renderer)?.Length} layers, buffer fits {fit}: {error}");
                });
                return __exception;
            }
        }

        [HarmonyPatch(typeof(FxComponentRenderer), nameof(FxComponentRenderer.GetLayerIndexAddItIFN))]
        private static class LayerAddedSample
        {
            private static void Prefix(FxOutputLayer layer, out bool __state) => __state = layer != null && !layer.Loaded;

            private static void Postfix(FxOutputLayer layer, bool __state)
            {
                if (__state) Plugin.ParticleBudgetHook.Run(layer, static added => Budget.RecordLayerAdded(added.Name));
            }
        }

        [HarmonyPatch]
        private static class CpuDropSample
        {
            private static readonly AccessTools.FieldRef<FxComponentCPUParticleAdder<CPUShurikenNewParticle>, ReadWriteBuffer1D<CPUShurikenNewParticle>> Buffer =
                AccessTools.FieldRefAccess<FxComponentCPUParticleAdder<CPUShurikenNewParticle>, ReadWriteBuffer1D<CPUShurikenNewParticle>>("cpuParticleBuffer");

            private static MethodBase TargetMethod() => AccessTools.Method(typeof(FxComponentCPUParticleAdder<CPUShurikenNewParticle>),
                nameof(FxComponentCPUParticleAdder<CPUShurikenNewParticle>.AddCPUParticle), new[] { typeof(CPUShurikenNewParticle).MakeByRefType() });

            // Same test as the game's (line 54): a full buffer drops the particle and logs only once per session.
            private static void Prefix(FxComponentCPUParticleAdder<CPUShurikenNewParticle> __instance) =>
                Plugin.ParticleBudgetHook.Run(__instance, static adder =>
                {
                    var buffer = Buffer(adder);
                    if (buffer != null && adder.NewParticleCount + 1 > buffer.Size) Budget.RecordCpuDrop();
                });
        }
    }
}
