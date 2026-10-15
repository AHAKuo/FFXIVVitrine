using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Lumina.Excel.Sheets;

namespace Vitrine.TryOn;

/// <summary> A game item that can be equipped through Glamourer. </summary>
public readonly record struct EquippableItem(uint ItemId, string Name, ApiEquipSlot Slot);

/// <summary> Resolves affected-item names to equippable game items. Abstracted so the planner is testable without the game. </summary>
public interface IItemResolver
{
    /// <summary> Finds the item by its exact display name (case-insensitive). False when unknown or not equippable. </summary>
    bool TryResolve(string name, out EquippableItem item);
}

/// <summary>
/// Name → item lookup over the Item sheet. Penumbra's changed-item names are the item sheet names, so this is the reliable
/// route; ids attached to Penumbra's changed-item objects are only used as a tie breaker when a name is ambiguous.
/// The index is built lazily on first use (one pass over the sheet) and kept for the plugin's lifetime.
/// </summary>
public sealed class ItemResolver : IItemResolver
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;
    private Dictionary<string, EquippableItem>? _byName;

    public ItemResolver(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <inheritdoc/>
    public bool TryResolve(string name, out EquippableItem item)
    {
        item = default;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var index = _byName ??= Build();
        return index.TryGetValue(name.Trim(), out item);
    }

    /// <summary> Resolves by item id when a caller has one. </summary>
    public bool TryResolve(uint itemId, out EquippableItem item)
    {
        item = default;
        var row = _data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (row is not { } r || SlotOf(r) is not { } slot)
            return false;

        item = new EquippableItem(r.RowId, r.Name.ExtractText(), slot);
        return true;
    }

    /// <summary> The Glamourer slot an item goes into, or null when it is not equippable through Glamourer (waist, soul crystal, non-gear). </summary>
    public static ApiEquipSlot? SlotOf(in Item item)
    {
        var category = item.EquipSlotCategory.ValueNullable;
        if (category is not { } c)
            return null;

        if (c.MainHand != 0) return ApiEquipSlot.MainHand;
        if (c.OffHand != 0)  return ApiEquipSlot.OffHand;
        if (c.Head != 0)     return ApiEquipSlot.Head;
        if (c.Body != 0)     return ApiEquipSlot.Body;
        if (c.Gloves != 0)   return ApiEquipSlot.Hands;
        if (c.Legs != 0)     return ApiEquipSlot.Legs;
        if (c.Feet != 0)     return ApiEquipSlot.Feet;
        if (c.Ears != 0)     return ApiEquipSlot.Ears;
        if (c.Neck != 0)     return ApiEquipSlot.Neck;
        if (c.Wrists != 0)   return ApiEquipSlot.Wrists;
        if (c.FingerR != 0)  return ApiEquipSlot.RFinger;
        if (c.FingerL != 0)  return ApiEquipSlot.LFinger;
        return null;
    }

    private Dictionary<string, EquippableItem> Build()
    {
        var index = new Dictionary<string, EquippableItem>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var row in _data.GetExcelSheet<Item>())
            {
                var name = row.Name.ExtractText();
                if (name.Length == 0 || SlotOf(row) is not { } slot)
                    continue;

                // Duplicated names: prefer the highest row id (the current version of an item).
                if (!index.TryGetValue(name, out var existing) || existing.ItemId < row.RowId)
                    index[name] = new EquippableItem(row.RowId, name, slot);
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: could not index the Item sheet: {ex.Message}");
        }

        _log.Debug($"Vitrine: indexed {index.Count} equippable items.");
        return index;
    }
}
