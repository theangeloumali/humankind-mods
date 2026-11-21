using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Mercury.Presentation;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    /// <summary>Reads the private per-channel state of PresentationLevelBuildComponent (decompiled :18-210).</summary>
    internal static class GameStateReader
    {
        private static readonly Type ChannelType = AccessTools.Inner(typeof(PresentationLevelBuildComponent), "PerChannelData");
        private static readonly FieldInfo ChannelsField = AccessTools.Field(typeof(PresentationLevelBuildComponent), "channels");
        private static readonly FieldInfo AddParticleMaskField = AccessTools.Field(typeof(PresentationLevelBuildComponent), "addParticleMask");
        private static readonly FieldInfo ChannelIndexField = AccessTools.Field(ChannelType, "channelIndex");
        private static readonly PropertyInfo AbleToSpawnProperty = AccessTools.Property(ChannelType, "AbleToSpawn");
        private static readonly PropertyInfo MaterialGuidProperty = AccessTools.Property(ChannelType, "EvolverMaterialGuid");
        private static readonly PropertyInfo GuidIsNullProperty = AccessTools.Property(MaterialGuidProperty.PropertyType, "IsNull");
        private static readonly MethodInfo ShouldSpawnMethod = AccessTools.Method(ChannelType, "ShouldSpawn");

        public static MethodBase RefreshChannelMethod() =>
            AccessTools.Method(typeof(PresentationLevelBuildComponent), "RefreshChannel", new[] { ChannelType.MakeByRefType() });

        public static (int Index, bool HasMaterial, bool AbleToSpawn) ReadChannel(object boxedChannel)
        {
            var index = (int)(uint)ChannelIndexField.GetValue(boxedChannel);
            var guid = MaterialGuidProperty.GetValue(boxedChannel);
            var hasMaterial = !(bool)GuidIsNullProperty.GetValue(guid);
            return (index, hasMaterial, (bool)AbleToSpawnProperty.GetValue(boxedChannel));
        }

        /// <summary>The channels AddParticle is about to write, in the order it writes them.</summary>
        public static List<int> ChannelsToSpawn(PresentationLevelBuildComponent component)
        {
            var toSpawn = new List<int>();
            if (!(ChannelsField.GetValue(component) is Array channels)) return toSpawn;
            var mask = new object[] { AddParticleMaskField.GetValue(component) };
            for (var i = 0; i < channels.Length; i++)
            {
                if ((bool)ShouldSpawnMethod.Invoke(channels.GetValue(i), mask)) toSpawn.Add(i);
            }

            return toSpawn;
        }

        public static bool IsAbleToSpawn(PresentationLevelBuildComponent component, int channel) =>
            ChannelsField.GetValue(component) is Array channels
            && channel < channels.Length
            && (bool)AbleToSpawnProperty.GetValue(channels.GetValue(channel));
    }
}
