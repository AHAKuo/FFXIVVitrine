namespace Vitrine.Lamina;

/// <summary> The part of Lamina Vitrine talks to: ask it to re-assert its design once a preview ends. </summary>
public interface ILaminaLink
{
    /// <summary> True when Lamina answered the version probe. </summary>
    bool Available { get; }

    /// <summary> (Major, Minor) as Lamina reports it, or null when unavailable. </summary>
    (int Major, int Minor)? ApiVersion { get; }

    /// <summary> Asks Lamina to re-evaluate and re-apply its active design. Returns true when the call went through. </summary>
    bool Reapply();
}
