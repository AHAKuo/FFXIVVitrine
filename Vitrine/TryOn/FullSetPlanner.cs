using Glamourer.Api.Enums;

namespace Vitrine.TryOn;

/// <summary> Outcome of planning a full set from a mod's affected items. </summary>
public sealed record FullSetPlan(
    /// <summary> One item per slot, in slot order. </summary>
    IReadOnlyList<EquippableItem> Items,
    /// <summary> Affected names that did not resolve to an equippable item. </summary>
    IReadOnlyList<string> Unresolved,
    /// <summary> Affected names that resolved but lost to an earlier item in the same slot. </summary>
    IReadOnlyList<string> Alternatives);

/// <summary>
/// Picks one affected item per equipment slot so a whole look can be shown at once: a mod that replaces a head, two bodies,
/// two legs and feet yields head + first body + first legs + feet. List order decides which alternative wins.
/// Pure logic: no game access, fully unit testable through <see cref="IItemResolver"/>.
/// </summary>
public static class FullSetPlanner
{
    /// <summary> Slot order of plans. </summary>
    public static readonly ApiEquipSlot[] SlotOrder =
    {
        ApiEquipSlot.MainHand, ApiEquipSlot.OffHand, ApiEquipSlot.Head, ApiEquipSlot.Body, ApiEquipSlot.Hands,
        ApiEquipSlot.Legs, ApiEquipSlot.Feet, ApiEquipSlot.Ears, ApiEquipSlot.Neck, ApiEquipSlot.Wrists,
        ApiEquipSlot.RFinger, ApiEquipSlot.LFinger,
    };

    /// <summary> Plans a full set from the affected items in the order given. </summary>
    public static FullSetPlan Plan(IEnumerable<AffectedItem> affected, IItemResolver resolver)
    {
        var chosen       = new Dictionary<ApiEquipSlot, EquippableItem>();
        var unresolved   = new List<string>();
        var alternatives = new List<string>();

        foreach (var entry in affected)
        {
            if (!IsGearCategory(entry.Category))
                continue;

            if (!resolver.TryResolve(entry.Name, out var item))
            {
                unresolved.Add(entry.Name);
                continue;
            }

            // Rings: a second ring goes on the other hand instead of replacing the first.
            var slot = item.Slot;
            if (slot == ApiEquipSlot.RFinger && chosen.ContainsKey(ApiEquipSlot.RFinger) && !chosen.ContainsKey(ApiEquipSlot.LFinger))
                slot = ApiEquipSlot.LFinger;

            if (chosen.ContainsKey(slot))
            {
                alternatives.Add(entry.Name);
                continue;
            }

            chosen[slot] = item with { Slot = slot };
        }

        var ordered = SlotOrder.Where(chosen.ContainsKey).Select(s => chosen[s]).ToList();
        return new FullSetPlan(ordered, unresolved, alternatives);
    }

    /// <summary> Categories whose affected items may resolve to equippable gear. </summary>
    public static bool IsGearCategory(VitrineCategory category)
        => category is VitrineCategory.Gear or VitrineCategory.Weapon or VitrineCategory.Accessory or VitrineCategory.Unknown or VitrineCategory.Other;
}
