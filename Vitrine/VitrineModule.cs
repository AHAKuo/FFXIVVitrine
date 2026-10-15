using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Vitrine.Catalog;
using Vitrine.Designs;
using Vitrine.Glamourer;
using Vitrine.Lamina;
using Vitrine.Penumbra;
using Vitrine.Photos;
using Vitrine.TryOn;
using Vitrine.UI;

namespace Vitrine;

/// <summary>
/// Heart of the plugin: owns the Penumbra reader, the Glamourer link, the mod catalog, the design store, photos,
/// previews, settings and the window. <see cref="Plugin"/> constructs one instance and disposes it on unload.
/// </summary>
public sealed class VitrineModule : IDisposable
{
    private static readonly TimeSpan CollectionRefreshInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan CollectionFactsInterval   = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DesignRebuildInterval     = TimeSpan.FromSeconds(1);

    private readonly IPluginLog _log;
    private readonly IFramework _framework;
    private readonly WindowSystem _windowSystem = new("Vitrine");
    private readonly VitrineWindow _window;
    private readonly string _settingsPath;

    private DateTime _nextCollectionRefresh = DateTime.MinValue;
    private DateTime _nextCollectionFacts = DateTime.MinValue;
    private DateTime _nextDesignRebuild = DateTime.MinValue;
    private bool _collectionRefreshPending;
    private bool _designsDirty;

    public VitrineModule(IDalamudPluginInterface pi, IPluginLog log, IFramework framework, ITextureProvider textures,
        IDataManager data, IClientState clientState, IObjectTable objects, ICondition condition, string dataDirectory)
    {
        _log       = log;
        _framework = framework;

        DataDirectory = dataDirectory;
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        Settings      = VitrineSettings.Load(_settingsPath, m => log.Warning(m));

        Reader      = new PenumbraReader(pi, log);
        Glamourer   = new GlamourerLink(pi, log);
        Lamina      = new LaminaLink(pi, log);
        Catalog     = new ModCatalog(Reader, Path.Combine(dataDirectory, "mods.json"), m => log.Warning(m), m => log.Debug(m));
        var items   = new ItemResolver(data, log);
        DesignStore = new VitrineDesignStore(Path.Combine(dataDirectory, "designs.json"), m => log.Warning(m), items);
        Photos      = new ModPhotoService(
            Path.Combine(dataDirectory, "photos"),
            new HttpPhotoHttp(),
            () => Reader.GetModRootDirectory(),
            () => Settings.FetchPhotosFromModPages,
            m => log.Warning(m),
            m => log.Debug(m));
        EyeColors = new EyeColorTable(data, log);
        Preview = new PreviewService(Reader, Glamourer, items, new EmotePlayer(data, condition, clientState, log),
            clientState, objects, framework, log, Lamina, () => Settings.HandBackToLamina);

        Catalog.LoadCache();
        DesignStore.Load();
        DesignStore.UpdateFromCatalog(Catalog.Mods);

        Catalog.Changed               += OnCatalogChanged;
        DesignStore.Changed           += OnDesignsChanged;
        Reader.AvailabilityChanged    += OnAvailabilityChanged;
        Glamourer.AvailabilityChanged += OnAvailabilityChanged;
        Reader.ModAdded               += dir => Catalog.QueueMod(dir);
        Reader.ModDeleted             += Catalog.RemoveMod;
        Reader.ModMoved               += Catalog.MoveMod;

        _window = new VitrineWindow(this, textures);
        _windowSystem.AddWindow(_window);
        _framework.Update += OnUpdate;

        if (Reader.Available)
        {
            Catalog.BeginFullScan();
            _collectionRefreshPending = true;
        }
    }

    /// <summary> Where settings, caches, designs and photos live. </summary>
    public string DataDirectory { get; }

    /// <summary> The Penumbra reading layer. </summary>
    public IPenumbraReader Reader { get; }

    /// <summary> The Glamourer link used for try-on. </summary>
    public GlamourerLink Glamourer { get; }

    /// <summary> The Lamina link used to hand the character back after a preview. </summary>
    public LaminaLink Lamina { get; }

    /// <summary> The mod catalog. </summary>
    public ModCatalog Catalog { get; }

    /// <summary> Baselines, variants and auto designs. </summary>
    public VitrineDesignStore DesignStore { get; }

    /// <summary> Window settings. </summary>
    public VitrineSettings Settings { get; }

    /// <summary> Display photos per mod (local file or downloaded from the mod page). </summary>
    public ModPhotoService Photos { get; }

    /// <summary> Previews, try-on and emote playback. </summary>
    public PreviewService Preview { get; }

    /// <summary> The game's eye colours for the try-on picker. </summary>
    public EyeColorTable EyeColors { get; }

    /// <summary> False while Penumbra is unavailable. </summary>
    public bool Available
        => Reader.Available;

    /// <summary> All designs (auto + variants), stable order. </summary>
    public IReadOnlyList<VitrineDesign> Designs
        => DesignStore.Designs;

    /// <summary> Looks a design up by id. </summary>
    public VitrineDesign? GetDesign(Guid id)
        => DesignStore.Get(id);

    /// <summary> Raised on the framework thread when designs, the catalog or plugin availability changed. </summary>
    public event Action? CatalogChanged;

    /// <summary> Rescans; <paramref name="force"/> ignores the per-mod source stamps. </summary>
    public void Refresh(bool force = false)
    {
        Reader.Probe();
        Glamourer.Probe();
        if (!Reader.Available)
            return;

        Catalog.BeginFullScan(force);
        _collectionRefreshPending = true;
    }

    /// <summary> Asks for a per-collection state refresh on the next update (throttled). </summary>
    public void RequestCollectionRefresh()
        => _collectionRefreshPending = true;

    /// <summary> Toggles the window. </summary>
    public void ToggleWindow()
        => _window.Toggle();

    /// <summary> null toggles, true opens, false closes. </summary>
    public void SetWindowOpen(bool? open)
    {
        if (open is null)
            _window.Toggle();
        else
            _window.IsOpen = open.Value;
    }

    /// <summary> Draw hook for UiBuilder.Draw. </summary>
    public void Draw()
        => _windowSystem.Draw();

    /// <summary> Persists the settings file. </summary>
    public void SaveSettings()
        => Settings.Save(_settingsPath, m => _log.Warning(m));

    // ---- design edits (all raise CatalogChanged) -----------------------------------------------------------------

    /// <summary> Stores (or clears with null) the baseline of a mod's auto design. </summary>
    public void SetBaseline(string modDirectory, Baseline? baseline)
        => DesignStore.SetBaseline(modDirectory, baseline);

    /// <summary> Creates a variant from a mod's auto design. </summary>
    public Guid CreateVariant(string modDirectory, string name)
        => DesignStore.CreateVariant(modDirectory, name);

    /// <summary> Updates a variant. </summary>
    public bool UpdateVariant(Guid id, string? name = null, bool? enabled = null, int? priority = null, Dictionary<string, List<string>>? settings = null, bool clearSettings = false,
        ApplyAction? applyAction = null, bool clearApplyAction = false)
        => DesignStore.UpdateVariant(id, name, enabled, priority, settings, clearSettings, applyAction, clearApplyAction);

    /// <summary> Deletes a variant. </summary>
    public bool DeleteVariant(Guid id)
        => DesignStore.DeleteVariant(id);

    // ---- preview -------------------------------------------------------------------------------------------------

    /// <summary> Shows the design on the player: mod enabled + redraw + full-set try-on + replaced emote. </summary>
    public string PreviewDesign(VitrineDesign design)
    {
        var info    = design.Mods.Count > 0 ? Catalog.Get(design.Mods[0].ModDirectory) : null;
        var message = Preview.PreviewDesign(design, info);
        RequestCollectionRefresh();
        return message;
    }

    /// <summary> IPC entry: preview by id. </summary>
    public string PreviewDesign(Guid id)
        => GetDesign(id) is { } design ? PreviewDesign(design) : "Unknown design.";

    /// <summary> Applies the design on the player: mod enabled + redraw + exactly the Apply Action's pieces (no guessing, no emotes). </summary>
    public string ApplyDesign(VitrineDesign design)
    {
        var info    = design.Mods.Count > 0 ? Catalog.Get(design.Mods[0].ModDirectory) : null;
        var message = Preview.ApplyDesign(design, info);
        RequestCollectionRefresh();
        return message;
    }

    /// <summary> IPC entry: apply by id. </summary>
    public string ApplyDesign(Guid id)
        => GetDesign(id) is { } design ? ApplyDesign(design) : "Unknown design.";

    /// <summary> Removes a previewed mod's temporary setting from the player. </summary>
    public string ClearPreview(string modDirectory)
    {
        var message = Preview.ClearPreview(modDirectory);
        RequestCollectionRefresh();
        return message;
    }

    /// <summary> Ends a design's preview: mod settings removed, gear reverted, redraw, hand back to Lamina (setting). </summary>
    public string EndPreview(VitrineDesign design)
    {
        var message = Preview.EndPreview(design);
        RequestCollectionRefresh();
        return message;
    }

    /// <summary> Ends whatever is previewed (window close). </summary>
    public string EndAllPreviews()
    {
        var message = Preview.EndAllPreviews();
        if (message.Length > 0)
            RequestCollectionRefresh();
        return message;
    }

    // ---- enable / disable ----------------------------------------------------------------------------------------

    /// <summary>
    /// Enables the design's mod in the player's collection with the design's priority and option settings, permanently
    /// (as if ticked in Penumbra), and records the design as the mod's active one. Enabling another design of the same mod
    /// switches the settings over.
    /// </summary>
    public string EnableDesign(VitrineDesign design)
    {
        if (!Reader.Available)
            return "Penumbra is not available.";
        if (design.Mods.Count == 0)
            return "This design has no mod.";
        if (Reader.GetCollectionForObject(VitrineConstants.LocalPlayerIndex) is not { } collection)
            return "Your character has no collection assigned.";

        var mod  = design.Mods[0];
        var code = (global::Penumbra.Api.Enums.PenumbraApiEc)Reader.SetModEnabled(collection.Id, mod.ModDirectory, true);
        if (!Ok(code))
            return $"Penumbra returned {code} enabling {mod.ModName}.";

        var problems = new List<string>();
        code = (global::Penumbra.Api.Enums.PenumbraApiEc)Reader.SetModPriority(collection.Id, mod.ModDirectory, mod.Priority);
        if (!Ok(code))
            problems.Add($"priority: {code}");
        if (mod.Settings is not null)
            foreach (var (group, options) in mod.Settings)
            {
                code = (global::Penumbra.Api.Enums.PenumbraApiEc)Reader.SetModSettings(collection.Id, mod.ModDirectory, group, options);
                if (!Ok(code))
                    problems.Add($"{group}: {code}");
            }

        DesignStore.SetActiveDesign(mod.ModDirectory, design.Id);
        Reader.RedrawObject(VitrineConstants.LocalPlayerIndex);
        RequestCollectionRefresh();
        return problems.Count == 0
            ? $"{design.Name} enabled in {collection.Name}."
            : $"{design.Name} enabled in {collection.Name}; Penumbra refused {string.Join(", ", problems)}.";
    }

    /// <summary> Disables the design's mod in the player's collection and clears the active design. </summary>
    public string DisableDesign(VitrineDesign design)
    {
        if (!Reader.Available)
            return "Penumbra is not available.";
        if (design.Mods.Count == 0)
            return "This design has no mod.";
        if (Reader.GetCollectionForObject(VitrineConstants.LocalPlayerIndex) is not { } collection)
            return "Your character has no collection assigned.";

        var mod  = design.Mods[0];
        var code = (global::Penumbra.Api.Enums.PenumbraApiEc)Reader.SetModEnabled(collection.Id, mod.ModDirectory, false);
        DesignStore.SetActiveDesign(mod.ModDirectory, null);
        Reader.RedrawObject(VitrineConstants.LocalPlayerIndex);
        RequestCollectionRefresh();
        return Ok(code) ? $"{mod.ModName} disabled in {collection.Name}." : $"Penumbra returned {code} disabling {mod.ModName}.";
    }

    /// <summary> True when the design is the mod's active one and the mod is enabled in the player's collection. </summary>
    public bool IsActive(VitrineDesign design, ModInformation mod, Guid? playerCollection)
        => DesignStore.ActiveDesignFor(mod.ModDirectory) == design.Id
        && playerCollection is { } id && mod.PerCollection.TryGetValue(id, out var state) && state.Enabled;

    private static bool Ok(global::Penumbra.Api.Enums.PenumbraApiEc code)
        => code is global::Penumbra.Api.Enums.PenumbraApiEc.Success or global::Penumbra.Api.Enums.PenumbraApiEc.NothingChanged;

    // ---- internals -----------------------------------------------------------------------------------------------

    private void OnUpdate(IFramework framework)
    {
        Catalog.Tick();
        Photos.Tick();

        var now = DateTime.UtcNow;
        if (_designsDirty && (!Catalog.IsScanning || now >= _nextDesignRebuild))
        {
            _designsDirty      = false;
            _nextDesignRebuild = now + DesignRebuildInterval;
            DesignStore.UpdateFromCatalog(Catalog.Mods);
            if (!Catalog.IsScanning)
                _collectionRefreshPending = true;
        }

        if (_window.IsOpen && now >= _nextCollectionFacts)
        {
            _nextCollectionFacts = now + CollectionFactsInterval;
            _window.RefreshCollectionFacts();
        }

        if (!Catalog.IsScanning && (_collectionRefreshPending || (_window.IsOpen && now >= _nextCollectionRefresh)))
        {
            _collectionRefreshPending = false;
            _nextCollectionRefresh    = DateTime.UtcNow + CollectionRefreshInterval;
            if (Reader.Available)
            {
                Catalog.RefreshCollectionStates();
                _window.OnCollectionStatesRefreshed();
            }
        }
    }

    /// <summary> Catalog ticks can fire every frame during a scan; the design rebuild is throttled in <see cref="OnUpdate"/>. </summary>
    private void OnCatalogChanged()
        => _designsDirty = true;

    private void OnDesignsChanged()
        => CatalogChanged?.Invoke();

    private void OnAvailabilityChanged()
    {
        if (Reader.Available && !Catalog.IsScanning)
        {
            Catalog.BeginFullScan();
            _collectionRefreshPending = true;
        }

        CatalogChanged?.Invoke();
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        try
        {
            Preview.EndAllPreviews();
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: clearing previews on unload failed: {ex.Message}");
        }

        Catalog.SaveCacheIfDirty();
        SaveSettings();
        _windowSystem.RemoveAllWindows();
        _window.Dispose();
        Photos.Dispose();
        Lamina.Dispose();
        Glamourer.Dispose();
        Reader.Dispose();
    }
}
