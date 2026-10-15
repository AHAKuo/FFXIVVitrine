using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Vitrine.Ipc;

/// <summary>
/// Dalamud IPC provider implementing docs/IPC.md. Other plugins (Lamina) read designs as JSON, listen for changes,
/// open the window and trigger previews. Labels are stable; a breaking change bumps <see cref="VitrineConstants.IpcVersion"/>.
/// </summary>
public sealed class VitrineIpc : IDisposable
{
    public const string LabelApiVersion     = "Vitrine.ApiVersion";
    public const string LabelReady          = "Vitrine.Ready";
    public const string LabelDisposed       = "Vitrine.Disposed";
    public const string LabelGetDesigns     = "Vitrine.GetDesigns";
    public const string LabelGetDesign      = "Vitrine.GetDesign";
    public const string LabelDesignsChanged = "Vitrine.DesignsChanged";
    public const string LabelOpenWindow     = "Vitrine.OpenWindow";
    public const string LabelPreviewDesign  = "Vitrine.PreviewDesign";
    public const string LabelApplyDesign    = "Vitrine.ApplyDesign";

    private readonly VitrineModule _module;
    private readonly IPluginLog _log;
    private readonly ICallGateProvider<(int Major, int Minor)> _apiVersion;
    private readonly ICallGateProvider<object> _ready;
    private readonly ICallGateProvider<object> _disposed;
    private readonly ICallGateProvider<string> _getDesigns;
    private readonly ICallGateProvider<Guid, string> _getDesign;
    private readonly ICallGateProvider<object> _designsChanged;
    private readonly ICallGateProvider<bool?, object> _openWindow;
    private readonly ICallGateProvider<Guid, string> _previewDesign;
    private readonly ICallGateProvider<Guid, string> _applyDesign;

    public VitrineIpc(IDalamudPluginInterface pi, VitrineModule module, IPluginLog log)
    {
        _module = module;
        _log    = log;

        _apiVersion     = pi.GetIpcProvider<(int Major, int Minor)>(LabelApiVersion);
        _ready          = pi.GetIpcProvider<object>(LabelReady);
        _disposed       = pi.GetIpcProvider<object>(LabelDisposed);
        _getDesigns     = pi.GetIpcProvider<string>(LabelGetDesigns);
        _getDesign      = pi.GetIpcProvider<Guid, string>(LabelGetDesign);
        _designsChanged = pi.GetIpcProvider<object>(LabelDesignsChanged);
        _openWindow     = pi.GetIpcProvider<bool?, object>(LabelOpenWindow);
        _previewDesign  = pi.GetIpcProvider<Guid, string>(LabelPreviewDesign);
        _applyDesign    = pi.GetIpcProvider<Guid, string>(LabelApplyDesign);

        _apiVersion.RegisterFunc(() => VitrineConstants.IpcVersion);
        _getDesigns.RegisterFunc(GetDesigns);
        _getDesign.RegisterFunc(GetDesign);
        _openWindow.RegisterAction(OpenWindow);
        _previewDesign.RegisterFunc(PreviewDesign);
        _applyDesign.RegisterFunc(ApplyDesign);

        _module.CatalogChanged += OnDesignsChanged;
        _ready.SendMessage();
    }

    private string GetDesigns()
    {
        try
        {
            return DesignJson.Serialize(_module.Available ? _module.Designs : Array.Empty<VitrineDesign>());
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: IPC GetDesigns failed: {ex.Message}");
            return "[]";
        }
    }

    private string GetDesign(Guid id)
    {
        try
        {
            return _module.GetDesign(id) is { } design ? DesignJson.Serialize(design) : string.Empty;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: IPC GetDesign failed: {ex.Message}");
            return string.Empty;
        }
    }

    private void OpenWindow(bool? open)
    {
        try
        {
            _module.SetWindowOpen(open);
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: IPC OpenWindow failed: {ex.Message}");
        }
    }

    private string PreviewDesign(Guid id)
    {
        try
        {
            return _module.PreviewDesign(id);
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: IPC PreviewDesign failed: {ex.Message}");
            return $"Preview failed: {ex.Message}";
        }
    }

    private string ApplyDesign(Guid id)
    {
        try
        {
            return _module.ApplyDesign(id);
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: IPC ApplyDesign failed: {ex.Message}");
            return $"Apply failed: {ex.Message}";
        }
    }

    private void OnDesignsChanged()
    {
        try
        {
            _designsChanged.SendMessage();
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: DesignsChanged subscribers threw: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _module.CatalogChanged -= OnDesignsChanged;
        try
        {
            _disposed.SendMessage();
        }
        catch (Exception ex)
        {
            _log.Debug($"Vitrine: Disposed subscribers threw: {ex.Message}");
        }

        _apiVersion.UnregisterFunc();
        _getDesigns.UnregisterFunc();
        _getDesign.UnregisterFunc();
        _openWindow.UnregisterAction();
        _previewDesign.UnregisterFunc();
        _applyDesign.UnregisterFunc();
    }
}
