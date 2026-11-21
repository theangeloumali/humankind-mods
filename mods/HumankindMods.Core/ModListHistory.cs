using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HumankindMods.Core
{
    /// <summary>
    /// Backup entries for the game's saved mod list (registry key RuntimeModule/RuntimeModuleConfiguration,
    /// module names joined with '/'). JSON lines, so the Mod Hub app can read them too.
    /// </summary>
    public static class ModListHistory
    {
        private const string OldField = "\"old\":";

        public static bool IsLoss(string? oldList, string? newList)
        {
            var kept = new HashSet<string>(Split(newList));
            return Split(oldList).Any(module => !kept.Contains(module));
        }

        public static string ToJsonLine(DateTime utc, string reason, string? culprit, string oldList, string newList)
        {
            var json = new StringBuilder("{");
            json.Append("\"time\":").Append(Quote(utc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            json.Append("\"reason\":").Append(Quote(reason)).Append(',');
            json.Append("\"culprit\":").Append(culprit == null ? "null" : Quote(culprit)).Append(',');
            json.Append(OldField).Append(Quote(oldList)).Append(',');
            json.Append("\"new\":").Append(Quote(newList));
            return json.Append('}').ToString();
        }

        /// <summary>The list as it was before the newest recorded loss, or null.</summary>
        public static string? FindNewestRestorable(IEnumerable<string> jsonLines)
        {
            string? newest = null;
            foreach (var line in jsonLines)
            {
                newest = ReadOldField(line) ?? newest;
            }

            return newest;
        }

        private static IEnumerable<string> Split(string? list) =>
            (list ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        private static string Quote(string value)
        {
            var quoted = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else quoted.Append(c);
                        break;
                }
            }

            return quoted.Append('"').ToString();
        }

        // Reads only lines this class wrote; anything else returns null.
        private static string? ReadOldField(string line)
        {
            var start = line.IndexOf(OldField, StringComparison.Ordinal);
            if (start < 0 || start + OldField.Length >= line.Length || line[start + OldField.Length] != '"')
            {
                return null;
            }

            var value = new StringBuilder();
            for (var i = start + OldField.Length + 1; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"') return value.ToString();
                if (c != '\\' || i + 1 >= line.Length)
                {
                    value.Append(c);
                    continue;
                }

                var escaped = line[++i];
                switch (escaped)
                {
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u' when i + 4 < line.Length:
                        value.Append((char)int.Parse(line.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: value.Append(escaped); break;
                }
            }

            return null;
        }
    }
}
