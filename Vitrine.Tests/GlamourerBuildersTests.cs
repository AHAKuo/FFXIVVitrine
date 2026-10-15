using Glamourer.Api.Enums;
using Newtonsoft.Json.Linq;
using Vitrine;
using Vitrine.Glamourer;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class GlamourerBuildersTests
{
    private static readonly Dictionary<string, (string[] Options, int GroupType)> Available = new()
    {
        ["Body options"] = (new[] { "Tre S chest", "Tre M chest", "Tre L chest" }, 0),
        ["Extras"]       = (new[] { "Tiger stripes", "Freckles" }, 1),
    };

    [Fact]
    public void Design_HasVersion3_AppliedPiecesOnly_AndTheModAssociation()
    {
        var mod = new VitrineModState
        {
            ModDirectory = "Cowkini",
            ModName      = "Cowkini",
            Enabled      = true,
            Priority     = 2,
            Settings     = new Dictionary<string, List<string>> { ["Body options"] = new() { "Tre M chest" }, ["Extras"] = new() { "Tiger stripes", "Freckles" } },
        };
        var items = new[] { new EquippableItem(1001, "Cow Top", ApiEquipSlot.Body), new EquippableItem(1002, "Cow Legs", ApiEquipSlot.Legs) };

        var design = GlamourerDesignBuilder.Build("Cowkini · Baseline", "from Vitrine", items, new Dictionary<string, int> { ["Hairstyle"] = 194 }, mod, Available);

        Assert.Equal(3, design["FileVersion"]!.Value<int>());
        Assert.True(Guid.TryParse(design["Identifier"]!.Value<string>(), out _));
        Assert.Equal("Cowkini · Baseline", design["Name"]!.Value<string>());
        Assert.Equal(JTokenType.Integer, design["CreationDate"]!.Type);

        var body = (JObject)design["Equipment"]!["Body"]!;
        Assert.Equal(1001UL, body["ItemId"]!.Value<ulong>());
        Assert.True(body["Apply"]!.Value<bool>());
        Assert.False(body["ApplyStain"]!.Value<bool>());
        Assert.Null(design["Equipment"]!["Head"]);

        Assert.Equal(194, design["Customize"]!["Hairstyle"]!["Value"]!.Value<int>());
        Assert.True(design["Customize"]!["Hairstyle"]!["Apply"]!.Value<bool>());
        Assert.Empty((JObject)design["Parameters"]!);

        var assoc = (JObject)design["Mods"]![0]!;
        Assert.Equal("Cowkini", assoc["Directory"]!.Value<string>());
        Assert.Equal("Enabled", assoc["State"]!.Value<string>());
        Assert.Equal(2, assoc["Priority"]!.Value<int>());
        var bodyGroup = assoc["Settings"]!.First(g => g["Name"]!.Value<string>() == "Body options");
        Assert.Equal(new[] { "Disabled", "Enabled", "Disabled" }, bodyGroup["Options"]!.Select(o => o["State"]!.Value<string>()));
        var extras = assoc["Settings"]!.First(g => g["Name"]!.Value<string>() == "Extras");
        Assert.All(extras["Options"]!, o => Assert.Equal("Enabled", o["State"]!.Value<string>()));
    }

    [Fact]
    public void Design_WithPenumbraDefaults_ListsNoSettings_AndDisabledModIsWritten()
    {
        var mod    = new VitrineModState { ModDirectory = "X", ModName = "X", Enabled = false, Settings = null };
        var design = GlamourerDesignBuilder.Build("X", "", Array.Empty<EquippableItem>(), null, mod, Available);
        var assoc  = (JObject)design["Mods"]![0]!;

        Assert.Null(assoc["Settings"]);
        Assert.Equal("Disabled", assoc["State"]!.Value<string>());
        Assert.Empty((JObject)design["Equipment"]!);
        Assert.Empty((JObject)design["Customize"]!);
    }

    [Fact]
    public void Design_SettingsNotInTheAvailableList_AreStillWritten()
    {
        var mod    = new VitrineModState { ModDirectory = "X", ModName = "X", Settings = new() { ["Gone group"] = new() { "Old option" } } };
        var design = GlamourerDesignBuilder.Build("X", "", Array.Empty<EquippableItem>(), null, mod, Available);
        var groups = design["Mods"]![0]!["Settings"]!.Select(g => g["Name"]!.Value<string>()).ToList();

        Assert.Contains("Gone group", groups);
        var gone = design["Mods"]![0]!["Settings"]!.First(g => g["Name"]!.Value<string>() == "Gone group");
        Assert.Equal("Enabled", gone["Options"]![0]!["State"]!.Value<string>());
    }

    [Fact]
    public void State_IsVersion1_AppliesOnlyGivenValues_AndCopiesIdentityUnapplied()
    {
        var current = new JObject
        {
            ["Customize"] = new JObject
            {
                ["ModelId"]   = 0,
                ["Race"]      = new JObject { ["Value"] = 6 },
                ["Clan"]      = new JObject { ["Value"] = 11 },
                ["Gender"]    = new JObject { ["Value"] = 1 },
                ["BodyType"]  = new JObject { ["Value"] = 1 },
                ["Hairstyle"] = new JObject { ["Value"] = 5 },
            },
        };

        var state = GlamourerStateBuilder.Customize(new Dictionary<string, int> { ["Hairstyle"] = 194, ["EyeColorRight"] = 65 }, current);

        Assert.Equal(1, state["FileVersion"]!.Value<int>());
        var customize = (JObject)state["Customize"]!;
        Assert.Equal(194, customize["Hairstyle"]!["Value"]!.Value<int>());
        Assert.True(customize["Hairstyle"]!["Apply"]!.Value<bool>());
        Assert.True(customize["EyeColorRight"]!["Apply"]!.Value<bool>());
        Assert.Equal(11, customize["Clan"]!["Value"]!.Value<int>());
        Assert.False(customize["Clan"]!["Apply"]!.Value<bool>());
        Assert.False(customize["Gender"]!["Apply"]!.Value<bool>());
        Assert.Null(customize["SkinColor"]);
        Assert.Empty((JObject)state["Equipment"]!);
    }

    [Fact]
    public void State_WithoutCurrentState_StillBuilds()
    {
        var state = GlamourerStateBuilder.Customize(new Dictionary<string, int> { ["Face"] = 2 }, null);

        Assert.Equal(2, state["Customize"]!["Face"]!["Value"]!.Value<int>());
        Assert.Null(state["Customize"]!["Race"]);
    }
}
