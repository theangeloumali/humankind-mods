using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Amplitude.Mercury.Data.Simulation;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    // At an era change the independents upgrade their army patterns rung by rung and read each rung's
    // EraOfUnlock.EraIndex unchecked; only a technology sets EraOfUnlock, so a mod's hidden placeholder on a
    // ladder (ENCReload's Citizens, Fake ships) throws and the turn never begins (soak e, turn 88). Such a rung
    // now reads as not yet available, which ends the walk there, as the game does for a rung too new.
    [HarmonyPatch]
    internal static class MinorFactionUpgradeGuard
    {
        private static readonly FieldInfo EraOfUnlock = AccessTools.Field(typeof(ConstructibleDefinition), nameof(ConstructibleDefinition.EraOfUnlock));
        private static readonly FieldInfo EraIndex = AccessTools.Field(typeof(EraDefinition), nameof(EraDefinition.EraIndex));

        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("Amplitude.Mercury.Simulation.BaseHumanMinorFactionUtils"), "GetUpgradedUnitDefinition");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var replaced = 0;
            for (var i = 0; i < code.Count - 1; i++)
            {
                if (!code[i].LoadsField(EraOfUnlock) || !code[i + 1].LoadsField(EraIndex)) continue;
                code[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MinorFactionUpgradeGuard), nameof(EraIndexOrUnavailable))).MoveLabelsFrom(code[i]);
                code.RemoveAt(i + 1);
                replaced++;
            }

            if (replaced != 1) Plugin.Log($"minor faction upgrade guard: expected 1 unchecked era read, found {replaced}");
            return code;
        }

        private static int EraIndexOrUnavailable(ConstructibleDefinition unit)
        {
            if (unit.EraOfUnlock != null) return unit.EraOfUnlock.EraIndex;
            if (Plugin.UpgradeStops.ShouldReport(unit.Name.ToString())) Plugin.Log("minor faction upgrade stops at " + unit.Name + ": no technology unlocks it");
            return int.MaxValue;
        }
    }
}
