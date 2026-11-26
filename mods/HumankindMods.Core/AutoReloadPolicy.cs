namespace HumankindMods.Core
{
    /// <summary>When to reload a session to reset the FX layer list: at the threshold, and only if the session grew
    /// past what a fresh load of it needs (else a reload would loop without freeing anything).</summary>
    public static class AutoReloadPolicy
    {
        public static bool ShouldReload(int layers, int threshold, int layersAfterLoad, int minGrowth) =>
            threshold > 0 && layers >= threshold && layers - layersAfterLoad >= minGrowth;
    }
}
