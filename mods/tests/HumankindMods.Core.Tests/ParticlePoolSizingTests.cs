namespace HumankindMods.Core.Tests;

public class ParticlePoolSizingTests
{
    [Fact]
    public void Raises_the_pool_to_the_configured_size()
    {
        Assert.Equal(33_554_432, ParticlePoolSizing.Choose(current: 16_777_216, configured: 33_554_432));
    }

    [Fact]
    public void Never_shrinks_a_larger_pool()
    {
        Assert.Equal(67_108_864, ParticlePoolSizing.Choose(current: 67_108_864, configured: 33_554_432));
    }

    [Fact]
    public void Zero_keeps_the_games_size()
    {
        Assert.Equal(16_777_216, ParticlePoolSizing.Choose(current: 16_777_216, configured: 0));
    }
}
