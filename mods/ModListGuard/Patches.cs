using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Amplitude.Framework.Runtime;
using Amplitude.Mercury.Runtime;
using HarmonyLib;
using HumankindMods.Core;
using FrameworkRuntimeManager = Amplitude.Framework.Runtime.RuntimeManager;
using MercuryRuntimeManager = Amplitude.Mercury.Runtime.RuntimeManager;

namespace ModListGuard
{
    // Root fix: repair the saved list before the game's check rejects it and wipes everything.
    [HarmonyPatch(typeof(FrameworkRuntimeManager), "CheckRuntimeConfiguration")]
    internal static class CheckRuntimeConfigurationPatch
    {
        private static void Prefix(FrameworkRuntimeManager __instance, ref RuntimeModuleConfiguration[] configuration)
        {
            var current = configuration;
            RuntimeModuleConfiguration[]? repaired = null;
            Plugin.Guard.Run("CheckRuntimeConfiguration.Prefix", () => repaired = Plugin.RepairIfSaved(__instance, current));
            if (repaired != null) configuration = repaired;
        }
    }

    // Backup: every save that loses a module is recorded with its reason before it happens.
    [HarmonyPatch]
    internal static class SaveConfigurationPatch
    {
        private static MethodBase TargetMethod()
        {
            var map = typeof(MercuryRuntimeManager).GetInterfaceMap(typeof(IRuntimeConfigurationService));
            return map.TargetMethods[Array.FindIndex(map.InterfaceMethods, m => m.Name == nameof(IRuntimeConfigurationService.SaveConfiguration))];
        }

        private static void Prefix(RuntimeModuleConfiguration[] configuration)
        {
            Plugin.Guard.Run("SaveConfiguration.Prefix", () => Plugin.RecordIfLoss(Plugin.Join(configuration), Callers));
        }

        // The game methods on the stack name the path that saved (menu, mod.io, error reset).
        private static string Callers() =>
            string.Join(" < ", new StackTrace().GetFrames()
                .Select(f => f.GetMethod())
                .Where(m => m?.DeclaringType != null && !IsOurs(m.DeclaringType.Assembly))
                .Select(m => GameMethodName(m!))
                .Where(name => !name.StartsWith("HarmonyLib", StringComparison.Ordinal))
                .Distinct()
                .Take(3));

        private static bool IsOurs(Assembly assembly) =>
            assembly == typeof(Plugin).Assembly || assembly == typeof(HookGuard).Assembly;

        // Patched methods run as "DMD<Type::Method>" wrappers; keep only the method's own name.
        private static string GameMethodName(MethodBase method)
        {
            var name = method.Name;
            var start = name.LastIndexOf("::", StringComparison.Ordinal);
            if (!name.StartsWith("DMD<", StringComparison.Ordinal) || start < 0) return method.DeclaringType!.Name + "." + name;
            var inner = name.Substring(start + 2).TrimEnd('>');
            return inner.Substring(inner.LastIndexOf('.') + 1);
        }
    }

    [HarmonyPatch(typeof(MercuryRuntimeManager), "RuntimeExceptionRaised")]
    internal static class RuntimeExceptionRaisedPatch
    {
        private static void Prefix(RuntimeExceptionEventArgs e)
        {
            Plugin.Guard.Run("RuntimeExceptionRaised.Prefix", () => Plugin.NoteCulprit(e.Exception));
        }
    }

    [HarmonyPatch(typeof(RuntimeManagerHelper), nameof(RuntimeManagerHelper.HandleCrash))]
    internal static class HandleCrashPatch
    {
        private static void Prefix(Exception exception)
        {
            Plugin.Guard.Run("HandleCrash.Prefix", () => Plugin.NoteCulprit(exception));
        }
    }
}
