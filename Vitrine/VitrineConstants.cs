namespace Vitrine;

/// <summary> Identifiers Vitrine uses towards other plugins. </summary>
public static class VitrineConstants
{
    /// <summary> Source string shown in Penumbra for Vitrine's temporary settings. </summary>
    public const string TemporarySource = "Vitrine";

    /// <summary>
    /// Penumbra temporary-settings key. Negative = identification lock: the user can still edit the setting, but only
    /// Vitrine can remove all of its own settings in one call with the same key. ASCII "VITR" negated.
    /// </summary>
    public const int TemporaryKey = -0x56495452;

    /// <summary> Index of the local player in the game object table. </summary>
    public const int LocalPlayerIndex = 0;

    /// <summary> Version of the IPC contract in docs/IPC.md. </summary>
    public static readonly (int Major, int Minor) IpcVersion = (1, 2);

    /// <summary> Glamourer lock key Lamina uses ("LAMI"). Only used to retry a try-on when Lamina holds the state locked. </summary>
    public const uint LaminaGlamourerKey = 0x4C414D49;
}
