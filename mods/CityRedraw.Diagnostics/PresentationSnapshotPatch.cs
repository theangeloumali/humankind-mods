using System;
using Amplitude.Mercury.Interop;
using HarmonyLib;
using HumankindMods.Core;
using Collection = Amplitude.Mercury.Interop.Snapshot.PresentationDataChangedCollection;

namespace CityRedraw.Diagnostics
{
    // The game's Invoke stops at the first handler that throws, so one undrawable unit kept the city and
    // district handlers behind it from ever running (soaks b and c). This is the same loop, decompiled from
    // Amplitude.Mercury.Firstpass, with each handler isolated; the cursor stays the game's own array slot
    // because its "-" operator moves it back when a handler unsubscribes mid-loop.
    [HarmonyPatch(typeof(Snapshot.PresentationDataChangedCollection), "Invoke")]
    internal static class PresentationSnapshotPatch
    {
        private static readonly AccessTools.StructFieldRef<Collection, int> Cascades = AccessTools.StructFieldRefAccess<Collection, int>("numberOfCascades");
        private static readonly AccessTools.StructFieldRef<Collection, int[]> Cursors = AccessTools.StructFieldRefAccess<Collection, int[]>("invocationIndices");
        private static readonly AccessTools.StructFieldRef<Collection, int> Count = AccessTools.StructFieldRefAccess<Collection, int>("numberOfActions");
        private static readonly AccessTools.StructFieldRef<Collection, Action[]> Actions = AccessTools.StructFieldRefAccess<Collection, Action[]>("actions");

        private static readonly HandlerIsolation Isolation = new HandlerIsolation(10,
            static message => Plugin.Log("presentation handler threw (isolated, later handlers still run): " + message));

        private static bool Prefix(ref Collection __instance)
        {
            ref var cascades = ref Cascades(ref __instance);
            if (cascades >= 1) throw new InvalidOperationException();
            var slot = cascades;
            cascades++;
            var cursors = Cursors(ref __instance);
            cursors[slot] = 0;
            // Re-read count and array every pass: a handler can subscribe (resizing the array) or unsubscribe.
            while (cursors[slot] < Count(ref __instance))
            {
                Isolation.Run(Actions(ref __instance)[cursors[slot]]);
                cursors[slot]++;
            }

            cursors[slot] = 0;
            cascades--;
            return false;
        }
    }
}
