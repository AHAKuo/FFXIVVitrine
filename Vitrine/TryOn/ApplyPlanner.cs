using Glamourer.Api.Enums;

namespace Vitrine.TryOn;

/// <summary> The pieces a mod offers for one equipment slot, in affected-item order. </summary>
public sealed record SlotCandidates(ApiEquipSlot Slot, IReadOnlyList<EquippableItem> Items);

/// <summary> Outcome of planning an apply. </summary>
public sealed record ApplyPlan(
    /// <summary> What to equip, in slot order. </summary>
    IReadOnlyList<EquippableItem> Items,
    /// <summary> Slots the action names whose chosen piece the mod no longer offers. </summary>
    IReadOnlyList<ApiEquipSlot> Missing);

/// <summary>
/// Turns a design's <see cref="ApplyAction"/> into the exact items to equip. Without an action the full-set plan is used,
/// so Apply and Try-on agree until the user decides otherwise. Pure logic, unit tested.
/// </summary>
public static class ApplyPlanner
{
    /// <summary> Slot order of plans and editors. </summary>
    public static readonly IReadOnlyList<ApiEquipSlot> SlotOrder = FullSetPlanner.SlotOrder;

    /// <summary>
    /// Groups the mod's equippable affected items by slot, in slot order. Rings: when the mod offers more than one ring the
    /// left finger is offered too with the same pieces, so both hands can be set.
    /// </summary>
    public static IReadOnlyList<SlotCandidates> Candidates(IEnumerable<AffectedItem> affected, IItemResolver resolver)
    {
        var bySlot = new Dictionary<ApiEquipSlot, List<EquippableItem>>();
        foreach (var entry in affected)
        {
            if (!FullSetPlanner.IsGearCategory(entry.Category) || !resolver.TryResolve(entry.Name, out var item))
                continue;

            if (!bySlot.TryGetValue(item.Slot, out var list))
                bySlot[item.Slot] = list = new List<EquippableItem>();
            if (list.All(i => i.ItemId != item.ItemId))
                list.Add(item);
        }

        if (bySlot.TryGetValue(ApiEquipSlot.RFinger, out var rings) && rings.Count > 1 && !bySlot.ContainsKey(ApiEquipSlot.LFinger))
            bySlot[ApiEquipSlot.LFinger] = rings.Select(r => r with { Slot = ApiEquipSlot.LFinger }).ToList();

        return SlotOrder.Where(bySlot.ContainsKey).Select(s => new SlotCandidates(s, bySlot[s])).ToList();
    }

    /// <summary> The default action: every affected slot enabled with its first piece; the left finger takes the second ring. </summary>
    public static ApplyAction Default(IReadOnlyList<SlotCandidates> candidates)
    {
        var action = new ApplyAction();
        foreach (var slot in candidates)
        {
            var pick = slot.Slot == ApiEquipSlot.LFinger && slot.Items.Count > 1 ? slot.Items[1] : slot.Items[0];
            action.Slots[slot.Slot] = new ApplySlot { Enabled = true, ItemId = pick.ItemId, ItemName = pick.Name };
        }

        return action;
    }

    /// <summary> Plans what to equip for a design. Null action = full-set plan. </summary>
    public static ApplyPlan Plan(ApplyAction? action, IEnumerable<AffectedItem> affected, IItemResolver resolver)
    {
        if (action is null)
            return new ApplyPlan(FullSetPlanner.Plan(affected, resolver).Items, Array.Empty<ApiEquipSlot>());

        var candidates = Candidates(affected, resolver).ToDictionary(c => c.Slot, c => c.Items);
        var items      = new List<EquippableItem>();
        var missing    = new List<ApiEquipSlot>();
        foreach (var slot in SlotOrder)
        {
            if (!action.Slots.TryGetValue(slot, out var choice) || !choice.Enabled || choice.ItemId is null && choice.ItemName is null)
                continue;

            var offered = candidates.GetValueOrDefault(slot);
            var item    = offered?.FirstOrDefault(i => choice.ItemId is { } id ? i.ItemId == id : string.Equals(i.Name, choice.ItemName, StringComparison.OrdinalIgnoreCase));
            if (item is { ItemId: not 0 } found)
                items.Add(found with { Slot = slot });
            else
                missing.Add(slot);
        }

        return new ApplyPlan(items, missing);
    }

    /// <summary>
    /// Glamourer's custom item id for an empty gear slot: <c>uint.MaxValue - 128 - penumbraSlotIndex</c> (Head 3, Body 4,
    /// Hands 5, Legs 7, Feet 8, Ears 9, Neck 10, Wrists 11, both rings 12). Weapons have none (it depends on the weapon type).
    /// </summary>
    public static ulong? NothingId(ApiEquipSlot slot)
        => slot switch
        {
            ApiEquipSlot.Head    => uint.MaxValue - 128 - 3,
            ApiEquipSlot.Body    => uint.MaxValue - 128 - 4,
            ApiEquipSlot.Hands   => uint.MaxValue - 128 - 5,
            ApiEquipSlot.Legs    => uint.MaxValue - 128 - 7,
            ApiEquipSlot.Feet    => uint.MaxValue - 128 - 8,
            ApiEquipSlot.Ears    => uint.MaxValue - 128 - 9,
            ApiEquipSlot.Neck    => uint.MaxValue - 128 - 10,
            ApiEquipSlot.Wrists  => uint.MaxValue - 128 - 11,
            ApiEquipSlot.RFinger => uint.MaxValue - 128 - 12,
            ApiEquipSlot.LFinger => uint.MaxValue - 128 - 12,
            _                    => null,
        };

    /// <summary> Gear slots that can be emptied, in slot order (weapons excluded). </summary>
    public static readonly IReadOnlyList<ApiEquipSlot> ClearableSlots = SlotOrder.Where(s => NothingId(s) is not null).ToList();

    /// <summary> The slots a design empties when it clears other slots: every clearable slot its pieces do not fill. </summary>
    public static IReadOnlyList<ApiEquipSlot> SlotsToClear(IEnumerable<EquippableItem> pieces)
    {
        var used = pieces.Select(p => p.Slot).ToHashSet();
        return ClearableSlots.Where(s => !used.Contains(s)).ToList();
    }

    /// <summary> Human summary of an action for cards ("Head · Body: Milky Chest · Feet: none"). Null action → "default". </summary>
    public static string Describe(ApplyAction? action)
    {
        if (action is null)
            return "default";

        var parts = new List<string>();
        foreach (var slot in SlotOrder)
        {
            if (!action.Slots.TryGetValue(slot, out var choice) || !choice.Enabled)
                continue;
            parts.Add(choice.ItemName is { Length: > 0 } name ? $"{SlotLabel(slot)}: {name}" : $"{SlotLabel(slot)}: none");
        }

        return parts.Count == 0 ? "no slots" : string.Join(" · ", parts);
    }

    /// <summary> Short slot names for the editor. </summary>
    public static string SlotLabel(ApiEquipSlot slot)
        => slot switch
        {
            ApiEquipSlot.MainHand => "Main hand",
            ApiEquipSlot.OffHand  => "Off hand",
            ApiEquipSlot.RFinger  => "Right ring",
            ApiEquipSlot.LFinger  => "Left ring",
            _                     => slot.ToString(),
        };
}
