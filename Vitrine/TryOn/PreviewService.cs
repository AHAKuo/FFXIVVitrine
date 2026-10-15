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
/// redraws, equips a full set of the mod's affected gear through Glamourer, applies matching hair / face customizations
/// and plays replaced emotes. "Apply" is the deliberate sibling: the same mod enabling plus exactly the design's Apply
/// Action pieces, nothing guessed. Everything runs on the framework thread (called from ImGui draws and IPC).
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
    private VitrineDesign? _previewedDesign;

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

    /// <summary> The design currently previewed on the player, or null. Only one design is previewed at a time. </summary>
    public Guid? PreviewedDesignId
        => _previewedDesign?.Id;

    /// <summary> True when Glamourer is loaded and a logged-in player exists (framework thread only). </summary>
    public bool CanTryOn
        => _glamourer.Available && HasPlayer();

    /// <summary> True when Penumbra is loaded and a player exists. </summary>
    public bool CanPreview
        => _penumbra.Available && HasPlayer();

    /// <summary> True when Glamourer is loaded (designs can be created without a character). </summary>
    public bool GlamourerAvailable
        => _glamourer.Available;

    // ---- preview / apply -----------------------------------------------------------------------------------------

    /// <summary>
    /// Previews a design: a previous preview is ended first (one at a time), then the design's mods are enabled on the
    /// player temporarily, the player is redrawn, the design's pieces (its affected slots) are equipped, fitting hair / face
    /// entries applied and the replaced emote played. Previews are transient: <see cref="EndPreview"/>, another preview,
    /// or closing the window undoes them.
    /// </summary>
    public string PreviewDesign(VitrineDesign design, ModInformation? info)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";
        if (!HasPlayer())
            return "No character to preview on.";

        if (_previewedDesign is { } previous && previous.Id != design.Id)
            EndPreviewCore(previous);

        if (EnableMods(design) is { } error)
            return error;

        _previewedDesign = design;
        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        var message = new StringBuilder("Previewing: mod enabled on you and redrawn.");
        if (info is not null)
        {
            Append(message, EquipPieces(design, info));
            Append(message, TryOnCustomizations(info, quiet: true));
            Append(message, PlayEmotes(info));
        }

        return message.ToString();
    }

    /// <summary> Equips the design's pieces (its affected slots, one piece each). Empty message when there is nothing to equip. </summary>
    private string EquipPieces(VitrineDesign design, ModInformation info)
    {
        if (!_glamourer.Available)
            return "Glamourer not loaded: pieces not equipped.";

        var plan = ApplyPlanner.Plan(design.ApplyAction, info.AffectedItems, _items);
        if (plan.Items.Count == 0)
            return string.Empty;

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

        return $"Equipped {equipped} of {plan.Items.Count} piece(s){(lastError is { } e ? $" (Glamourer: {e})" : string.Empty)}.";
    }

    /// <summary>
    /// Applies the design: enables its mods with the design's settings, redraws, and equips exactly the pieces its Apply
    /// Action names (the full-set plan when it has none). Matching hair / face customizations are applied too; emotes are not played.
    /// </summary>
    public string ApplyDesign(VitrineDesign design, ModInformation? info)
    {
        if (EnableMods(design) is { } error)
            return error;

        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        var message = new StringBuilder("Mod enabled on you and redrawn.");
        if (info is null)
            return message.ToString();

        if (!_glamourer.Available)
        {
            Append(message, "Glamourer not loaded: pieces not equipped.");
            return message.ToString();
        }

        var plan = ApplyPlanner.Plan(design.ApplyAction, info.AffectedItems, _items);
        if (plan.Items.Count > 0)
        {
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

            Append(message, $"Applied {equipped} of {plan.Items.Count} piece(s){(lastError is { } e ? $" (Glamourer: {e})" : string.Empty)}.");
        }
        else if (design.ApplyAction is not null)
        {
            Append(message, "The apply action equips nothing.");
        }

        if (plan.Missing.Count > 0)
            Append(message, $"{plan.Missing.Count} chosen piece(s) the mod no longer offers were skipped.");
        Append(message, TryOnCustomizations(info, quiet: true));
        return message.ToString();
    }

    /// <summary> Writes the design's mods as temporary settings into the player's collection. Null on success, else the message. </summary>
    private string? EnableMods(VitrineDesign design)
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

        return null;
    }

    /// <summary>
    /// Ends a preview as one action: removes the design's mods' preview settings from the player, reverts the gear and
    /// customization try-on (Glamourer automation), redraws, then hands the look back to Lamina when the setting is on.
    /// </summary>
    public string EndPreview(VitrineDesign design)
    {
        if (!_penumbra.Available)
            return "Penumbra is not available.";

        var error = EndPreviewCore(design);
        return _handBack.Append(error is { } e ? $"Preview ended (Penumbra returned {e})" : "Preview ended");
    }

    /// <summary> Removes the design's preview settings and reverts Glamourer; no Lamina hand-back. Returns Penumbra's error, if any. </summary>
    private PenumbraApiEc? EndPreviewCore(VitrineDesign design)
    {
        PenumbraApiEc? error = null;
        foreach (var mod in design.Mods)
        {
            var code = (PenumbraApiEc)_penumbra.RemoveTemporaryModSettingsPlayer(VitrineConstants.LocalPlayerIndex, mod.ModDirectory, VitrineConstants.TemporaryKey);
            if (code is not (PenumbraApiEc.Success or PenumbraApiEc.NothingChanged))
                error = code;
            _previewed.Remove(mod.ModDirectory);
        }

        if (_previewedDesign?.Id == design.Id)
            _previewedDesign = null;
        if (_glamourer.Available && HasPlayer())
            _glamourer.RevertToAutomation(VitrineConstants.LocalPlayerIndex);
        _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        return error;
    }

    /// <summary>
    /// Ends whatever is previewed (window close, unload): every Vitrine preview setting removed, Glamourer reverted,
    /// redraw, Lamina hand-back. Empty message when nothing was previewed.
    /// </summary>
    public string EndAllPreviews()
    {
        if (_previewedDesign is null && _previewed.Count == 0)
            return string.Empty;

        _previewedDesign = null;
        if (_glamourer.Available && HasPlayer())
            _glamourer.RevertToAutomation(VitrineConstants.LocalPlayerIndex);
        return ClearAllPreviews();
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
        _previewedDesign = null;
        if (redraw)
            _penumbra.RedrawObject(VitrineConstants.LocalPlayerIndex);
        return _handBack.Append(code is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged ? "All previews removed" : $"Penumbra returned {code}");
    }

    // ---- gear try-on ---------------------------------------------------------------------------------------------

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

    /// <summary> The pieces a mod offers per equipment slot (for the Apply Action editor). </summary>
    public IReadOnlyList<SlotCandidates> ApplyCandidates(ModInformation mod)
        => ApplyPlanner.Candidates(mod.AffectedItems, _items);

    /// <summary> What an apply of the design would equip. </summary>
    public ApplyPlan PlanApply(VitrineDesign design, ModInformation mod)
        => ApplyPlanner.Plan(design.ApplyAction, mod.AffectedItems, _items);

    // ---- customization try-on ------------------------------------------------------------------------------------

    /// <summary> The player's clan (1..16) and gender (0 male, 1 female) from the character's customize bytes. False without a player. </summary>
    public bool TryGetPlayerIdentity(out int clan, out int gender)
    {
        clan   = 0;
        gender = 0;
        if (!HasPlayer())
            return false;

        try
        {
            if (_objects.LocalPlayer is not { } player)
                return false;
            var bytes = player.Customize;
            if (bytes.Length < 26)
                return false;

            clan   = bytes[4];
            gender = bytes[1] == 0 ? 0 : 1;
            return clan is >= 1 and <= 16;
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: customize bytes unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary> The player's current eye colour values (right, left) for the picker's highlight. False without a player. </summary>
    public bool TryGetPlayerEyeColors(out int right, out int left)
    {
        right = 0;
        left  = 0;
        if (!HasPlayer())
            return false;

        try
        {
            if (_objects.LocalPlayer is not { } player)
                return false;
            var bytes = player.Customize;
            if (bytes.Length < 26)
                return false;

            right = bytes[9];
            left  = bytes[15];
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The customization entries of a mod that fit the player (first one per Glamourer key, so a mod shipping hair 194 and
    /// 195 contributes 194). Empty without a player.
    /// </summary>
    public IReadOnlyList<CustomizationEntry> MatchingCustomizations(ModInformation mod)
    {
        if (!TryGetPlayerIdentity(out var clan, out var gender))
            return Array.Empty<CustomizationEntry>();

        var seen   = new HashSet<string>();
        var result = new List<CustomizationEntry>();
        foreach (var entry in CustomizationParser.ParseAll(mod.AffectedItems))
        {
            if (entry.GlamourerKey is not { } key || !entry.Matches(clan, gender) || !seen.Add(key))
                continue;
            result.Add(entry);
        }

        return result;
    }

    /// <summary> Applies the mod's matching hair / face / tail entries (one per kind) through Glamourer, once, unlocked. </summary>
    public string TryOnCustomizations(ModInformation mod, bool quiet = false)
    {
        if (!_glamourer.Available)
            return quiet ? string.Empty : "Glamourer is not available.";
        if (!HasPlayer())
            return quiet ? string.Empty : "No character to try on.";

        var matching = MatchingCustomizations(mod);
        if (matching.Count == 0)
            return quiet ? string.Empty : "No hair, face, tail or ears entry fits your character.";

        var values = matching.ToDictionary(e => e.GlamourerKey!, e => e.Value);
        var code   = _glamourer.ApplyCustomize(VitrineConstants.LocalPlayerIndex, values);
        return code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone
            ? $"Applied {string.Join(", ", matching.Select(e => e.ShortLabel))}."
            : $"Glamourer returned {code} for {string.Join(", ", matching.Select(e => e.ShortLabel))}.";
    }

    /// <summary> Applies one customization entry (a clicked chip). </summary>
    public string TryOnCustomization(CustomizationEntry entry)
    {
        if (!_glamourer.Available)
            return "Glamourer is not available.";
        if (!TryGetPlayerIdentity(out var clan, out var gender))
            return "No character to try on.";
        if (entry.GlamourerKey is not { } key)
            return $"{entry.ShortLabel} is a texture, not a value Glamourer can set: preview the mod to see it.";
        if (!entry.Matches(clan, gender))
            return $"{entry.ShortLabel} is made for {entry.RaceName} {entry.GenderName}; your character is {CustomizationParser.ClanName(clan)} {(gender == 0 ? "male" : "female")}.";

        var code = _glamourer.ApplyCustomize(VitrineConstants.LocalPlayerIndex, new Dictionary<string, int> { [key] = entry.Value });
        return code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone
            ? $"Applied {entry.ShortLabel}."
            : $"Glamourer returned {code} for {entry.ShortLabel}.";
    }

    /// <summary> Sets the eye colour (0..191) on the right and / or left eye, once, unlocked. </summary>
    public string TryOnEyeColor(int value, bool right, bool left)
    {
        if (!_glamourer.Available)
            return "Glamourer is not available.";
        if (!HasPlayer())
            return "No character to try on.";
        if (!right && !left)
            return "Pick an eye.";

        var values = new Dictionary<string, int>();
        if (right)
            values["EyeColorRight"] = value;
        if (left)
            values["EyeColorLeft"] = value;
        var code = _glamourer.ApplyCustomize(VitrineConstants.LocalPlayerIndex, values);
        var which = right && left ? "both eyes" : right ? "right eye" : "left eye";
        return code is GlamourerApiEc.Success or GlamourerApiEc.NothingDone
            ? $"Eye colour {value} on {which}."
            : $"Glamourer returned {code} for the eye colour.";
    }

    // ---- Glamourer designs ---------------------------------------------------------------------------------------

    /// <summary> True when a Glamourer design made from the design would apply at least one piece or customization. </summary>
    public bool CanMakeGlamourerDesign(VitrineDesign design, ModInformation mod)
        => PlanApply(design, mod).Items.Count > 0 || MatchingCustomizations(mod).Count > 0;

    /// <summary>
    /// Builds a Glamourer design from the Vitrine design (its Apply Action pieces or the full-set plan, matching hair / face
    /// values, and the mod association with the design's settings) and creates it through Glamourer.
    /// </summary>
    public string MakeGlamourerDesign(VitrineDesign design, ModInformation mod, out Guid created)
    {
        created = Guid.Empty;
        if (!_glamourer.Available)
            return "Glamourer is not available.";

        var plan      = PlanApply(design, mod);
        var customize = MatchingCustomizations(mod).ToDictionary(e => e.GlamourerKey!, e => e.Value);
        if (plan.Items.Count == 0 && customize.Count == 0)
            return "Nothing to put in a design: this mod has no equippable pieces or matching customization.";

        var state = design.Mods.Count > 0 ? design.Mods[0] : null;
        var name  = design.IsAuto ? $"{mod.Name} · Baseline" : $"{mod.Name} · {design.Name}";
        var json  = GlamourerDesignBuilder.Build(name, $"Created by Vitrine from the mod \"{mod.Name}\".", plan.Items, customize, state, mod.AvailableSettings);
        var code  = _glamourer.AddDesign(json, name, out created);
        if (code != GlamourerApiEc.Success)
            return $"Glamourer returned {code} while creating the design.";

        var parts = new List<string>();
        if (plan.Items.Count > 0)
            parts.Add($"{plan.Items.Count} piece(s)");
        if (customize.Count > 0)
            parts.Add(string.Join(", ", customize.Keys));
        return $"Glamourer design \"{name}\" created with {string.Join(" and ", parts)} and the mod attached.";
    }

    /// <summary> Opens a design in Glamourer's window. </summary>
    public string OpenGlamourerDesign(Guid id)
        => _glamourer.OpenDesign(id) ? "Opened in Glamourer." : "Glamourer is not available.";

    // ---- emotes --------------------------------------------------------------------------------------------------

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

    private static void Append(StringBuilder message, string part)
    {
        if (part.Length > 0)
            message.Append(' ').Append(part);
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
