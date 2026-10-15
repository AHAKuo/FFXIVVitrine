namespace Vitrine.Lamina;

/// <summary>
/// Decides whether ending a preview hands the character back to Lamina (asks it to re-apply its design) and phrases the
/// outcome for the status line. Pure logic so it can be unit tested without Dalamud.
/// </summary>
public sealed class LaminaHandBack
{
    private readonly ILaminaLink _lamina;
    private readonly Func<bool> _enabled;

    /// <param name="lamina"> The Lamina link. </param>
    /// <param name="enabled"> Reads the "hand back to Lamina" setting. </param>
    public LaminaHandBack(ILaminaLink lamina, Func<bool> enabled)
    {
        _lamina  = lamina;
        _enabled = enabled;
    }

    /// <summary> Outcome of one hand-back attempt. </summary>
    public enum Outcome
    {
        /// <summary> The setting is off; Lamina was not contacted. </summary>
        Disabled,
        /// <summary> The setting is on but Lamina is not installed or not answering. </summary>
        NotInstalled,
        /// <summary> Lamina accepted the reapply request. </summary>
        Reapplied,
        /// <summary> Lamina is installed but the call failed. </summary>
        Failed,
    }

    /// <summary> Asks Lamina to re-apply when the setting is on and Lamina is available. </summary>
    public Outcome TryHandBack()
    {
        if (!_enabled())
            return Outcome.Disabled;
        if (!_lamina.Available)
            return Outcome.NotInstalled;
        return _lamina.Reapply() ? Outcome.Reapplied : Outcome.Failed;
    }

    /// <summary> Runs <see cref="TryHandBack"/> and appends what happened to <paramref name="message"/> ("Preview ended" → "Preview ended; Lamina re-applied."). </summary>
    public string Append(string message)
        => Describe(message, TryHandBack());

    /// <summary> Phrases an outcome after a base message. </summary>
    public static string Describe(string message, Outcome outcome)
    {
        var head = message.TrimEnd().TrimEnd('.');
        return outcome switch
        {
            Outcome.Reapplied    => $"{head}; Lamina re-applied.",
            Outcome.NotInstalled => $"{head}; Lamina not installed.",
            Outcome.Failed       => $"{head}; Lamina did not answer (use /lam reapply).",
            _                    => $"{head}.",
        };
    }
}
