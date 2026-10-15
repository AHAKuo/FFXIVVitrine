using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json.Linq;

namespace Vitrine.Glamourer;

/// <summary>
/// The part of Glamourer Vitrine talks to: equip one item on the local player (try-on), apply customization values
/// (hair / face / eye colour try-on), revert to automation, and create / open designs.
/// Glamourer is optional; every call is a no-op with a failure code while it is missing.
/// </summary>
public sealed class GlamourerLink : IDisposable
{
    private readonly IPluginLog _log;
    private readonly ApiVersion _apiVersion;
    private readonly SetItem _setItem;
    private readonly RevertToAutomation _revertToAutomation;
    private readonly GetState _getState;
    private readonly ApplyState _applyState;
    private readonly AddDesign _addDesign;
    private readonly OpenDesign _openDesign;
    private readonly IDisposable _initialized;
    private readonly IDisposable _disposed;
    private readonly IDalamudPluginInterface _pi;
    private readonly IDalamudPluginInterface.ActivePluginsChangedDelegate _onPluginsChanged;
    private bool _available;

    public GlamourerLink(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi  = pi;
        _log = log;

        _apiVersion         = new ApiVersion(pi);
        _setItem            = new SetItem(pi);
        _revertToAutomation = new RevertToAutomation(pi);
        _getState           = new GetState(pi);
        _applyState         = new ApplyState(pi);
        _addDesign          = new AddDesign(pi);
        _openDesign         = new OpenDesign(pi);
        _initialized        = Initialized.Subscriber(pi, Probe);
        _disposed           = Disposed.Subscriber(pi, OnGlamourerDisposed);
        _onPluginsChanged   = _ => Probe();
        pi.ActivePluginsChanged += _onPluginsChanged;

        Probe();
    }

    /// <summary> True when Glamourer answered the version probe. </summary>
    public bool Available
        => _available;

    /// <summary> (Major, Minor) as Glamourer reports it, or null when unavailable. </summary>
    public (int Major, int Minor)? ApiVersion { get; private set; }

    /// <summary> Raised whenever <see cref="Available"/> flips. </summary>
    public event Action? AvailabilityChanged;

    /// <summary>
    /// Re-checks availability. Glamourer reports its own (Major, Minor) behind the ".V2" labels; the labels answering at all
    /// is the compatibility proof, so no major gate is applied.
    /// </summary>
    public void Probe()
    {
        bool now;
        try
        {
            var (major, minor) = _apiVersion.Invoke();
            ApiVersion = (major, minor);
            now        = true;
        }
        catch
        {
            ApiVersion = null;
            now        = false;
        }

        if (now == _available)
            return;

        _available = now;
        _log.Information($"Vitrine: Glamourer {(now ? $"available (API {ApiVersion?.Major}.{ApiVersion?.Minor})" : "unavailable")}.");
        AvailabilityChanged?.Invoke();
    }

    /// <summary>
    /// Two stain slots, 0 = no dye. A List (not byte[]): Dalamud serializes IPC arguments as JSON and a byte[] becomes a
    /// base64 string Glamourer cannot read back as a byte list; a List serializes as a JSON array.
    /// </summary>
    private static readonly List<byte> NoStains = new() { 0, 0 };

    /// <summary>
    /// Equips <paramref name="itemId"/> into <paramref name="slot"/> on the actor. Flags = Once: the item shows immediately but
    /// is not written into persistent state, so a revert or another plugin's apply undoes it; key 0: Vitrine never locks.
    /// When Lamina holds the state locked, Glamourer answers InvalidKey; the call is then retried with Lamina's well-known
    /// key (<see cref="VitrineConstants.LaminaGlamourerKey"/>) so try-ons keep working alongside Lamina.
    /// </summary>
    public GlamourerApiEc SetItem(int objectIndex, ApiEquipSlot slot, ulong itemId)
    {
        if (!_available)
            return GlamourerApiEc.UnknownError;

        try
        {
            // Glamourer reads both stain slots from the list it is given (an empty list throws inside the call); 0 = no dye.
            var code = _setItem.Invoke(objectIndex, slot, itemId, NoStains, 0, ApplyFlag.Once);
            if (code == GlamourerApiEc.InvalidKey)
                code = _setItem.Invoke(objectIndex, slot, itemId, NoStains, VitrineConstants.LaminaGlamourerKey, ApplyFlag.Once);
            return code;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Glamourer SetItem failed: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}");
            return GlamourerApiEc.UnknownError;
        }
    }

    /// <summary> The actor's current Glamourer state (design-base layout), or null. Retries with Lamina's key when Lamina holds the lock. </summary>
    public JObject? GetState(int objectIndex)
    {
        if (!_available)
            return null;

        try
        {
            var (code, state) = _getState.Invoke(objectIndex);
            if (code == GlamourerApiEc.InvalidKey)
                (code, state) = _getState.Invoke(objectIndex, VitrineConstants.LaminaGlamourerKey);
            return code == GlamourerApiEc.Success ? state : null;
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: Glamourer GetState failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies customize values only (hairstyle, face, tail shape, eye colours) with flags Customization | Once: shown
    /// immediately, not persisted, no lock, equipment untouched. Identity fields are copied from the current state so
    /// Glamourer validates the numbers for the right clan. Retries with Lamina's key on InvalidKey like <see cref="SetItem"/>.
    /// </summary>
    public GlamourerApiEc ApplyCustomize(int objectIndex, IReadOnlyDictionary<string, int> values)
    {
        if (!_available)
            return GlamourerApiEc.UnknownError;

        try
        {
            var state = GlamourerStateBuilder.Customize(values, GetState(objectIndex));
            const ApplyFlag flags = ApplyFlag.Customization | ApplyFlag.Once;
            var code = _applyState.Invoke(state, objectIndex, 0, flags);
            if (code == GlamourerApiEc.InvalidKey)
                code = _applyState.Invoke(state, objectIndex, VitrineConstants.LaminaGlamourerKey, flags);
            return code;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Glamourer ApplyState failed: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}");
            return GlamourerApiEc.UnknownError;
        }
    }

    /// <summary> Creates a design from its JSON (FileVersion 3 layout) and writes it to Glamourer's disk. </summary>
    public GlamourerApiEc AddDesign(JObject design, string name, out Guid id)
    {
        id = Guid.Empty;
        if (!_available)
            return GlamourerApiEc.UnknownError;

        try
        {
            return _addDesign.Invoke(design.ToString(Newtonsoft.Json.Formatting.None), name, out id);
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Glamourer AddDesign failed: {(ex.InnerException ?? ex).Message}");
            return GlamourerApiEc.UnknownError;
        }
    }

    /// <summary> Opens Glamourer's main window on a design. </summary>
    public bool OpenDesign(Guid id)
    {
        if (!_available)
            return false;

        try
        {
            _openDesign.Invoke(id);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Glamourer OpenDesign failed: {ex.Message}");
            return false;
        }
    }

    /// <summary> Gives the actor back to whatever Glamourer automation would show (key 0: only unlocked states revert). </summary>
    public GlamourerApiEc RevertToAutomation(int objectIndex)
    {
        if (!_available)
            return GlamourerApiEc.UnknownError;

        try
        {
            return _revertToAutomation.Invoke(objectIndex);
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Glamourer RevertToAutomation failed: {ex.Message}");
            return GlamourerApiEc.UnknownError;
        }
    }

    private void OnGlamourerDisposed()
    {
        if (!_available)
            return;

        _available = false;
        ApiVersion = null;
        AvailabilityChanged?.Invoke();
    }

    public void Dispose()
    {
        _pi.ActivePluginsChanged -= _onPluginsChanged;
        _initialized.Dispose();
        _disposed.Dispose();
    }
}
