using Glamourer.Api.Enums;
using Vitrine;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class FullSetPlannerTests
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

    private static AffectedItem Gear(string name)
        => new(VitrineCategory.Gear, name, null);

    [Fact]
    public void OneItemPerSlot_FirstAlternativeWins()
    {
        var resolver = new FakeResolver()
            .Add("Hat", ApiEquipSlot.Head)
            .Add("Body A", ApiEquipSlot.Body)
            .Add("Body B", ApiEquipSlot.Body)
            .Add("Legs A", ApiEquipSlot.Legs)
            .Add("Legs B", ApiEquipSlot.Legs)
            .Add("Boots", ApiEquipSlot.Feet);

        var plan = FullSetPlanner.Plan(new[] { Gear("Body A"), Gear("Hat"), Gear("Legs B"), Gear("Body B"), Gear("Legs A"), Gear("Boots") }, resolver);

        Assert.Equal(new[] { "Hat", "Body A", "Legs B", "Boots" }, plan.Items.Select(i => i.Name));
        Assert.Equal(new[] { ApiEquipSlot.Head, ApiEquipSlot.Body, ApiEquipSlot.Legs, ApiEquipSlot.Feet }, plan.Items.Select(i => i.Slot));
        Assert.Equal(new[] { "Body B", "Legs A" }, plan.Alternatives);
        Assert.Empty(plan.Unresolved);
    }

    [Fact]
    public void UnknownNames_AreReportedNotEquipped()
    {
        var resolver = new FakeResolver().Add("Boots", ApiEquipSlot.Feet);
        var plan = FullSetPlanner.Plan(new[] { Gear("Critical Hit A-1 Tour Boots"), Gear("Boots") }, resolver);

        Assert.Single(plan.Items);
        Assert.Equal(new[] { "Critical Hit A-1 Tour Boots" }, plan.Unresolved);
    }

    [Fact]
    public void SecondRing_GoesOnTheOtherHand()
    {
        var resolver = new FakeResolver().Add("Ring A", ApiEquipSlot.RFinger).Add("Ring B", ApiEquipSlot.RFinger).Add("Ring C", ApiEquipSlot.RFinger);
        var plan = FullSetPlanner.Plan(new[] { Gear("Ring A"), Gear("Ring B"), Gear("Ring C") }, resolver);

        Assert.Equal(new[] { ApiEquipSlot.RFinger, ApiEquipSlot.LFinger }, plan.Items.Select(i => i.Slot));
        Assert.Equal(new[] { "Ring C" }, plan.Alternatives);
    }

    [Fact]
    public void NonGearCategories_AreIgnored()
    {
        var resolver = new FakeResolver().Add("Hum", ApiEquipSlot.Head);
        var plan = FullSetPlanner.Plan(new[] { new AffectedItem(VitrineCategory.Emote, "Hum", null), new AffectedItem(VitrineCategory.Hair, "Hair 5", null) }, resolver);

        Assert.Empty(plan.Items);
        Assert.Empty(plan.Unresolved);
    }
}
