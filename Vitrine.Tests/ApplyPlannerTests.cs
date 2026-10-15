using Glamourer.Api.Enums;
using Vitrine;
using Vitrine.TryOn;
using Xunit;

namespace Vitrine.Tests;

public class ApplyPlannerTests
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

    private static readonly AffectedItem[] Outfit =
    {
        Gear("Hat"), Gear("Body A"), Gear("Body B"), Gear("Legs A"), Gear("Boots"), Gear("Ring A"), Gear("Ring B"),
        new(VitrineCategory.Hair, "Customization: Midlander Female Hair 194", null),
    };

    private static FakeResolver Resolver()
        => new FakeResolver()
            .Add("Hat", ApiEquipSlot.Head).Add("Body A", ApiEquipSlot.Body).Add("Body B", ApiEquipSlot.Body)
            .Add("Legs A", ApiEquipSlot.Legs).Add("Boots", ApiEquipSlot.Feet)
            .Add("Ring A", ApiEquipSlot.RFinger).Add("Ring B", ApiEquipSlot.RFinger);

    [Fact]
    public void Candidates_GroupBySlot_InSlotOrder_WithLeftRingWhenTwoRings()
    {
        var candidates = ApplyPlanner.Candidates(Outfit, Resolver());

        Assert.Equal(new[] { ApiEquipSlot.Head, ApiEquipSlot.Body, ApiEquipSlot.Legs, ApiEquipSlot.Feet, ApiEquipSlot.RFinger, ApiEquipSlot.LFinger },
            candidates.Select(c => c.Slot));
        Assert.Equal(new[] { "Body A", "Body B" }, candidates.Single(c => c.Slot == ApiEquipSlot.Body).Items.Select(i => i.Name));
        Assert.All(candidates.Single(c => c.Slot == ApiEquipSlot.LFinger).Items, i => Assert.Equal(ApiEquipSlot.LFinger, i.Slot));
    }

    [Fact]
    public void Default_TakesFirstPiecePerSlot_AndSecondRingOnTheLeft()
    {
        var action = ApplyPlanner.Default(ApplyPlanner.Candidates(Outfit, Resolver()));

        Assert.Equal("Body A", action.Slots[ApiEquipSlot.Body].ItemName);
        Assert.Equal("Ring A", action.Slots[ApiEquipSlot.RFinger].ItemName);
        Assert.Equal("Ring B", action.Slots[ApiEquipSlot.LFinger].ItemName);
        Assert.All(action.Slots.Values, s => Assert.True(s.Enabled));
    }

    [Fact]
    public void Plan_WithoutAction_IsTheFullSetPlan()
    {
        var plan = ApplyPlanner.Plan(null, Outfit, Resolver());
        var full = FullSetPlanner.Plan(Outfit, Resolver());

        Assert.Equal(full.Items.Select(i => i.Name), plan.Items.Select(i => i.Name));
        Assert.Empty(plan.Missing);
    }

    [Fact]
    public void Plan_HonoursDisabledSlots_NoneAndExplicitPieces()
    {
        var action = ApplyPlanner.Default(ApplyPlanner.Candidates(Outfit, Resolver()));
        action.Slots[ApiEquipSlot.Head].Enabled = false;                    // leave the head alone
        action.Slots[ApiEquipSlot.Legs] = new ApplySlot { Enabled = true }; // none
        action.Slots[ApiEquipSlot.Body].ItemName = "Body B";               // the other body
        action.Slots[ApiEquipSlot.Body].ItemId   = null;

        var plan = ApplyPlanner.Plan(action, Outfit, Resolver());

        Assert.Equal(new[] { "Body B", "Boots", "Ring A", "Ring B" }, plan.Items.Select(i => i.Name));
        Assert.Equal(new[] { ApiEquipSlot.Body, ApiEquipSlot.Feet, ApiEquipSlot.RFinger, ApiEquipSlot.LFinger }, plan.Items.Select(i => i.Slot));
        Assert.Empty(plan.Missing);
    }

    [Fact]
    public void Plan_ReportsPiecesTheModNoLongerOffers()
    {
        var action = new ApplyAction();
        action.Slots[ApiEquipSlot.Body] = new ApplySlot { Enabled = true, ItemId = 999999, ItemName = "Gone" };

        var plan = ApplyPlanner.Plan(action, Outfit, Resolver());

        Assert.Empty(plan.Items);
        Assert.Equal(new[] { ApiEquipSlot.Body }, plan.Missing);
    }

    [Fact]
    public void Describe_ListsEnabledSlots()
    {
        var action = new ApplyAction();
        action.Slots[ApiEquipSlot.Body] = new ApplySlot { Enabled = true, ItemName = "Body B" };
        action.Slots[ApiEquipSlot.Legs] = new ApplySlot { Enabled = true };
        action.Slots[ApiEquipSlot.Feet] = new ApplySlot { Enabled = false, ItemName = "Boots" };

        Assert.Equal("Body: Body B · Legs: none", ApplyPlanner.Describe(action));
        Assert.Equal("default", ApplyPlanner.Describe(null));
    }
}
