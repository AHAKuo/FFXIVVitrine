namespace Vitrine;

/// <summary> What a mod changes, as Vitrine classifies it from Penumbra's changed items. </summary>
public enum VitrineCategory
{
    Unknown,
    Gear,
    Weapon,
    Accessory,
    Hair,
    Face,
    Body,
    Skin,
    Tail,
    Ears,
    Animation,
    Emote,
    Vfx,
    Sfx,
    Ui,
    Minion,
    Mount,
    Housing,
    Npc,
    Other,
}

/// <summary> Affected item entry on a ModInformation front box. <see cref="ItemId"/> is only a hint; try-on resolves items by name. </summary>
public sealed record AffectedItem(VitrineCategory Category, string Name, ulong? ItemId);

/// <summary> Mod state as a Vitrine design wants it. </summary>
public sealed class VitrineModState
{
    public string ModDirectory { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    /// <summary> Option group → selected option names. Null = mod defaults. </summary>
    public Dictionary<string, List<string>>? Settings { get; set; }
}

/// <summary>
/// A design prepared in Vitrine: one mod with the baseline settings the user chose. Auto designs exist for every catalogued mod
/// (IsAuto = true, id derived from the mod directory); variants are user created from an auto design with different options
/// (IsAuto = false, BaseDesignId set). Previewing a design shows it on the character: the mod is enabled, a full set of its
/// affected gear is equipped and replaced emotes are played. Other plugins (Lamina) reference designs by id through IPC.
/// </summary>
public sealed class VitrineDesign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsAuto { get; set; }
    public Guid? BaseDesignId { get; set; }
    public VitrineCategory Category { get; set; }
    /// <summary> Usually one mod (the design's own). </summary>
    public List<VitrineModState> Mods { get; set; } = new();
}

/// <summary> Read focused view of one Penumbra mod. </summary>
public sealed class ModInformation
{
    public string ModDirectory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public VitrineCategory Type { get; set; }
    public List<AffectedItem> AffectedItems { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    /// <summary> Option group → (options, group type as Penumbra reports it). </summary>
    public Dictionary<string, (string[] Options, int GroupType)> AvailableSettings { get; set; } = new();
    /// <summary> Collection id → (enabled, priority, settings). Informational. </summary>
    public Dictionary<Guid, (bool Enabled, int Priority, Dictionary<string, List<string>> Settings)> PerCollection { get; set; } = new();
    /// <summary> Collection currently activating this mod for the local player, if any. </summary>
    public Guid? CurrentActiveCollection { get; set; }
}
