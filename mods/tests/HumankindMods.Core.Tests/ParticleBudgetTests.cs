namespace HumankindMods.Core.Tests;

public class ParticleBudgetTests
{
    [Fact]
    public void A_layer_drawing_its_whole_allocation_counts_as_saturated()
    {
        var budget = new ParticleBudget();
        budget.RecordLayer("Buildings", rendered: 500, allocated: 500, requested: 600, upperBound: 0);
        budget.RecordLayer("Trees", rendered: 100, allocated: 400, requested: 400, upperBound: 0);

        Assert.Equal(1, budget.SaturatedLayers);
        Assert.Contains("Buildings 500/500 (asked 600, cap 0, full 1x)", budget.Summary(maxLayers: 5));
    }

    [Fact]
    public void A_layer_with_no_allocation_that_has_particles_is_saturated()
    {
        var budget = new ParticleBudget();
        budget.RecordLayer("Props", rendered: 3, allocated: 0, requested: 10, upperBound: 0);

        Assert.Equal(1, budget.SaturatedLayers);
    }

    [Fact]
    public void Keeps_each_layers_peak_and_reports_texture_use_and_cpu_drops()
    {
        var budget = new ParticleBudget();
        budget.RecordLayer("Buildings", 300, 500, 500, 0);
        budget.RecordLayer("Buildings", 450, 500, 500, 0);
        budget.RecordLayer("Buildings", 200, 500, 500, 0);
        budget.RecordTexture(usedPixels: 196608, totalPixels: 262144);
        budget.RecordCpuDrop();
        budget.RecordCpuDrop();

        var summary = budget.Summary(maxLayers: 5);

        Assert.Contains("texture 75% (196608/262144 px)", summary);
        Assert.Contains("cpu drops 2", summary);
        Assert.Contains("Buildings 450/500", summary);
    }

    [Fact]
    public void Reset_clears_the_peaks_so_each_turn_reports_its_own()
    {
        var budget = new ParticleBudget();
        budget.RecordLayer("Buildings", 500, 500, 500, 0);
        budget.RecordCpuDrop();

        budget.Reset();

        Assert.False(budget.HasData);
        Assert.Equal(0, budget.SaturatedLayers);
    }

    [Fact]
    public void Lists_saturated_layers_first_and_caps_the_list()
    {
        var budget = new ParticleBudget();
        budget.RecordLayer("Calm", 10, 100, 100, 0);
        budget.RecordLayer("Full", 100, 100, 120, 0);
        budget.RecordLayer("Busy", 90, 100, 100, 0);

        var summary = budget.Summary(maxLayers: 2);

        Assert.True(summary.IndexOf("Full", System.StringComparison.Ordinal) < summary.IndexOf("Busy", System.StringComparison.Ordinal));
        Assert.DoesNotContain("Calm", summary);
    }

    [Fact]
    public void Reports_the_renderers_layer_count_against_what_its_buffer_fits()
    {
        var budget = new ParticleBudget();
        budget.RecordLayerCount(layers: 975, layersThatFit: 960);

        Assert.Contains("renderer layers 975/960 OVER", budget.Summary(maxLayers: 5));
    }

    [Fact]
    public void A_layer_count_within_the_buffer_is_not_flagged()
    {
        var budget = new ParticleBudget();
        budget.RecordLayerCount(layers: 788, layersThatFit: 960);

        Assert.Contains("renderer layers 788/960;", budget.Summary(maxLayers: 5));
    }

    [Fact]
    public void Lists_the_layers_added_since_the_last_summary_with_a_cap()
    {
        var budget = new ParticleBudget();
        budget.RecordLayerAdded("BUI_Era6_CultureEastern_CitySet_01OutputLayer");
        budget.RecordLayerAdded("EQ_Era6_Weapons_OutputLayer");
        budget.RecordLayerAdded("SchematicView_On_Ocean_OutputLayer");

        var summary = budget.Summary(maxLayers: 5, maxAdded: 2);

        Assert.Contains("added 3: BUI_Era6_CultureEastern_CitySet_01OutputLayer, EQ_Era6_Weapons_OutputLayer, +1 more", summary);
    }

    [Fact]
    public void Reset_forgets_the_added_layers()
    {
        var budget = new ParticleBudget();
        budget.RecordLayerAdded("EQ_Era6_Weapons_OutputLayer");

        budget.Reset();

        Assert.Contains("added 0", budget.Summary(maxLayers: 5, maxAdded: 2));
    }

    [Fact]
    public void Reports_the_peak_particle_pool_fill()
    {
        var budget = new ParticleBudget();
        budget.RecordParticlePool(used: 4_000_000, capacity: 16_000_000);
        budget.RecordParticlePool(used: 12_000_000, capacity: 16_000_000);
        budget.RecordParticlePool(used: 5_000_000, capacity: 16_000_000);

        Assert.Contains("pool 75% (12000000/16000000)", budget.Summary(maxLayers: 5));
    }
}
