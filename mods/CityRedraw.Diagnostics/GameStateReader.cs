using System;
using System.Reflection;
using System.Reflection.Emit;
using Amplitude.Mercury.Presentation;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    /// <summary>
    /// Reads the private per-channel state of PresentationLevelBuildComponent (decompiled :18-210) through
    /// IL compiled once: the hooks run tens of thousands of times per frame after a save load, and reflection
    /// boxing there would slow the very spawn bursts being measured.
    /// </summary>
    internal static class GameStateReader
    {
        private static readonly Type ChannelType = AccessTools.Inner(typeof(PresentationLevelBuildComponent), "PerChannelData");
        private static readonly FieldInfo ChannelsField = AccessTools.Field(typeof(PresentationLevelBuildComponent), "channels");
        private static readonly FieldInfo AddParticleMaskField = AccessTools.Field(typeof(PresentationLevelBuildComponent), "addParticleMask");
        private static readonly MethodInfo ShouldSpawnMethod = AccessTools.Method(ChannelType, "ShouldSpawn");
        private static readonly MethodInfo AbleToSpawnGetter = AccessTools.PropertyGetter(ChannelType, "AbleToSpawn");
        private static readonly MethodInfo MaterialGuidGetter = AccessTools.PropertyGetter(ChannelType, "EvolverMaterialGuid");
        private static readonly MethodInfo GuidIsNullGetter = AccessTools.PropertyGetter(MaterialGuidGetter.ReturnType, "IsNull");
        private static readonly FieldInfo ChannelIndexField = AccessTools.Field(ChannelType, "channelIndex");

        /// <summary>Bit i set = channel i is due in this AddParticle call.</summary>
        public static readonly Func<PresentationLevelBuildComponent, ulong> DueChannelMask = EmitDueChannelMask();

        public static readonly Func<PresentationLevelBuildComponent, int, bool> IsAbleToSpawn = EmitIsAbleToSpawn();

        public static readonly Func<object, int> BoxedChannelIndex = EmitBoxedRead<int>(il => il.Emit(OpCodes.Ldfld, ChannelIndexField));

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

        public static MethodBase RefreshChannelMethod() =>
            AccessTools.Method(typeof(PresentationLevelBuildComponent), "RefreshChannel", new[] { ChannelType.MakeByRefType() });

        /// <summary>Forces the static initialisers so a game update that renamed a member fails at load, not mid-game.</summary>
        public static void EnsureReady() => _ = DueChannelMask;

        private static DynamicMethod NewMethod(string name, Type returnType, params Type[] parameters) =>
            new DynamicMethod("CityRedraw_" + name, returnType, parameters, typeof(PresentationLevelBuildComponent), skipVisibility: true);

        private static Func<PresentationLevelBuildComponent, ulong> EmitDueChannelMask()
        {
            var method = NewMethod("DueChannelMask", typeof(ulong), typeof(PresentationLevelBuildComponent));
            var il = method.GetILGenerator();
            var channels = il.DeclareLocal(ChannelsField.FieldType);
            var mask = il.DeclareLocal(typeof(uint));
            var result = il.DeclareLocal(typeof(ulong));
            var index = il.DeclareLocal(typeof(int));
            var hasChannels = il.DefineLabel();
            var loop = il.DefineLabel();
            var next = il.DefineLabel();
            var condition = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, ChannelsField);
            il.Emit(OpCodes.Stloc, channels);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Brtrue, hasChannels);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_U8);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(hasChannels);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, AddParticleMaskField);
            il.Emit(OpCodes.Stloc, mask);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_U8);
            il.Emit(OpCodes.Stloc, result);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, index);
            il.Emit(OpCodes.Br, condition);

            il.MarkLabel(loop);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldelema, ChannelType);
            il.Emit(OpCodes.Ldloc, mask);
            il.Emit(OpCodes.Call, ShouldSpawnMethod);
            il.Emit(OpCodes.Brfalse, next);
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Conv_U8);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Shl);
            il.Emit(OpCodes.Or);
            il.Emit(OpCodes.Stloc, result);

            il.MarkLabel(next);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, index);

            il.MarkLabel(condition);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Blt, loop);
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret);
            return (Func<PresentationLevelBuildComponent, ulong>)method.CreateDelegate(typeof(Func<PresentationLevelBuildComponent, ulong>));
        }

        private static Func<PresentationLevelBuildComponent, int, bool> EmitIsAbleToSpawn()
        {
            var method = NewMethod("IsAbleToSpawn", typeof(bool), typeof(PresentationLevelBuildComponent), typeof(int));
            var il = method.GetILGenerator();
            var channels = il.DeclareLocal(ChannelsField.FieldType);
            var no = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, ChannelsField);
            il.Emit(OpCodes.Stloc, channels);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Brfalse, no);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Bge, no);
            il.Emit(OpCodes.Ldloc, channels);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldelema, ChannelType);
            il.Emit(OpCodes.Call, AbleToSpawnGetter);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(no);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            return (Func<PresentationLevelBuildComponent, int, bool>)method.CreateDelegate(typeof(Func<PresentationLevelBuildComponent, int, bool>));
        }

        // Unboxes the struct in place (no copy) and applies one read to it.
        private static Func<object, T> EmitBoxedRead<T>(Action<ILGenerator> read)
        {
            var method = NewMethod("BoxedRead", typeof(T), typeof(object));
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Unbox, ChannelType);
            read(il);
            il.Emit(OpCodes.Ret);
            return (Func<object, T>)method.CreateDelegate(typeof(Func<object, T>));
        }
    }
}
