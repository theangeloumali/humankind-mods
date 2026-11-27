namespace HumankindMods.Core
{
    /// <summary>The GPU particle pool size to create: the configured size when larger, never smaller (0 = the game's).</summary>
    public static class ParticlePoolSizing
    {
        public static int Choose(int current, int configured) => configured > current ? configured : current;
    }
}
