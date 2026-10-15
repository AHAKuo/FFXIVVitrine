using Newtonsoft.Json.Linq;

namespace Vitrine.Glamourer;

/// <summary>
/// Builds the state JObject Glamourer's ApplyState accepts for a customization try-on: FileVersion 1 (the design-base
/// layout the state loader reads; the on-disk design version 3 is rejected as "no valid Version"), only the given
/// customize values applied. Identity fields (race, clan, gender, body type) are copied from the actor's current state
/// with Apply = false so Glamourer validates hair / face numbers against the right clan instead of its zero defaults.
/// Pure logic, unit tested.
/// </summary>
public static class GlamourerStateBuilder
{
    /// <summary> The design-base file version Glamourer's state loader accepts. </summary>
    public const int FileVersion = 1;

    private static readonly string[] IdentityKeys = { "Race", "Clan", "Gender", "BodyType" };

    /// <summary> Builds a customization-only state. </summary>
    /// <param name="values"> Glamourer customize key → value to apply ("Hairstyle" → 194, "EyeColorRight" → 65). </param>
    /// <param name="current"> The actor's current state (GetState), or null when unknown. </param>
    public static JObject Customize(IReadOnlyDictionary<string, int> values, JObject? current)
    {
        var customize = new JObject();
        var baseline  = current?["Customize"] as JObject;
        if (baseline?["ModelId"] is { Type: JTokenType.Integer } model)
            customize["ModelId"] = model.Value<uint>();

        foreach (var key in IdentityKeys)
        {
            if (values.ContainsKey(key))
                continue;
            if (baseline?[key]?["Value"] is { Type: JTokenType.Integer } value)
                customize[key] = new JObject { ["Value"] = value.Value<int>(), ["Apply"] = false };
        }

        foreach (var (key, value) in values)
            customize[key] = new JObject { ["Value"] = value, ["Apply"] = true };

        return new JObject
        {
            ["FileVersion"] = FileVersion,
            ["Equipment"]   = new JObject(),
            ["Bonus"]       = new JObject(),
            ["Customize"]   = customize,
            ["Parameters"]  = new JObject(),
            ["Materials"]   = new JObject(),
        };
    }
}
