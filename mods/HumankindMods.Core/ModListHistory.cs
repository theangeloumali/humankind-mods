using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HumankindMods.Core
{
    [DataContract]
    internal sealed class ModListHistoryEntry
    {
        [DataMember(Name = "time", Order = 0)] public string Time = "";
        [DataMember(Name = "reason", Order = 1)] public string Reason = "";
        [DataMember(Name = "culprit", Order = 2)] public string? Culprit;
        [DataMember(Name = "old", Order = 3)] public string Old = "";
        [DataMember(Name = "new", Order = 4)] public string New = "";
    }

    /// <summary>
    /// Backup entries for the game's saved mod list (registry key RuntimeModule/RuntimeModuleConfiguration,
    /// module names joined with '/'). JSON lines, so the Mod Hub app can read them too.
    /// </summary>
    public static class ModListHistory
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(ModListHistoryEntry));

        public static string[] Split(string? list) => (list ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        public static string Join(IEnumerable<string> modules) => string.Join("/", modules);

        public static bool IsLoss(string? oldList, string? newList)
        {
            var kept = new HashSet<string>(Split(newList));
            return Split(oldList).Any(module => !kept.Contains(module));
        }

        public static string ToJsonLine(DateTime utc, string reason, string? culprit, string oldList, string newList)
        {
            var entry = new ModListHistoryEntry
            {
                Time = utc.ToString("o", CultureInfo.InvariantCulture),
                Reason = reason,
                Culprit = culprit,
                Old = oldList,
                New = newList,
            };
            using var stream = new MemoryStream();
            Serializer.WriteObject(stream, entry);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>The list as it was before the newest recorded loss, or null.</summary>
        public static string? FindNewestRestorable(IEnumerable<string> jsonLines)
        {
            string? newest = null;
            foreach (var line in jsonLines.Where(l => l.Length > 0))
            {
                newest = Read(line)?.Old ?? newest;
            }

            return newest;
        }

        private static ModListHistoryEntry? Read(string line)
        {
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(line));
                return Serializer.ReadObject(stream) as ModListHistoryEntry;
            }
            catch (SerializationException)
            {
                return null;
            }
        }
    }
}
