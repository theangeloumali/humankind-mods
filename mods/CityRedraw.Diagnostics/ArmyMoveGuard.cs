using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CityRedraw.Diagnostics
{
    // ArmyGoToAction.CanBeFurthered divides each step's cost by the army's speed for that terrain; an army whose
    // speed there is zero throws DivideByZeroException at turn end and the turn never ends (soak e, turn 111).
    // Answers "can't move further this turn", the method's own safe result, and logs the army's units once.
    [HarmonyPatch]
    internal static class ArmyMoveGuard
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("Amplitude.Mercury.Simulation.ArmyGoToAction"), "CanBeFurthered");

        private static Exception? Finalizer(Exception? __exception, object __instance, ref bool __result)
        {
            if (!(__exception is DivideByZeroException)) return __exception;
            __result = false;
            var army = Traverse.Create(__instance).Field("Entity").GetValue();
            var description = Describe(army);
            if (Plugin.ZeroSpeedArmies.ShouldReport(description)) Plugin.Log("army can't move (a speed is zero): " + description);
            return null;
        }

        private static string Describe(object? army)
        {
            if (army == null) return "unknown army";
            var units = new List<string>();
            var collection = Traverse.Create(army).Field("Units");
            var count = collection.Property("Count").GetValue<int>();
            for (var i = 0; i < count; i++)
            {
                var unit = collection.Method("get_Item", i).GetValue();
                var unitTraverse = Traverse.Create(unit);
                units.Add($"{unitTraverse.Field("UnitDefinition").Property("Name").GetValue()} (land {Speed(unitTraverse, "LandSpeed")}, naval {Speed(unitTraverse, "NavalSpeed")})");
            }

            var armyTraverse = Traverse.Create(army);
            return $"land {Speed(armyTraverse, "LandSpeed")}, naval {Speed(armyTraverse, "NavalSpeed")}: {string.Join(", ", units)}";
        }

        private static string Speed(Traverse entity, string property) => entity.Field(property).Property("Value").GetValue()?.ToString() ?? "?";
    }
}
