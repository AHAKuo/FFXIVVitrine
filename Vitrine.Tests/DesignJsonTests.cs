using Newtonsoft.Json.Linq;
using Vitrine;
using Vitrine.Ipc;
using Xunit;

namespace Vitrine.Tests;

public class DesignJsonTests
{
    private static VitrineDesign Cowkini()
        => new()
        {
            Id       = Guid.Parse("a0f95146-309c-544e-9225-4702f2c86cb1"),
            Name     = "Cowkini",
            IsAuto   = true,
            Category = VitrineCategory.Gear,
            Mods =
            {
                new VitrineModState
                {
                    ModDirectory = "Cowkini",
                    ModName      = "Cowkini",
                    Enabled      = true,
                    Priority     = 0,
                    Settings     = new Dictionary<string, List<string>>
                    {
                        ["Cowkini Legs"] = new() { "Default Eve Legs" },
                        ["Extras"]       = new() { "Tiger stripes" },
                    },
                },
            },
        };

    [Fact]
    public void Serialize_MatchesTheDocumentedShape()
    {
        var json = JObject.Parse(DesignJson.Serialize(Cowkini()));

        Assert.Equal("a0f95146-309c-544e-9225-4702f2c86cb1", json["id"]!.Value<string>());
        Assert.Equal("Cowkini", json["name"]!.Value<string>());
        Assert.Equal("Gear", json["category"]!.Value<string>());
        Assert.True(json["isAuto"]!.Value<bool>());
        Assert.Equal(JTokenType.Null, json["baseDesignId"]!.Type);
        var mod = (JObject)json["mods"]![0]!;
        Assert.Equal("Cowkini", mod["modDirectory"]!.Value<string>());
        Assert.True(mod["enabled"]!.Value<bool>());
        Assert.Equal(0, mod["priority"]!.Value<int>());
        Assert.Equal("Default Eve Legs", mod["settings"]!["Cowkini Legs"]![0]!.Value<string>());
        Assert.Equal("Tiger stripes", mod["settings"]!["Extras"]![0]!.Value<string>());
    }

    [Fact]
    public void NullSettings_StayNull()
    {
        var design = Cowkini();
        design.Mods[0].Settings = null;
        var json = JObject.Parse(DesignJson.Serialize(design));
        Assert.Equal(JTokenType.Null, json["mods"]![0]!["settings"]!.Type);
    }

    [Fact]
    public void RoundTrip_ListAndSingle()
    {
        var list = DesignJson.DeserializeList(DesignJson.Serialize(new[] { Cowkini(), new VitrineDesign { Name = "Other", Category = VitrineCategory.Hair } }));
        Assert.Equal(2, list.Count);
        Assert.Equal(VitrineCategory.Hair, list[1].Category);

        var single = DesignJson.Deserialize(DesignJson.Serialize(Cowkini()));
        Assert.NotNull(single);
        Assert.Equal("Cowkini", single!.Mods[0].ModName);
        Assert.Equal(new[] { "Tiger stripes" }, single.Mods[0].Settings!["Extras"]);
    }

    [Fact]
    public void Malformed_IsTolerated()
    {
        Assert.Null(DesignJson.Deserialize("not json"));
        Assert.Empty(DesignJson.DeserializeList("{"));
    }
}
