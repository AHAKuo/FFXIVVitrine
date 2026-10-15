using Vitrine;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class CustomizationParserTests
{
    [Theory]
    [InlineData("Customization: Midlander Female Hair 194", ModelRace.Midlander, 1, CustomizationKind.Hair, null, 194)]
    [InlineData("Customization: Au Ra Female Face (Iris) 1", ModelRace.AuRa, 1, CustomizationKind.Face, "Iris", 1)]
    [InlineData("Customization: Miqo'te Female Hair 108", ModelRace.Miqote, 1, CustomizationKind.Hair, null, 108)]
    [InlineData("Miqo'te Keeper of the Moon Female Hair 108", ModelRace.Miqote, 1, CustomizationKind.Hair, null, 108)]
    [InlineData("Hyur Midlander Female Hairstyle 5", ModelRace.Midlander, 1, CustomizationKind.Hair, null, 5)]
    [InlineData("Hyur Highlander Male Face 3", ModelRace.Highlander, 0, CustomizationKind.Face, null, 3)]
    [InlineData("Customization: Elezen Male Face 3", ModelRace.Elezen, 0, CustomizationKind.Face, null, 3)]
    [InlineData("Customization: Roegadyn Male Face (Etc) 2", ModelRace.Roegadyn, 0, CustomizationKind.Face, "Etc", 2)]
    [InlineData("Customization: Viera Female Ears 4", ModelRace.Viera, 1, CustomizationKind.Ears, null, 4)]
    [InlineData("Customization: Au Ra Male Tail 2", ModelRace.AuRa, 0, CustomizationKind.Tail, null, 2)]
    [InlineData("Customization: Viera Female Face (Ears) 2", ModelRace.Viera, 1, CustomizationKind.Ears, "Ears", 2)]
    [InlineData("Customization: Hrothgar Female Face (Tail) 1", ModelRace.Hrothgar, 1, CustomizationKind.Tail, "Tail", 1)]
    [InlineData("Customization: Lalafell Female Hair 12", ModelRace.Lalafell, 1, CustomizationKind.Hair, null, 12)]
    public void ParsesRaceGenderKindAndNumber(string name, ModelRace race, int gender, CustomizationKind kind, string? marker, int value)
    {
        Assert.True(CustomizationParser.TryParse(name, out var entry), name);
        Assert.Equal(race, entry.Race);
        Assert.Equal(gender, entry.Gender);
        Assert.Equal(kind, entry.Kind);
        Assert.Equal(marker, entry.Marker);
        Assert.Equal(value, entry.Value);
    }

    [Theory]
    [InlineData("Emote: Sit")]
    [InlineData("Midlander Female Hair")]
    [InlineData("Cowkini Top")]
    [InlineData("Customization: Female Hair 1")]
    [InlineData("")]
    public void NonEntries_AreRejected(string name)
        => Assert.False(CustomizationParser.TryParse(name, out _));

    [Fact]
    public void BodyEntry_NeedsNoNumber_AndHasNoGlamourerKey()
    {
        Assert.True(CustomizationParser.TryParse("Customization: Highlander Female Body", out var entry));
        Assert.Equal(CustomizationKind.Body, entry.Kind);
        Assert.Null(entry.GlamourerKey);
        Assert.Equal("Body", entry.ShortLabel);
    }

    [Fact]
    public void GlamourerKeys_FollowTheKind()
    {
        Assert.True(CustomizationParser.TryParse("Customization: Midlander Female Hair 194", out var hair));
        Assert.True(CustomizationParser.TryParse("Customization: Au Ra Female Face (Iris) 1", out var eyes));
        Assert.True(CustomizationParser.TryParse("Customization: Viera Female Ears 4", out var ears));

        Assert.Equal("Hairstyle", hair.GlamourerKey);
        Assert.Equal("Face", eyes.GlamourerKey);
        Assert.True(eyes.IsEyes);
        Assert.Equal("TailShape", ears.GlamourerKey);
        Assert.Equal("Eyes · Face 1", eyes.ShortLabel);
        Assert.Equal("Hair 194", hair.ShortLabel);
        Assert.Equal("Au Ra", eyes.RaceName);
    }

    [Fact]
    public void Matches_UsesTheModelRaceOfTheClan()
    {
        Assert.True(CustomizationParser.TryParse("Customization: Au Ra Female Face (Iris) 1", out var entry));
        Assert.True(entry.Matches(11, 1));  // Raen
        Assert.True(entry.Matches(12, 1));  // Xaela
        Assert.False(entry.Matches(11, 0)); // male
        Assert.False(entry.Matches(1, 1));  // Midlander

        Assert.True(CustomizationParser.TryParse("Customization: Highlander Female Hair 5", out var highlander));
        Assert.True(highlander.Matches(2, 1));
        Assert.False(highlander.Matches(1, 1)); // Midlander has its own models
        Assert.Equal("female", highlander.GenderName);
    }

    [Fact]
    public void ParseAll_KeepsOrder_AndSkipsOtherItems()
    {
        var items = new[]
        {
            new AffectedItem(VitrineCategory.Hair, "Customization: Midlander Female Hair 195", null),
            new AffectedItem(VitrineCategory.Gear, "Hat", null),
            new AffectedItem(VitrineCategory.Hair, "Customization: Midlander Female Hair 194", null),
        };

        Assert.Equal(new[] { 195, 194 }, CustomizationParser.ParseAll(items).Select(e => e.Value));
    }

    [Fact]
    public void ClanNames_AreKnown()
    {
        Assert.Equal("Keeper of the Moon", CustomizationParser.ClanName(8));
        Assert.Equal(ModelRace.Viera, CustomizationParser.RaceOfClan(16));
        Assert.Null(CustomizationParser.RaceOfClan(0));
    }
}
