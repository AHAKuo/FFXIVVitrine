using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Vitrine.Lamina;

/// <summary>
/// Subscriber side of Lamina's IPC (docs/IPC.md, "Lamina IPC"). Lamina is optional: every call is a no-op while it is missing.
/// Availability is probed through <c>Lamina.ApiVersion</c>, refreshed on Lamina's Ready / Disposed events and whenever the
/// plugin list changes.
/// </summary>
public sealed class LaminaLink : ILaminaLink, IDisposable
{
    public const string ApiVersionLabel = "Lamina.ApiVersion";
    public const string ReapplyLabel    = "Lamina.Reapply";
    public const string IsEnabledLabel  = "Lamina.IsEnabled";
    public const string ReadyLabel      = "Lamina.Ready";
    public const string DisposedLabel   = "Lamina.Disposed";

    /// <summary> Major version of the Lamina IPC this link understands. </summary>
    public const int RequiredMajor = 1;

    private readonly IPluginLog _log;
    private readonly IDalamudPluginInterface _pi;
    private readonly ICallGateSubscriber<(int Major, int Minor)> _apiVersion;
    private readonly ICallGateSubscriber<object> _reapply;
    private readonly ICallGateSubscriber<bool> _isEnabled;
    private readonly ICallGateSubscriber<object> _ready;
    private readonly ICallGateSubscriber<object> _disposed;
    private readonly IDalamudPluginInterface.ActivePluginsChangedDelegate _onPluginsChanged;
    private bool _available;

    public LaminaLink(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi  = pi;
        _log = log;

        _apiVersion = pi.GetIpcSubscriber<(int Major, int Minor)>(ApiVersionLabel);
        _reapply    = pi.GetIpcSubscriber<object>(ReapplyLabel);
        _isEnabled  = pi.GetIpcSubscriber<bool>(IsEnabledLabel);
        _ready      = pi.GetIpcSubscriber<object>(ReadyLabel);
        _disposed   = pi.GetIpcSubscriber<object>(DisposedLabel);

        _ready.Subscribe(Probe);
        _disposed.Subscribe(OnLaminaDisposed);
        _onPluginsChanged = _ => Probe();
        pi.ActivePluginsChanged += _onPluginsChanged;

        Probe();
    }

    /// <inheritdoc/>
    public bool Available
        => _available;

    /// <inheritdoc/>
    public (int Major, int Minor)? ApiVersion { get; private set; }

    /// <summary> Raised whenever <see cref="Available"/> flips. </summary>
    public event Action? AvailabilityChanged;

    /// <summary> Re-checks availability; a major version other than <see cref="RequiredMajor"/> counts as unavailable. </summary>
    public void Probe()
    {
        bool now;
        try
        {
            var version = _apiVersion.InvokeFunc();
            ApiVersion = version;
            now        = version.Major == RequiredMajor;
            if (!now)
                _log.Warning($"Vitrine: Lamina IPC {version.Major}.{version.Minor} is not supported (expected major {RequiredMajor}).");
        }
        catch
        {
            ApiVersion = null;
            now        = false;
        }

        if (now == _available)
            return;

        _available = now;
        _log.Information($"Vitrine: Lamina {(now ? $"available (IPC {ApiVersion?.Major}.{ApiVersion?.Minor})" : "unavailable")}.");
        AvailabilityChanged?.Invoke();
    }

    /// <summary> True when Lamina reports its master toggle as on; false when unknown. </summary>
    public bool IsEnabled()
    {
        if (!_available)
            return false;

        try
        {
            return _isEnabled.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: Lamina.IsEnabled failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Reapply()
    {
        if (!_available)
            return false;

        try
        {
            _reapply.InvokeAction();
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: Lamina.Reapply failed: {ex.Message}");
            return false;
        }
    }

    private void OnLaminaDisposed()
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
        _ready.Unsubscribe(Probe);
        _disposed.Unsubscribe(OnLaminaDisposed);
    }
}
