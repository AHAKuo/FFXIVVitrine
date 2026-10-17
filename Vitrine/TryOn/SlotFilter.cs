using Glamourer.Api.Enums;

namespace Vitrine.TryOn;

/// <summary> Equipment slots a mod affects, as a flag set so the Mods tab can filter by several at once. </summary>
[Flags]
public enum EquipSlotFlags
{
    None     = 0,
    MainHand = 1 << 0,
    OffHand  = 1 << 1,
    Head     = 1 << 2,
    Body     = 1 << 3,
    Hands    = 1 << 4,
    Legs     = 1 << 5,
    Feet     = 1 << 6,
    Ears     = 1 << 7,
    Neck     = 1 << 8,
    Wrists   = 1 << 9,
    /// <summary> Either ring slot. </summary>
    Fingers  = 1 << 10,
}

/// <summary>
/// Which equipment slots a mod's affected items fill. Pure logic over <see cref="IItemResolver"/>: an affected gear name
/// that resolves to an equippable item contributes that item's slot; unresolved names and non-gear entries contribute nothing.
/// </summary>
public static class SlotFilter
{
    /// <summary> Every flag in display order, one per bit. </summary>
    public static readonly IReadOnlyList<EquipSlotFlags> All =
    [
        EquipSlotFlags.MainHand, EquipSlotFlags.OffHand, EquipSlotFlags.Head, EquipSlotFlags.Body, EquipSlotFlags.Hands,
        EquipSlotFlags.Legs, EquipSlotFlags.Feet, EquipSlotFlags.Ears, EquipSlotFlags.Neck, EquipSlotFlags.Wrists,
        EquipSlotFlags.Fingers,
    ];

    /// <summary> The flag of a Glamourer slot; both ring slots map to <see cref="EquipSlotFlags.Fingers"/>. </summary>
    public static EquipSlotFlags FlagOf(ApiEquipSlot slot)
        => slot switch
        {
            ApiEquipSlot.MainHand => EquipSlotFlags.MainHand,
            ApiEquipSlot.OffHand  => EquipSlotFlags.OffHand,
            ApiEquipSlot.Head     => EquipSlotFlags.Head,
            ApiEquipSlot.Body     => EquipSlotFlags.Body,
            ApiEquipSlot.Hands    => EquipSlotFlags.Hands,
            ApiEquipSlot.Legs     => EquipSlotFlags.Legs,
            ApiEquipSlot.Feet     => EquipSlotFlags.Feet,
            ApiEquipSlot.Ears     => EquipSlotFlags.Ears,
            ApiEquipSlot.Neck     => EquipSlotFlags.Neck,
            ApiEquipSlot.Wrists   => EquipSlotFlags.Wrists,
            ApiEquipSlot.RFinger  => EquipSlotFlags.Fingers,
            ApiEquipSlot.LFinger  => EquipSlotFlags.Fingers,
            _                     => EquipSlotFlags.None,
        };

    /// <summary> The slots the affected items fill. </summary>
    public static EquipSlotFlags SlotsOf(IEnumerable<AffectedItem> affected, IItemResolver resolver)
    {
        var flags = EquipSlotFlags.None;
        foreach (var entry in affected)
        {
            if (!FullSetPlanner.IsGearCategory(entry.Category))
                continue;
            if (resolver.TryResolve(entry.Name, out var item))
                flags |= FlagOf(item.Slot);
        }

        return flags;
    }

    /// <summary> True when the mod fills at least one of the wanted slots; an empty wanted set matches everything. </summary>
    public static bool Matches(EquipSlotFlags wanted, IEnumerable<AffectedItem> affected, IItemResolver resolver)
        => wanted == EquipSlotFlags.None || (SlotsOf(affected, resolver) & wanted) != 0;

    /// <summary> Display name of one flag. </summary>
    public static string Label(EquipSlotFlags flag)
        => flag switch
        {
            EquipSlotFlags.MainHand => "Weapon (main hand)",
            EquipSlotFlags.OffHand  => "Off hand",
            EquipSlotFlags.Head     => "Head",
            EquipSlotFlags.Body     => "Body",
            EquipSlotFlags.Hands    => "Hands",
            EquipSlotFlags.Legs     => "Legs",
            EquipSlotFlags.Feet     => "Feet",
            EquipSlotFlags.Ears     => "Earrings",
            EquipSlotFlags.Neck     => "Necklace",
            EquipSlotFlags.Wrists   => "Bracelets",
            EquipSlotFlags.Fingers  => "Rings",
            _                       => "Any",
        };

    /// <summary> Short combo preview: "Any", one label, or "N slots". </summary>
    public static string Summary(EquipSlotFlags flags)
    {
        var set = All.Where(f => flags.HasFlag(f)).ToList();
        return set.Count switch
        {
            0 => "Any",
            1 => Label(set[0]),
            _ => $"{set.Count} slots",
        };
    }
}
