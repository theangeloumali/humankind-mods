using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Mercury.Presentation;
using HarmonyLib;
using HumankindMods.Core;

namespace CityRedraw.Diagnostics
{
    /// <summary>Reads every district on the map and the state of its main art channel (index 0, decompiled
    /// PresentationDistrict.mainLevelBuildComponantLayer), so a report can count what can't be drawn.</summary>
    internal static class DistrictCensusReader
    {
        private static readonly AccessTools.FieldRef<PresentationEntityLevelBuild, PresentationLevelBuildComponent> Component =
            AccessTools.FieldRefAccess<PresentationEntityLevelBuild, PresentationLevelBuildComponent>("presentationLevelBuildComponent");
        private static readonly AccessTools.FieldRef<PresentationDistrict, StaticString> Definition =
            AccessTools.FieldRefAccess<PresentationDistrict, StaticString>("constructibleDefinitionName");
        private static readonly AccessTools.FieldRef<PresentationDistrict, StaticString> Style =
            AccessTools.FieldRefAccess<PresentationDistrict, StaticString>("visualAffinityName");

        /// <summary>Valid district entries in the local empire's snapshot, split by whether a construction hides them
        /// (decompiled PresentationEntityFactoryController.SynchronizeDistrictInfo's release rule).</summary>
        public static (int Expected, int UnderConstruction) Expected()
        {
            // Null while a save loads; a throw here used to switch off the whole Update hook.
            var snapshot = Amplitude.Mercury.Interop.Snapshots.GameSnapshot;
            if (snapshot?.PresentationData == null) return (0, 0);
            var info = snapshot.PresentationData.LocalEmpireInfo.DistrictInfo;
            int expected = 0, underConstruction = 0;
            if (info.Data == null) return (0, 0);
            for (var i = 0; i < info.Length; i++)
            {
                ref var district = ref info.Data[i];
                if (district.PoolAllocationIndex < 0 || district.SimulationEntityGUID == 0L) continue;
                if (district.HasConstructionOver) underConstruction++;
                else expected++;
            }

            return (expected, underConstruction);
        }

        public static List<DistrictArt> Read() => ReadWithComponents().ConvertAll(entry => entry.Art);

        /// <summary>Each district and construction site with its component, for the camera tour.</summary>
        public static List<(DistrictArt Art, PresentationLevelBuildComponent? Component)> ReadWithComponents()
        {
            var districts = new List<(DistrictArt, PresentationLevelBuildComponent?)>();
            foreach (var district in UnityEngine.Object.FindObjectsOfType<PresentationDistrict>())
            {
                var component = Component(district);
                var channel = GameStateReader.MainChannel(component);
                districts.Add((new DistrictArt(Definition(district).ToString(), Style(district).ToString(), district.EmpireIndex,
                    channel != null && GameStateReader.BoxedHasMaterial(channel), channel != null && GameStateReader.BoxedAbleToSpawn(channel),
                    componentLoaded: component != null && component.Loaded), component));
            }

            foreach (var site in UnityEngine.Object.FindObjectsOfType<PresentationConstruction>())
            {
                var component = Component(site);
                var channel = GameStateReader.MainChannel(component);
                districts.Add((new DistrictArt(SiteDefinition(site).ToString(), SiteStyle(site).ToString(), -1,
                    channel != null && GameStateReader.BoxedHasMaterial(channel), channel != null && GameStateReader.BoxedAbleToSpawn(channel),
                    isConstructionSite: true, componentLoaded: component != null && component.Loaded), component));
            }

            return districts;
        }

        private static readonly AccessTools.FieldRef<PresentationConstruction, StaticString> SiteDefinition =
            AccessTools.FieldRefAccess<PresentationConstruction, StaticString>("constructibleDefinitionName");
        private static readonly AccessTools.FieldRef<PresentationConstruction, StaticString> SiteStyle =
            AccessTools.FieldRefAccess<PresentationConstruction, StaticString>("visualAffinityName");
    }
}
