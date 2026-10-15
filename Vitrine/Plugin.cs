using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Vitrine.Ipc;

namespace Vitrine;

/// <summary> Dalamud entry point. Builds the module, registers /vit and the IPC provider. </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Command = "/vit";

    private readonly VitrineModule _module;
    private readonly VitrineIpc _ipc;

    /// <summary> Plugin version for the About tab. </summary>
    public static string Version { get; private set; } = "0.0.0";

    public Plugin()
    {
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        try
        {
            _module = new VitrineModule(PluginInterface, Log, Framework, TextureProvider, DataManager, ClientState, ObjectTable, Condition,
                PluginInterface.GetPluginConfigDirectory());
            _ipc = new VitrineIpc(PluginInterface, _module, Log);
        }
        catch
        {
            _module?.Dispose();
            throw;
        }

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Vitrine. '/vit rescan' rescans your mods, '/vit help' lists commands.",
        });

        PluginInterface.UiBuilder.Draw         += _module.Draw;
        PluginInterface.UiBuilder.OpenMainUi   += OpenWindow;
        PluginInterface.UiBuilder.OpenConfigUi += OpenWindow;

        Log.Information($"Vitrine {Version} loaded: Penumbra {(_module.Reader.Available ? "ok" : "missing")}, Glamourer {(_module.Glamourer.Available ? "ok" : "missing")}.");
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                _module.ToggleWindow();
                break;
            case "rescan":
                _module.Refresh(force: false);
                Log.Information("Vitrine: rescanning changed mods.");
                break;
            case "rescan all":
                _module.Refresh(force: true);
                Log.Information("Vitrine: full rescan.");
                break;
            case "clear":
                Log.Information($"Vitrine: {_module.Preview.ClearAllPreviews()}");
                break;
            default:
                Log.Information("Vitrine commands: /vit (toggle window), /vit rescan, /vit rescan all, /vit clear (remove previews).");
                break;
        }
    }

    private void OpenWindow()
        => _module.SetWindowOpen(true);

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw         -= _module.Draw;
        PluginInterface.UiBuilder.OpenMainUi   -= OpenWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        CommandManager.RemoveHandler(Command);
        _ipc.Dispose();
        _module.Dispose();
    }
}
