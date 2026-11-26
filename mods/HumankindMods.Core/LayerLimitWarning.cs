namespace HumankindMods.Core
{
    /// <summary>The on-screen warning as a session's FX layers near the renderer's limit (960: past it, buildings stop
    /// drawing until a reload); null while there is room.</summary>
    public static class LayerLimitWarning
    {
        public static string? Message(int layers, int warnAt, int limit, string reloadKey)
        {
            if (layers < warnAt) return null;
            var state = layers > limit ? "buildings are no longer drawing." : $"buildings stop drawing past {limit}.";
            return $"Graphics layers {layers}/{limit}: {state} Press {reloadKey} to quick-save and reload.";
        }
    }
}
