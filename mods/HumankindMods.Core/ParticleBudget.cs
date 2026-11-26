using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HumankindMods.Core
{
    /// <summary>Per-turn peaks of the GPU particle budgets that draw buildings: each render layer's drawn count
    /// against its allocation, the shared visual texture, and particles the CPU adder dropped.</summary>
    public sealed class ParticleBudget
    {
        private sealed class LayerPeak
        {
            public int Rendered, Allocated, Requested, UpperBound, FullSamples;
        }

        private readonly Dictionary<string, LayerPeak> layers = new Dictionary<string, LayerPeak>();
        private readonly List<string> added = new List<string>();
        private int textureUsedPixels, textureTotalPixels, cpuDrops, rendererLayers, layersThatFit;

        public bool HasData => layers.Count > 0 || cpuDrops > 0 || textureTotalPixels > 0 || rendererLayers > 0;

        public int SaturatedLayers => layers.Values.Count(layer => layer.FullSamples > 0);

        /// <summary>A layer is full when it drew as many particles as it holds; the GPU drops any beyond that.</summary>
        public void RecordLayer(string name, int rendered, int allocated, int requested, int upperBound)
        {
            if (!layers.TryGetValue(name, out var peak)) layers[name] = peak = new LayerPeak();
            var full = allocated <= 0 ? rendered > 0 : rendered >= allocated;
            if (full) peak.FullSamples++;
            if (rendered < peak.Rendered) return;
            peak.Rendered = rendered;
            peak.Allocated = allocated;
            peak.Requested = requested;
            peak.UpperBound = upperBound;
        }

        public void RecordTexture(int usedPixels, int totalPixels)
        {
            textureUsedPixels = Math.Max(textureUsedPixels, usedPixels);
            textureTotalPixels = totalPixels;
        }

        public void RecordCpuDrop() => cpuDrops++;

        /// <summary>A layer the renderer appended (it never removes one), to see what a long session accumulates.</summary>
        public void RecordLayerAdded(string name) => added.Add(name);

        public void RecordLayerCount(int layers, int layersThatFit)
        {
            rendererLayers = Math.Max(rendererLayers, layers);
            this.layersThatFit = layersThatFit;
        }

        public void Reset()
        {
            layers.Clear();
            added.Clear();
            textureUsedPixels = textureTotalPixels = cpuDrops = rendererLayers = layersThatFit = 0;
        }

        public string Summary(int maxLayers, int maxAdded = 12)
        {
            var percent = textureTotalPixels > 0 ? 100L * textureUsedPixels / textureTotalPixels : 0;
            var over = rendererLayers > layersThatFit ? " OVER" : "";
            var text = new StringBuilder()
                .Append($"renderer layers {rendererLayers}/{layersThatFit}{over}; ")
                .Append($"texture {percent}% ({textureUsedPixels}/{textureTotalPixels} px); cpu drops {cpuDrops}; ")
                .Append($"added {added.Count}");
            if (added.Count > 0) text.Append(": ").Append(string.Join(", ", added.Take(maxAdded)));
            if (added.Count > maxAdded) text.Append($", +{added.Count - maxAdded} more");
            text.Append($"; layers {layers.Count}, full {SaturatedLayers}");
            var busiest = layers
                .OrderByDescending(entry => entry.Value.FullSamples)
                .ThenByDescending(entry => entry.Value.Allocated > 0 ? (double)entry.Value.Rendered / entry.Value.Allocated : entry.Value.Rendered)
                .Take(maxLayers);
            foreach (var entry in busiest)
            {
                var peak = entry.Value;
                text.Append($"; {entry.Key} {peak.Rendered}/{peak.Allocated} (asked {peak.Requested}, cap {peak.UpperBound}, full {peak.FullSamples}x)");
            }

            return text.ToString();
        }
    }
}
