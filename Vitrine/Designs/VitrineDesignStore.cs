using Glamourer.Api.Enums;
using Vitrine.Catalog;
using Vitrine.TryOn;

namespace Vitrine.Designs;

/// <summary> Serialized form of designs.json. Auto designs are regenerated from the catalog; only baselines and variants are stored. </summary>
public sealed class DesignsFile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary> Mod directory → the user's baseline for that mod's auto design. </summary>
    public Dictionary<string, Baseline> Baselines { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<StoredVariant> Variants { get; set; } = new();

    /// <summary> Mod directory → the design the user enabled through Vitrine (its settings are in the player's collection). </summary>
    public Dictionary<string, Guid> Active { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary> Per-mod baseline: what the auto design applies. Null settings = Penumbra's defaults for the mod. </summary>
public sealed class Baseline
{
    public int Priority { get; set; }
    public Dictionary<string, List<string>>? Settings { get; set; }
    /// <summary> Explicit pieces to equip on apply; null = every affected slot. </summary>
    public ApplyAction? ApplyAction { get; set; }
    /// <summary> Empty the gear slots the design does not fill. </summary>
    public bool ClearOtherSlots { get; set; }
}

/// <summary> A user created variant of an auto design. </summary>
public sealed class StoredVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BaseDesignId { get; set; }
    public string ModDirectory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public Dictionary<string, List<string>>? Settings { get; set; }
    /// <summary> Explicit pieces to equip on apply; null = every affected slot. </summary>
    public ApplyAction? ApplyAction { get; set; }
    /// <summary> Empty the gear slots the design does not fill. </summary>
    public bool ClearOtherSlots { get; set; }
}

/// <summary>
/// Produces the <see cref="VitrineDesign"/> list from the catalog: one auto design per mod (deterministic id) plus the user's variants.
/// Persists baselines and variants to designs.json. Framework-thread only.
/// </summary>
public sealed class VitrineDesignStore
{
    private readonly string _path;
    private readonly Action<string> _logWarning;
    private readonly IItemResolver? _resolver;
    private DesignsFile _file = new();
    private readonly Dictionary<Guid, VitrineDesign> _byId = new();
    private readonly Dictionary<string, VitrineDesign> _autoByMod = new(StringComparer.OrdinalIgnoreCase);
    private List<VitrineDesign> _designs = new();
    private IReadOnlyList<ModInformation> _mods = Array.Empty<ModInformation>();

    /// <param name="resolver"> Resolves affected-item names to game items so designs export their pieces; null = no pieces. </param>
    public VitrineDesignStore(string path, Action<string> logWarning, IItemResolver? resolver = null)
    {
        _path       = path;
        _logWarning = logWarning;
        _resolver   = resolver;
    }

    /// <summary> All designs: auto designs (sorted by name) followed by variants (sorted by name). </summary>
    public IReadOnlyList<VitrineDesign> Designs
        => _designs;

    /// <summary> Raised after designs changed (rebuild from catalog, baseline edit, variant edit). </summary>
    public event Action? Changed;

    public VitrineDesign? Get(Guid id)
        => _byId.GetValueOrDefault(id);

    /// <summary> The auto design of a mod, if the mod is catalogued. </summary>
    public VitrineDesign? AutoDesignFor(string modDirectory)
        => _autoByMod.GetValueOrDefault(modDirectory);

    /// <summary> Variants of a mod (auto design excluded). </summary>
    public IEnumerable<VitrineDesign> VariantsFor(string modDirectory)
        => _designs.Where(d => !d.IsAuto && d.Mods.Count > 0 && string.Equals(d.Mods[0].ModDirectory, modDirectory, StringComparison.OrdinalIgnoreCase));

    /// <summary> True when a design references a mod that is no longer in the catalog. </summary>
    public bool IsOrphan(VitrineDesign design)
        => design.Mods.Count > 0 && _mods.All(m => !string.Equals(m.ModDirectory, design.Mods[0].ModDirectory, StringComparison.OrdinalIgnoreCase));

    /// <summary> The stored baseline of a mod, or null when the mod uses Penumbra defaults. </summary>
    public Baseline? BaselineFor(string modDirectory)
        => _file.Baselines.GetValueOrDefault(modDirectory);

    /// <summary> The design the user enabled for a mod through Vitrine, or null. </summary>
    public Guid? ActiveDesignFor(string modDirectory)
        => _file.Active.TryGetValue(modDirectory, out var id) ? id : null;

    /// <summary> Records (or clears with null) which design is enabled for a mod and saves. </summary>
    public void SetActiveDesign(string modDirectory, Guid? designId)
    {
        if (designId is { } id)
            _file.Active[modDirectory] = id;
        else
            _file.Active.Remove(modDirectory);
        Save();
        Changed?.Invoke();
    }

    /// <summary> Loads designs.json; a broken file is quarantined and an empty store is used. </summary>
    public void Load()
    {
        var file = JsonUtil.ReadOrNull<DesignsFile>(_path, out var error);
        if (error is not null)
        {
            _logWarning($"Vitrine: could not read {_path}: {error.Message}");
            try { System.IO.File.Move(_path, $"{_path}.broken-{DateTime.UtcNow:yyyyMMdd-HHmmss}", overwrite: true); }
            catch { /* best effort */ }
        }

        _file = file ?? new DesignsFile();
        _file.Baselines = new Dictionary<string, Baseline>(_file.Baselines, StringComparer.OrdinalIgnoreCase);
        _file.Active    = new Dictionary<string, Guid>(_file.Active, StringComparer.OrdinalIgnoreCase);
        Rebuild();
    }

    /// <summary> Writes designs.json atomically. </summary>
    public void Save()
    {
        try
        {
            JsonUtil.WriteAtomic(_path, _file);
        }
        catch (Exception ex)
        {
            _logWarning($"Vitrine: could not write {_path}: {ex.Message}");
        }
    }

    /// <summary> Regenerates auto designs from the catalog and re-attaches variants. </summary>
    public void UpdateFromCatalog(IReadOnlyList<ModInformation> mods)
    {
        _mods = mods;
        Rebuild();
    }

    /// <summary> Sets (or clears with null) the baseline of a mod and saves. </summary>
    public void SetBaseline(string modDirectory, Baseline? baseline)
    {
        if (baseline is null)
            _file.Baselines.Remove(modDirectory);
        else
            _file.Baselines[modDirectory] = baseline;
        Save();
        Rebuild();
    }

    /// <summary> Creates a variant of a mod's auto design, starting from the current baseline. Returns the new design id. </summary>
    public Guid CreateVariant(string modDirectory, string name)
    {
        var auto = _autoByMod.GetValueOrDefault(modDirectory);
        var baseline = _file.Baselines.GetValueOrDefault(modDirectory);
        var variant = new StoredVariant
        {
            BaseDesignId = auto?.Id ?? DeterministicGuid.ForAutoDesign(modDirectory),
            ModDirectory = modDirectory,
            Name         = string.IsNullOrWhiteSpace(name) ? $"{auto?.Name ?? modDirectory} · Variant" : name,
            Priority     = baseline?.Priority ?? 0,
            Settings     = baseline?.Settings?.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            ApplyAction  = CloneAction(baseline?.ApplyAction),
            ClearOtherSlots = baseline?.ClearOtherSlots ?? false,
        };
        _file.Variants.Add(variant);
        Save();
        Rebuild();
        return variant.Id;
    }

    /// <summary> Updates a variant in place and saves. Returns false when the id is not a variant. </summary>
    /// <param name="applyAction"> New apply action; <paramref name="clearApplyAction"/> resets it to the default (null). </param>
    public bool UpdateVariant(Guid id, string? name = null, bool? enabled = null, int? priority = null, Dictionary<string, List<string>>? settings = null, bool clearSettings = false,
        ApplyAction? applyAction = null, bool clearApplyAction = false, bool? clearOtherSlots = null)
    {
        var variant = _file.Variants.FirstOrDefault(v => v.Id == id);
        if (variant is null)
            return false;

        if (clearOtherSlots is { } clear) variant.ClearOtherSlots = clear;
        if (name is not null) variant.Name = name;
        if (enabled is { } e) variant.Enabled = e;
        if (priority is { } p) variant.Priority = p;
        if (clearSettings) variant.Settings = null;
        else if (settings is not null) variant.Settings = settings;
        if (clearApplyAction) variant.ApplyAction = null;
        else if (applyAction is not null) variant.ApplyAction = applyAction;
        Save();
        Rebuild();
        return true;
    }

    /// <summary> Deletes a variant and saves. </summary>
    public bool DeleteVariant(Guid id)
    {
        var removed = _file.Variants.RemoveAll(v => v.Id == id) > 0;
        if (!removed)
            return false;

        Save();
        Rebuild();
        return true;
    }

    private void Rebuild()
    {
        _byId.Clear();
        _autoByMod.Clear();
        var list = new List<VitrineDesign>();

        foreach (var mod in _mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            var baseline = _file.Baselines.GetValueOrDefault(mod.ModDirectory);
            var design = new VitrineDesign
            {
                Id       = DeterministicGuid.ForAutoDesign(mod.ModDirectory),
                Name     = mod.Name,
                IsAuto   = true,
                Category = mod.Type,
                Mods =
                {
                    new VitrineModState
                    {
                        ModDirectory = mod.ModDirectory,
                        ModName      = mod.Name,
                        Enabled      = true,
                        Priority     = baseline?.Priority ?? 0,
                        Settings     = baseline?.Settings?.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
                    },
                },
                ApplyAction     = CloneAction(baseline?.ApplyAction),
                ClearOtherSlots = baseline?.ClearOtherSlots ?? false,
            };
            design.Pieces         = ResolvePieces(mod, design.ApplyAction);
            design.Customizations = DesignCustomization.FromAffectedItems(mod.AffectedItems);
            list.Add(design);
            _byId[design.Id]               = design;
            _autoByMod[mod.ModDirectory]   = design;
        }

        foreach (var variant in _file.Variants.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
        {
            var auto = _autoByMod.GetValueOrDefault(variant.ModDirectory);
            var design = new VitrineDesign
            {
                Id           = variant.Id,
                Name         = variant.Name,
                IsAuto       = false,
                BaseDesignId = variant.BaseDesignId,
                Category     = auto?.Category ?? VitrineCategory.Unknown,
                Mods =
                {
                    new VitrineModState
                    {
                        ModDirectory = variant.ModDirectory,
                        ModName      = auto?.Name ?? variant.ModDirectory,
                        Enabled      = variant.Enabled,
                        Priority     = variant.Priority,
                        Settings     = variant.Settings?.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
                    },
                },
                ApplyAction     = CloneAction(variant.ApplyAction),
                ClearOtherSlots = variant.ClearOtherSlots,
            };
            var mod = _mods.FirstOrDefault(m => string.Equals(m.ModDirectory, variant.ModDirectory, StringComparison.OrdinalIgnoreCase));
            design.Pieces         = ResolvePieces(mod, design.ApplyAction);
            design.Customizations = mod is null ? null : DesignCustomization.FromAffectedItems(mod.AffectedItems);
            list.Add(design);
            _byId[design.Id] = design;
        }

        _designs = list;
        Changed?.Invoke();
    }

    /// <summary> The pieces a design equips, one per affected slot, from the mod's affected items. Null without a resolver or mod. </summary>
    private Dictionary<ApiEquipSlot, ApplyPiece>? ResolvePieces(ModInformation? mod, ApplyAction? action)
    {
        if (_resolver is null || mod is null)
            return null;

        var result = new Dictionary<ApiEquipSlot, ApplyPiece>();
        foreach (var item in ApplyPlanner.Plan(action, mod.AffectedItems, _resolver).Items)
            result[item.Slot] = new ApplyPiece { ItemId = item.ItemId, ItemName = item.Name };
        return result;
    }

    /// <summary> Deep copy so designs handed out never alias the stored file. </summary>
    public static ApplyAction? CloneAction(ApplyAction? action)
    {
        if (action is null)
            return null;

        var copy = new ApplyAction();
        foreach (var (slot, choice) in action.Slots)
            copy.Slots[slot] = new ApplySlot { Enabled = choice.Enabled, ItemId = choice.ItemId, ItemName = choice.ItemName };
        return copy;
    }
}
