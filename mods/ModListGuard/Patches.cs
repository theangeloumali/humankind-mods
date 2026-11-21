using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Amplitude.Framework.Runtime;
using Amplitude.Mercury.Runtime;
using HarmonyLib;
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
            configuration = Plugin.RepairHook.Run((__instance, configuration), static s => Plugin.RepairIfSaved(s.__instance, s.configuration), fallback: null)
                ?? configuration;
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
            Plugin.SaveHook.Run(configuration, static c => Plugin.RecordIfLoss(Plugin.Join(c), Callers));
        }

        // The game methods on the stack name the path that saved (menu, mod.io, error reset).
        private static string Callers() =>
            string.Join(" < ", new StackTrace().GetFrames()
                .Select(Harmony.GetMethodFromStackframe)
                .Where(m => m?.DeclaringType != null && m.DeclaringType.Assembly != typeof(Plugin).Assembly && m.DeclaringType.Namespace != "HumankindMods.Core")
                .Select(m => m!.DeclaringType!.Name + "." + m.Name)
                .Distinct()
                .Take(3));
    }

    [HarmonyPatch(typeof(MercuryRuntimeManager), "RuntimeExceptionRaised")]
    internal static class RuntimeExceptionRaisedPatch
    {
        private static void Prefix(RuntimeExceptionEventArgs e)
        {
            Plugin.CulpritHook.Run(e.Exception, static exception => Plugin.NoteCulprit(exception));
        }
    }

    [HarmonyPatch(typeof(RuntimeManagerHelper), nameof(RuntimeManagerHelper.HandleCrash))]
    internal static class HandleCrashPatch
    {
        private static void Prefix(Exception exception)
        {
            Plugin.CulpritHook.Run(exception, static e => Plugin.NoteCulprit(e));
        }
    }
}
