using System;

namespace HumankindMods.Core
{
    /// <summary>The FX renderer writes a 4-int header per output layer into the shared int buffer, starting at a
    /// fixed slot (decompiled FxComponentRenderer:507-515); the buffer's size caps how many layers can draw.</summary>
    public static class FxLayerCapacity
    {
        public static int LayersThatFit(int bufferInts, int headerStart, int intsPerLayer) =>
            Math.Max(0, (bufferInts - headerStart) / intsPerLayer);
    }
}
