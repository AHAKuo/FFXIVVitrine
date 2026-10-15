using System.Text;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Penumbra.Api.Enums;
using Vitrine.Catalog;
using Vitrine.Glamourer;
using Vitrine.Lamina;
using Vitrine.Penumbra;

namespace Vitrine.TryOn;

/// <summary>
/// Shows a design on the local player: writes the mod's temporary settings into the player's collection (Vitrine's key),
/// redraws, equips a full set of the mod's affected gear through Glamourer and plays replaced emotes.
/// Everything runs on the framework thread (called from ImGui draws and IPC).
/// Ending a preview can hand the character back to Lamina (ask it to re-apply its design) when that setting is on.
/// </summary>
public sealed class PreviewService
{
    private readonly IPenumbraReader _penumbra;
    private readonly GlamourerLink _glamourer;
    private readonly ItemResolver _items;
    private readonly EmotePlayer _emotes;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objects;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;
    private readonly LaminaHandBack _handBack;
    private readonly HashSet<string> _previewed = new(StringComparer.OrdinalIgnoreCase);

    public PreviewService(IPenumbraReader penumbra, GlamourerLink glamourer, ItemResolver items, EmotePlayer emotes,
        IClientState clientState, IObjectTable objects, IFramework framework, IPluginLog log,
        ILaminaLink lamina, Func<bool> handBackToLamina)
    {
        _penumbra    = penumbra;
        _glamourer   = glamourer;
        _items       = items;
        _emotes      = emotes;
        _clientState = clientState;
        _objects     = objects;
        _framework   = framework;
        _log         = log;
        _handBack    = new LaminaHandBack(lamina, handBackToLamina);
    }

    /// <summary> Mod directories currently carrying a Vitrine preview setting. </summary>
    public IReadOnlyCollection<string> PreviewedMods
        => _previewed;

    /// <summary> True when Glamourer is loaded and a logged-in player exists (framework thread only). </summary>
    public bool CanTryOn
        => _glamourer.Available && HasPlayer();

    /// <summary> True when Penumbra is loaded and a player exists. </summary>
    public bool CanPreview
        => _penumbra.Available && HasPlayer();

    /// <summary> Enables the design's mods on the player, redraws, equips a full set and plays replaced emotes. </summary>
    public string PreviewDesign(VitrineDesign design, ModInformation? info)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";
        if (!HasPlayer())
            return "No character to preview on.";

        foreach (var mod in design.Mods)
        {
            var settings = (mod.Settings ?? new Dictionary<string, List<string>>())
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
            var code = (PenumbraApiEc)_penumbra.SetTemporaryModSettingsPlayer(VitrineConstants.LocalPlayerIndex, mod.ModDirectory,
                mod.Enabled, mod.Priority, settings, VitrineConstants.TemporarySource, VitrineConstants.TemporaryKey);
            if (code is not (PenumbraApiEc.Success or PenumbraApiEc.NothingChanged))
                return $"Penumbra returned {code} for {mod.ModName}.";

            _previewed.Add(mod.ModDirectory);
        }

        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        var message = new StringBuilder("Mod enabled on you and redrawn.");
        if (info is not null)
        {
            var gear = TryOnFullSet(info, quiet: true);
            if (gear.Length > 0)
                message.Append(' ').Append(gear);
            var emote = PlayEmotes(info);
            if (emote.Length > 0)
                message.Append(' ').Append(emote);
        }

        return message.ToString();
    }

    /// <summary>
    /// Ends a preview as one action: removes the design's mods' preview settings from the player, reverts the gear try-on
    /// (Glamourer automation), redraws, then hands the look back to Lamina when the setting is on.
    /// </summary>
    public string EndPreview(VitrineDesign design)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";

        PenumbraApiEc? error = null;
        foreach (var mod in design.Mods)
        {
            var code = (PenumbraApiEc)_penumbra.RemoveTemporaryModSettingsPlayer(VitrineConstants.LocalPlayerIndex, mod.ModDirectory, VitrineConstants.TemporaryKey);
            if (code is not (PenumbraApiEc.Success or PenumbraApiEc.NothingChanged))
                error = code;
            _previewed.Remove(mod.ModDirectory);
        }

        if (_glamourer.Available && HasPlayer())
            _glamourer.RevertToAutomation(VitrineConstants.LocalPlayerIndex);
        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);

        return _handBack.Append(error is { } e ? $"Preview ended (Penumbra returned {e})" : "Preview ended");
    }

    /// <summary> Removes a mod's preview setting from the player and redraws. Gear try-ons are left alone; see <see cref="EndPreview"/>. </summary>
    public string ClearPreview(string modDirectory)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";

        var code = (PenumbraApiEc)_penumbra.RemoveTemporaryModSettingsPlayer(VitrineConstants.LocalPlayerIndex, modDirectory, VitrineConstants.TemporaryKey);
        _previewed.Remove(modDirectory);
        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        return _handBack.Append(code is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged ? "Preview removed and redrawn" : $"Penumbra returned {code}");
    }

    /// <summary> Removes every Vitrine preview setting from the player, then hands back to Lamina when the setting is on. Called on unload and from the UI. </summary>
    public string ClearAllPreviews(bool redraw = true)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";

        var code = (PenumbraApiEc)_penumbra.RemoveAllTemporaryModSettingsPlayer(VitrineConstants.LocalPlayerIndex, VitrineConstants.TemporaryKey);
        _previewed.Clear();
        if (redraw)
            _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        return _handBack.Append(code is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged ? "All previews removed" : $"Penumbra returned {code}");
    }

    /// <summary> Equips one affected item by name. </summary>
    public string TryOnItem(string name)
    {
        if (!_glamourer.Available)
            return "Glamourer is not available.";
        if (!HasPlayer())
            return "No character to equip on.";
        if (!_items.TryResolve(name, out var item))
            return $"\"{name}\" is not an equippable item.";

        var code = _glamourer.SetItem(VitrineConstants.LocalPlayerIndex, item.Slot, item.ItemId);
        return code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone
            ? $"Equipped {item.Name} ({item.Slot})."
            : $"Glamourer returned {code} for {item.Name}.";
    }

    /// <summary> Equips one affected item per slot so the whole look shows. </summary>
    public string TryOnFullSet(ModInformation mod, bool quiet = false)
    {
        if (!_glamourer.Available)
            return quiet ? "Glamourer not loaded: gear not equipped." : "Glamourer is not available.";
        if (!HasPlayer())
            return quiet ? string.Empty : "No character to equip on.";

        var plan = FullSetPlanner.Plan(mod.AffectedItems, _items);
        if (plan.Items.Count == 0)
            return quiet && plan.Unresolved.Count == 0 ? string.Empty : "No equippable items found in this mod.";

        var equipped = 0;
        GlamourerApiEc? lastError = null;
        foreach (var item in plan.Items)
        {
            var code = _glamourer.SetItem(VitrineConstants.LocalPlayerIndex, item.Slot, item.ItemId);
            if (code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone)
                equipped++;
            else
                lastError = code;
        }

        var sb = new StringBuilder();
        sb.Append($"Equipped {equipped} of {plan.Items.Count} slot(s)");
        if (plan.Alternatives.Count > 0)
            sb.Append($", {plan.Alternatives.Count} alternative(s) skipped");
        if (plan.Unresolved.Count > 0)
            sb.Append($", {plan.Unresolved.Count} name(s) not found as items");
        if (lastError is { } err)
            sb.Append($" (Glamourer: {err})");
        sb.Append('.');
        return sb.ToString();
    }

    /// <summary> Plays the first replaced emote of the mod, if any. </summary>
    public string PlayEmotes(ModInformation mod)
    {
        foreach (var item in mod.AffectedItems.Where(i => i.Category == VitrineCategory.Emote))
        {
            if (_emotes.TryResolve(item.Name, out var id))
                return _emotes.Play(id, item.Name);
        }

        return string.Empty;
    }

    /// <summary> Plays one replaced emote by name. </summary>
    public string PlayEmote(string name)
        => _emotes.Play(name);

    /// <summary> Undoes try-ons: Glamourer reverts the player to whatever its automation would show, then Lamina re-applies when the setting is on. </summary>
    public string RevertTryOn()
    {
        if (!_glamourer.Available)
            return "Glamourer is not available.";
        if (!HasPlayer())
            return "No character to revert.";

        var code = _glamourer.RevertToAutomation(VitrineConstants.LocalPlayerIndex);
        return _handBack.Append(code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone
            ? "Reverted to Glamourer automation"
            : $"Glamourer returned {code} (a state locked by another plugin cannot be reverted from here)");
    }

    private bool HasPlayer()
    {
        if (!_clientState.IsLoggedIn || !_framework.IsInFrameworkUpdateThread)
            return false;

        try
        {
            return _objects.LocalPlayer is not null;
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: local player lookup failed: {ex.Message}");
            return false;
        }
    }
}
