namespace HumankindMods.Core.Tests;

public class EntityContractJsonTests
{
    [Fact]
    public void Writes_each_type_with_its_properties_and_references()
    {
        var json = EntityContractJson.Format(new[]
        {
            new EntityContractInfo("District", new[] { "foodproduced", "cultureproduced" }, new[] { "settlement", "neighbours" }, new[] { "Settlement", "District" }),
            new EntityContractInfo("Settlement", new[] { "experiencegainonunitcreation" }, new[] { "empire" }, new[] { "MajorEmpire" }),
        });

        Assert.Equal(
            "{\"District\":{\"properties\":[\"foodproduced\",\"cultureproduced\"],\"references\":{\"settlement\":\"Settlement\",\"neighbours\":\"District\"}}," +
            "\"Settlement\":{\"properties\":[\"experiencegainonunitcreation\"],\"references\":{\"empire\":\"MajorEmpire\"}}}",
            json);
    }

    [Fact]
    public void Escapes_quotes_and_backslashes()
    {
        var json = EntityContractJson.Format(new[] { new EntityContractInfo("A\"B", new[] { "x\\y" }, new string[0], new string[0]) });

        Assert.Equal("{\"A\\\"B\":{\"properties\":[\"x\\\\y\"],\"references\":{}}}", json);
    }
}
