using System.Reflection;
using Amplitude;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    // The game logs a sandbox state's exception by message only, so a turn that never begins or ends gives no
    // stack (soak e, turn 88). Read-only: writes the full exception before the game's own (internal) handler runs.
    [HarmonyPatch]
    internal static class SandboxExceptionProbe
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("Amplitude.Mercury.Sandbox.SandboxState"), "ExceptionHandler");

        private static void Prefix(object __instance, CoroutineExceptionEventArgs e)
        {
            if (e?.Exception != null) Plugin.Log("sandbox state " + __instance.GetType().Name + " threw: " + e.Exception);
        }
    }
}
