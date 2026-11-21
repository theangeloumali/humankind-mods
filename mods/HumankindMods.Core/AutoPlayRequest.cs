using System.Globalization;

namespace HumankindMods.Core
{
    /// <summary>The autoplay trigger file holds the target turn, as "350" or "target=350".</summary>
    public static class AutoPlayRequest
    {
        public static int? ParseTargetTurn(string text)
        {
            var value = text.Trim();
            var equals = value.IndexOf('=');
            if (equals >= 0) value = value.Substring(equals + 1).Trim();
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn) && turn > 0
                ? turn
                : (int?)null;
        }
    }
}
