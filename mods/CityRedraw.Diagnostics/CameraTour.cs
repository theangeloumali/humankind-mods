using System.Collections.Generic;
using System.Linq;
using Amplitude.Mercury.Presentation;
using HumankindMods.Core;
using UnityEngine;

namespace CityRedraw.Diagnostics
{
    /// <summary>Each press focuses the camera on the next construction site, the first district of each city, or a
    /// district that can't draw, and logs what is there, so a screenshot can be matched to the census.</summary>
    internal sealed class CameraTour
    {
        private List<(DistrictArt Art, PresentationLevelBuildComponent Component)> stops = new List<(DistrictArt, PresentationLevelBuildComponent)>();
        private int next;

        public void Advance()
        {
            if (next >= stops.Count)
            {
                stops = PickStops(DistrictCensusReader.ReadWithComponents());
                next = 0;
                Plugin.Log($"camera tour: {stops.Count} stops");
                if (stops.Count == 0) return;
            }

            var (art, component) = stops[next];
            var tile = component.WorldPosition;
            // Left third of the screen: turn popups and notifications cover the centre and right.
            Presentation.PresentationCameraController.FocusCameraAt(tile, new Vector2(0.25f, 0.45f), 0.05f);
            Plugin.Log($"camera tour {next + 1}/{stops.Count}: {(art.IsConstructionSite ? "site" : "district")} {art.Definition} / {art.Style} " +
                       $"empire {art.Empire} tile {tile.Column},{tile.Row} art {art.HasArt} material {art.Loaded} component {art.ComponentLoaded}");
            next++;
        }

        private static List<(DistrictArt, PresentationLevelBuildComponent)> PickStops(List<(DistrictArt Art, PresentationLevelBuildComponent? Component)> entries)
        {
            var seenStyles = new HashSet<(int, string)>();
            var stops = new List<(DistrictArt, PresentationLevelBuildComponent)>();
            foreach (var (art, component) in entries.OrderBy(e => e.Art.IsConstructionSite ? 0 : 1))
            {
                if (component == null) continue;
                var broken = !(art.HasArt && art.Loaded && art.ComponentLoaded);
                if (art.IsConstructionSite || broken || seenStyles.Add((art.Empire, art.Style))) stops.Add((art, component));
            }

            return stops;
        }
    }
}
