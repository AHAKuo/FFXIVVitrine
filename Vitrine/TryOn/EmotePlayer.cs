using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Vitrine.TryOn;

/// <summary>
/// Plays the emote a mod replaces so animation / emote mods can be previewed. Resolves emote names through the Emote sheet
/// (Penumbra reports "Emote: Name" or the bare name), checks the emote is unlocked and that the player is free to emote.
/// </summary>
public sealed class EmotePlayer
{
    private static readonly string[] Prefixes = { "Emote: ", "Emote:", "/" };

    private readonly IDataManager _data;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;
    private Dictionary<string, ushort>? _byName;

    public EmotePlayer(IDataManager data, ICondition condition, IClientState clientState, IPluginLog log)
    {
        _data        = data;
        _condition   = condition;
        _clientState = clientState;
        _log         = log;
    }

    /// <summary> Finds the emote row for an affected-item name. </summary>
    public bool TryResolve(string name, out ushort emoteId)
    {
        emoteId = 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var index = _byName ??= Build();
        var key   = name.Trim();
        foreach (var prefix in Prefixes)
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                key = key[prefix.Length..].Trim();

        return index.TryGetValue(key, out emoteId) || index.TryGetValue("/" + key, out emoteId);
    }

    /// <summary> True when the player exists and is not busy (duty, cutscene, combat, already emoting is fine: the new emote replaces it). </summary>
    public bool CanPlay
        => _clientState.IsLoggedIn
        && !_condition[ConditionFlag.BoundByDuty]
        && !_condition[ConditionFlag.InCombat]
        && !_condition[ConditionFlag.WatchingCutscene]
        && !_condition[ConditionFlag.OccupiedInCutSceneEvent]
        && !_condition[ConditionFlag.BetweenAreas]
        && !_condition[ConditionFlag.Mounted];

    /// <summary> Plays the emote by name. Returns a message for the UI. </summary>
    public string Play(string name)
    {
        if (!TryResolve(name, out var id))
            return $"\"{name}\" is not a known emote.";
        return Play(id, name);
    }

    /// <summary> Plays the emote by id if it is unlocked and the player is free. Returns a message for the UI. </summary>
    public unsafe string Play(ushort emoteId, string name)
    {
        if (!CanPlay)
            return "Cannot play an emote right now (busy, in a duty or mounted).";

        try
        {
            if (!UIState.Instance()->IsEmoteUnlocked(emoteId))
                return $"Emote \"{name}\" is not unlocked on this character.";

            var agent = AgentEmote.Instance();
            if (agent == null)
                return "Emote agent unavailable.";

            agent->ExecuteEmote(emoteId, null, false, false);
            return $"Playing \"{name}\".";
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: playing emote {emoteId} failed: {ex.Message}");
            return $"Could not play \"{name}\".";
        }
    }

    private Dictionary<string, ushort> Build()
    {
        var index = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var row in _data.GetExcelSheet<Emote>())
            {
                var name = row.Name.ExtractText();
                if (name.Length == 0)
                    continue;

                index.TryAdd(name, (ushort)row.RowId);
                foreach (var command in new[] { row.TextCommand.ValueNullable?.Command.ExtractText(), row.TextCommand.ValueNullable?.ShortCommand.ExtractText() })
                    if (!string.IsNullOrEmpty(command))
                        index.TryAdd(command, (ushort)row.RowId);
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: could not index the Emote sheet: {ex.Message}");
        }

        return index;
    }
}
