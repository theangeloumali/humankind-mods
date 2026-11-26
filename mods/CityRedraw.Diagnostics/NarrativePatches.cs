using System.Reflection;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    // Names the narrative choice and status applied just before a simulation crash; the game's own
    // log shows only the stack. The types are internal, so they're reached by name.
    [HarmonyPatch]
    internal static class NarrativeChoicePatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method("Amplitude.Mercury.Simulation.NarrativeEventManager:MakeNarrativeEventChoice");

        private static void Prefix(object narrativeEvent, short choice)
        {
            Plugin.NarrativeHook.Run((narrativeEvent, choice), static s =>
            {
                var traverse = Traverse.Create(s.narrativeEvent);
                var definition = traverse.Field("NarrativeEventDescription").Field("NarrativeEventDefinition").GetValue();
                var empire = traverse.Field("MajorEmpire").Field("Index").GetValue();
                Plugin.Log($"narrative choice: empire {empire}, event {NameOf(definition)}, choice {s.choice}");
            });
        }

        internal static string NameOf(object? element) =>
            element == null ? "null" : (Traverse.Create(element).Property("Name").GetValue() ?? element).ToString();
    }

    [HarmonyPatch]
    internal static class ApplyStatusPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method("Amplitude.Mercury.Simulation.SimulationEventEffect_ApplyStatus_Base:Apply");

        private static void Prefix(object __instance)
        {
            Plugin.NarrativeHook.Run(__instance, static instance =>
            {
                var definition = Traverse.Create(instance).Property("SimulationEventEffectDefinition");
                var status = definition.Field("StatusDefinition").Field("serializableElementName").GetValue();
                Plugin.Log($"  applies status {status} to '{definition.Field("TargetID").GetValue()}'");
            });
        }
    }
}
