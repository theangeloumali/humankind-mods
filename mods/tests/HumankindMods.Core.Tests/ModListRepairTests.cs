
namespace HumankindMods.Core.Tests;

public class ModListRepairTests
{
    private const string Base = "project mercury";
    private const string NukesV05 = "long range nuclear missiles.2ff4c6472f744f1cb55a5e86d7306753.0.5";
    private const string DeepSeaV19 = "deepsea reloaded.a76c474eb14d46cab94bde2e802b5ba8.1.9";
    private const string DeepSeaV33 = "deepsea reloaded.a76c474eb14d46cab94bde2e802b5ba8.3.3";
    private const string DeepSeaV210 = "deepsea reloaded.a76c474eb14d46cab94bde2e802b5ba8.2.10";

    [Fact]
    public void Installed_modules_are_kept_in_order()
    {
        var result = ModListRepair.Repair(new[] { Base, NukesV05 }, new[] { NukesV05, Base });

        Assert.False(result.Changed);
        Assert.Equal(new[] { Base, NukesV05 }, result.Modules);
    }

    [Fact]
    public void An_outdated_version_is_replaced_by_the_installed_one_with_the_same_guid()
    {
        var result = ModListRepair.Repair(new[] { Base, NukesV05, DeepSeaV19 }, new[] { Base, NukesV05, DeepSeaV33 });

        Assert.True(result.Changed);
        Assert.Equal(new[] { Base, NukesV05, DeepSeaV33 }, result.Modules);
        Assert.Equal(new[] { DeepSeaV19 + " -> " + DeepSeaV33 }, result.Replaced);
        Assert.Empty(result.Dropped);
    }

    [Fact]
    public void A_version_too_big_for_an_int_still_sorts_instead_of_throwing()
    {
        const string Huge = "deepsea reloaded.a76c474eb14d46cab94bde2e802b5ba8.99999999999.0";
        var result = ModListRepair.Repair(new[] { Base, DeepSeaV19 }, new[] { Base, DeepSeaV33, Huge });

        Assert.Equal(Huge, result.Modules[1]);
    }

    [Fact]
    public void The_highest_installed_version_wins_numerically()
    {
        var result = ModListRepair.Repair(new[] { Base, DeepSeaV19 }, new[] { Base, DeepSeaV210, DeepSeaV33 });

        Assert.Equal(DeepSeaV33, result.Modules[1]);
    }

    [Fact]
    public void A_module_with_no_installed_match_is_dropped_and_the_rest_survive()
    {
        var result = ModListRepair.Repair(new[] { Base, NukesV05, DeepSeaV19 }, new[] { Base, NukesV05 });

        Assert.Equal(new[] { Base, NukesV05 }, result.Modules);
        Assert.Equal(new[] { DeepSeaV19 }, result.Dropped);
    }

    [Fact]
    public void Matching_ignores_case()
    {
        var result = ModListRepair.Repair(new[] { Base, DeepSeaV19 }, new[] { Base, "DeepSea Reloaded.A76C474EB14D46CAB94BDE2E802B5BA8.3.3" });

        Assert.Equal(new[] { Base, "DeepSea Reloaded.A76C474EB14D46CAB94BDE2E802B5BA8.3.3" }, result.Modules);
    }

    [Fact]
    public void A_replacement_already_in_the_list_is_not_duplicated()
    {
        var result = ModListRepair.Repair(new[] { Base, DeepSeaV19, DeepSeaV33 }, new[] { Base, DeepSeaV33 });

        Assert.Equal(new[] { Base, DeepSeaV33 }, result.Modules);
    }

    [Fact]
    public void Names_without_a_guid_are_dropped_when_not_installed()
    {
        var result = ModListRepair.Repair(new[] { Base, "prussia" }, new[] { Base });

        Assert.Equal(new[] { "prussia" }, result.Dropped);
    }

    [Fact]
    public void A_versioned_name_resolves_to_the_installed_short_name()
    {
        var result = ModListRepair.Repair(new[] { Base, DeepSeaV19 }, new[] { Base, "deepsea reloaded", "long range nuclear missiles" });

        Assert.Equal(new[] { Base, "deepsea reloaded" }, result.Modules);
        Assert.Equal(new[] { DeepSeaV19 + " -> deepsea reloaded" }, result.Replaced);
    }

    [Fact]
    public void Short_names_that_are_installed_are_kept_and_a_missing_one_is_dropped()
    {
        var result = ModListRepair.Repair(
            new[] { Base, "long range nuclear missiles", "zk removed mod", "deepsea reloaded" },
            new[] { Base, "deepsea reloaded", "long range nuclear missiles" });

        Assert.Equal(new[] { Base, "long range nuclear missiles", "deepsea reloaded" }, result.Modules);
        Assert.Equal(new[] { "zk removed mod" }, result.Dropped);
    }
}
