using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HumankindMods.Core;

namespace CityRedraw.Diagnostics
{
    /// <summary>Writes the simulation's entity contracts once per session, after the simulation has built them.
    /// They come from the game's code, not mods, so the offline effect check can compile effects as the game does.</summary>
    internal static class EntityContractDump
    {
        private static readonly Type? ControllerType = AccessTools.TypeByName("Amplitude.Framework.Simulation.SimulationController");

        /// <summary>True once written; false while the simulation hasn't built its contracts yet.</summary>
        public static bool TryWrite(string path)
        {
            var repository = ControllerType == null ? null : AccessTools.Field(ControllerType, "EntityRepository")?.GetValue(null);
            if (repository == null) return false;
            var types = Traverse.Create(repository).Field("EntityTypeByContractIndex").GetValue<Type[]>();
            var contracts = Traverse.Create(repository).Field("Contracts").GetValue<Array>();
            if (types == null || contracts == null || contracts.Length == 0) return false;

            var infos = new List<EntityContractInfo>();
            for (var i = 0; i < contracts.Length; i++)
            {
                var contract = Traverse.Create(contracts.GetValue(i));
                var targets = contract.Field("ReferenceTargetByIndex").GetValue<Type[]>() ?? new Type[0];
                infos.Add(new EntityContractInfo(types[i].Name,
                    contract.Field("PropertyNameByIndex").GetValue<string[]>() ?? new string[0],
                    contract.Field("ReferenceNameByIndex").GetValue<string[]>() ?? new string[0],
                    Array.ConvertAll(targets, target => target.Name)));
            }

            File.WriteAllText(path, EntityContractJson.Format(infos));
            return true;
        }
    }
}
