using System;
using System.Reflection;
using System.Reflection.Emit;
using Amplitude.Mercury.Presentation;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    /// <summary>Reads the private per-channel state of PresentationLevelBuildComponent for the district census.
    /// Read-only and only on demand: hooking the spawn path itself stopped every building drawing (2026-11-26).</summary>
    internal static class GameStateReader
    {
        private static readonly Type ChannelType = AccessTools.Inner(typeof(PresentationLevelBuildComponent), "PerChannelData");
        private static readonly FieldInfo ChannelsField = AccessTools.Field(typeof(PresentationLevelBuildComponent), "channels");
        private static readonly MethodInfo AbleToSpawnGetter = AccessTools.PropertyGetter(ChannelType, "AbleToSpawn");
        private static readonly MethodInfo MaterialGuidGetter = AccessTools.PropertyGetter(ChannelType, "EvolverMaterialGuid");
        private static readonly MethodInfo GuidIsNullGetter = AccessTools.PropertyGetter(MaterialGuidGetter.ReturnType, "IsNull");

        public static readonly Func<object, bool> BoxedAbleToSpawn = EmitBoxedRead<bool>(il => il.Emit(OpCodes.Call, AbleToSpawnGetter));

        public static readonly Func<object, bool> BoxedHasMaterial = EmitBoxedRead<bool>(il =>
        {
            il.Emit(OpCodes.Call, MaterialGuidGetter);
            var guid = il.DeclareLocal(MaterialGuidGetter.ReturnType);
            il.Emit(OpCodes.Stloc, guid);
            il.Emit(OpCodes.Ldloca, guid);
            il.Emit(OpCodes.Call, GuidIsNullGetter);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
        });

        /// <summary>The boxed channel 0 of a component, or null before its channels exist.</summary>
        public static object? MainChannel(PresentationLevelBuildComponent? component) =>
            component == null ? null : (ChannelsField.GetValue(component) as Array) is { Length: > 0 } channels ? channels.GetValue(0) : null;

        /// <summary>Forces the static initialisers so a game update that renamed a member fails at load, not mid-game.</summary>
        public static void EnsureReady() => _ = BoxedHasMaterial;

        // Unboxes the struct in place (no copy) and applies one read to it.
        private static Func<object, T> EmitBoxedRead<T>(Action<ILGenerator> read)
        {
            var method = new DynamicMethod("CityRedraw_BoxedRead", typeof(T), new[] { typeof(object) }, typeof(PresentationLevelBuildComponent), skipVisibility: true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Unbox, ChannelType);
            read(il);
            il.Emit(OpCodes.Ret);
            return (Func<object, T>)method.CreateDelegate(typeof(Func<object, T>));
        }
    }
}
