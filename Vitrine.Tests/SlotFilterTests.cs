using Glamourer.Api.Enums;
using Vitrine;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class SlotFilterTests
{
    private sealed class FakeResolver : IItemResolver
    {
        private readonly Dictionary<string, EquippableItem> _items = new(StringComparer.OrdinalIgnoreCase);
        private uint _next = 1000;

        public FakeResolver Add(string name, ApiEquipSlot slot)
        {
            _items[name] = new EquippableItem(_next++, name, slot);
            return this;
        }

        public bool TryResolve(string name, out EquippableItem item)
            => _items.TryGetValue(name, out item);
    }

    private static readonly FakeResolver Resolver = new FakeResolver()
        .Add("Gloves", ApiEquipSlot.Hands)
        .Add("Coat", ApiEquipSlot.Body)
        .Add("Ring", ApiEquipSlot.RFinger)
        .Add("Sword", ApiEquipSlot.MainHand);

    [Fact]
    public void SlotsOf_collects_every_resolved_gear_slot()
    {
        var affected = new[]
        {
            new AffectedItem(VitrineCategory.Gear, "Gloves", null),
            new AffectedItem(VitrineCategory.Gear, "Coat", null),
            new AffectedItem(VitrineCategory.Accessory, "Ring", null),
        };

        Assert.Equal(EquipSlotFlags.Hands | EquipSlotFlags.Body | EquipSlotFlags.Fingers, SlotFilter.SlotsOf(affected, Resolver));
    }

    [Fact]
    public void SlotsOf_ignores_unresolved_and_non_gear_entries()
    {
        var affected = new[]
        {
            new AffectedItem(VitrineCategory.Hair, "Sword", null),
            new AffectedItem(VitrineCategory.Gear, "Unknown thing", null),
        };

        Assert.Equal(EquipSlotFlags.None, SlotFilter.SlotsOf(affected, Resolver));
    }

    [Fact]
    public void Matches_is_any_of_the_ticked_slots_and_empty_matches_all()
    {
        var gloves = new[] { new AffectedItem(VitrineCategory.Gear, "Gloves", null) };
        var emote  = new[] { new AffectedItem(VitrineCategory.Emote, "Hum", null) };

        Assert.True(SlotFilter.Matches(EquipSlotFlags.None, emote, Resolver));
        Assert.True(SlotFilter.Matches(EquipSlotFlags.Hands | EquipSlotFlags.Feet, gloves, Resolver));
        Assert.False(SlotFilter.Matches(EquipSlotFlags.Feet, gloves, Resolver));
        Assert.False(SlotFilter.Matches(EquipSlotFlags.Hands, emote, Resolver));
    }

    [Fact]
    public void Summary_and_labels()
    {
        Assert.Equal("Any", SlotFilter.Summary(EquipSlotFlags.None));
        Assert.Equal("Hands", SlotFilter.Summary(EquipSlotFlags.Hands));
        Assert.Equal("2 slots", SlotFilter.Summary(EquipSlotFlags.Hands | EquipSlotFlags.Body));
        Assert.Equal(SlotFilter.All.Count, Enum.GetValues<EquipSlotFlags>().Count(f => f != EquipSlotFlags.None));
        Assert.All(SlotFilter.All, f => Assert.NotEqual("Any", SlotFilter.Label(f)));
    }
}
