namespace HumankindMods.Core.Tests;

public class DistrictCensusTests
{
    [Fact]
    public void Counts_each_state_and_lists_failures_by_district_and_style()
    {
        var districts = new[]
        {
            new DistrictArt("Extension_Base_Food", "BuildingVisualAffinity_Era1_EgyptianKingdom", 0, true, true),
            new DistrictArt("District_CityCenter", "BuildingVisualAffinity_Era1_Colchis", 3, true, false),
            new DistrictArt("District_CityCenter", "BuildingVisualAffinity_Era1_Colchis", 3, true, false),
            new DistrictArt("Extension_Base_Housing", "BuildingVisualAffinity_Era1_Colchis", 3, false, false),
        };

        var text = DistrictCensus.Format(districts, expected: 4, underConstruction: 0);

        Assert.Contains("districts 4: ready 1, art found but not loaded 2, no art 1, component not loaded 0", text);
        Assert.Contains("2 x not loaded: District_CityCenter / BuildingVisualAffinity_Era1_Colchis (empires 3)", text);
        Assert.Contains("1 x no art: Extension_Base_Housing / BuildingVisualAffinity_Era1_Colchis (empires 3)", text);
        Assert.DoesNotContain("Extension_Base_Food", text);
    }

    [Fact]
    public void Art_ready_on_a_component_that_never_loaded_is_counted_as_not_drawing()
    {
        var districts = new[]
        {
            new DistrictArt("District_CityCenter", "BuildingVisualAffinity_Era1_Colchis", 2, true, true, componentLoaded: false),
            new DistrictArt("Extension_Base_Food", "BuildingVisualAffinity_Era1_Colchis", 2, true, true),
        };

        var text = DistrictCensus.Format(districts, expected: 2, underConstruction: 0);

        Assert.Contains("districts 2: ready 1, art found but not loaded 0, no art 0, component not loaded 1", text);
        Assert.Contains("1 x component not loaded: District_CityCenter / BuildingVisualAffinity_Era1_Colchis (empires 2)", text);
    }

    [Fact]
    public void Reports_an_empty_map()
    {
        Assert.Contains("districts 0", DistrictCensus.Format(Array.Empty<DistrictArt>(), expected: 0, underConstruction: 0));
    }

    [Fact]
    public void Flags_districts_the_simulation_has_but_the_map_never_built()
    {
        var districts = new[]
        {
            new DistrictArt("District_CityCenter", "BuildingVisualAffinity_Era1_Assyria", 0, true, true),
            new DistrictArt("Extension_Base_Food", "BuildingVisualAffinity_Era1_Assyria", 0, true, true, isConstructionSite: true),
        };

        var text = DistrictCensus.Format(districts, expected: 3, underConstruction: 1);

        Assert.Contains("districts 1: ready 1", text);
        Assert.Contains("construction sites 1: ready 1", text);
        Assert.Contains("simulation expects 3 (1 more under construction): 2 never built", text);
    }
}
