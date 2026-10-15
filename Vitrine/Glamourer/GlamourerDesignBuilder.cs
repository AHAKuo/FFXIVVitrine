using Glamourer.Api.Enums;
using Newtonsoft.Json.Linq;
using Vitrine.TryOn;

namespace Vitrine.Glamourer;

/// <summary>
/// Builds the JSON of a Glamourer design (FileVersion 3, the on-disk design layout) from a Vitrine design: the chosen
/// pieces as applied equipment, optional applied customizations (hair / face / tail), nothing else applied, plus the
/// mod association so the design carries the mod with it. Pure logic, unit tested.
/// </summary>
public static class GlamourerDesignBuilder
{
    /// <summary> Glamourer's design file version. </summary>
    public const int FileVersion = 3;

    /// <summary> Builds the design JObject. </summary>
    /// <param name="name"> Design name. </param>
    /// <param name="description"> Design description. </param>
    /// <param name="items"> Pieces to equip (one per slot). </param>
    /// <param name="customize"> Glamourer customize key → value to apply (e.g. "Hairstyle" → 194). </param>
    /// <param name="mod"> The mod association, or null for none. </param>
    /// <param name="availableSettings"> The mod's option groups, so every option is listed with its enabled / disabled state. </param>
    public static JObject Build(string name, string description, IEnumerable<EquippableItem> items, IReadOnlyDictionary<string, int>? customize,
        VitrineModState? mod, IReadOnlyDictionary<string, (string[] Options, int GroupType)>? availableSettings)
    {
        var equipment = new JObject();
        foreach (var item in items)
            equipment[item.Slot.ToString()] = new JObject
            {
                ["ItemId"]     = item.ItemId,
                ["Stain"]      = 0,
                ["Stain2"]     = 0,
                ["Apply"]      = true,
                ["ApplyStain"] = false,
            };

        var customizeObj = new JObject();
        if (customize is not null)
            foreach (var (key, value) in customize)
                customizeObj[key] = new JObject { ["Value"] = value, ["Apply"] = true };

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var design = new JObject
        {
            ["FileVersion"]    = FileVersion,
            ["Identifier"]     = Guid.NewGuid().ToString(),
            ["CreationDate"]   = now,
            ["LastEdit"]       = now,
            ["Name"]           = name,
            ["Description"]    = description,
            ["Color"]          = string.Empty,
            ["QuickDesign"]    = false,
            ["Tags"]           = new JArray(),
            ["WriteProtected"] = false,
            ["Equipment"]      = equipment,
            ["Bonus"]          = new JObject(),
            ["Customize"]      = customizeObj,
            ["Parameters"]     = new JObject(),
            ["Materials"]      = new JObject(),
        };

        if (mod is not null)
            design["Mods"] = new JArray(ModAssociation(mod, availableSettings));

        return design;
    }

    /// <summary>
    /// One entry of a design's "Mods" array as Glamourer writes it: name, directory, state, priority and, when the design
    /// pins settings, every group with each option enabled or disabled. Null settings (Penumbra defaults) list no settings.
    /// </summary>
    public static JObject ModAssociation(VitrineModState mod, IReadOnlyDictionary<string, (string[] Options, int GroupType)>? availableSettings)
    {
        var obj = new JObject
        {
            ["Name"]      = mod.ModName,
            ["Directory"] = mod.ModDirectory,
            ["Version"]   = 1,
            ["State"]     = mod.Enabled ? "Enabled" : "Disabled",
            ["Priority"]  = mod.Priority,
        };

        if (mod.Settings is null)
            return obj;

        var settings = new JArray();
        var groups   = new List<string>();
        if (availableSettings is not null)
            groups.AddRange(availableSettings.Keys);
        foreach (var group in mod.Settings.Keys)
            if (!groups.Contains(group))
                groups.Add(group);

        foreach (var group in groups)
        {
            var selected = mod.Settings.GetValueOrDefault(group) ?? new List<string>();
            var names    = new List<string>();
            if (availableSettings is not null && availableSettings.TryGetValue(group, out var available))
                names.AddRange(available.Options);
            foreach (var option in selected)
                if (!names.Contains(option))
                    names.Add(option);

            var options = new JArray();
            foreach (var option in names)
                options.Add(new JObject { ["Name"] = option, ["State"] = selected.Contains(option) ? "Enabled" : "Disabled" });
            settings.Add(new JObject { ["Name"] = group, ["Options"] = options });
        }

        obj["Settings"] = settings;
        return obj;
    }

    /// <summary> The equip slots Glamourer accepts in a design, in order. </summary>
    public static IReadOnlyList<ApiEquipSlot> Slots
        => ApplyPlanner.SlotOrder;
}
