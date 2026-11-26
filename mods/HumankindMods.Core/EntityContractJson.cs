using System.Collections.Generic;
using System.Text;

namespace HumankindMods.Core
{
    /// <summary>One simulation entity type as the game's effect compiler sees it: property names and reference
    /// names (lower case, as paths are matched) with the type each reference leads to.</summary>
    public readonly struct EntityContractInfo
    {
        public EntityContractInfo(string type, string[] properties, string[] references, string[] referenceTargets)
        {
            Type = type;
            Properties = properties;
            References = references;
            ReferenceTargets = referenceTargets;
        }

        public string Type { get; }
        public string[] Properties { get; }
        public string[] References { get; }
        public string[] ReferenceTargets { get; }
    }

    /// <summary>Writes the contracts as JSON for the offline effect check (mod-tests/tools/descriptor_check.py).</summary>
    public static class EntityContractJson
    {
        public static string Format(IEnumerable<EntityContractInfo> contracts)
        {
            var json = new StringBuilder("{");
            var firstType = true;
            foreach (var contract in contracts)
            {
                if (!firstType) json.Append(',');
                firstType = false;
                json.Append(Quote(contract.Type)).Append(":{\"properties\":[");
                for (var i = 0; i < contract.Properties.Length; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append(Quote(contract.Properties[i]));
                }

                json.Append("],\"references\":{");
                for (var i = 0; i < contract.References.Length; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append(Quote(contract.References[i])).Append(':').Append(Quote(contract.ReferenceTargets[i]));
                }

                json.Append("}}");
            }

            return json.Append('}').ToString();
        }

        private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
