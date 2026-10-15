using System.Text.RegularExpressions;

namespace Vitrine.TryOn;

/// <summary> What part of the character a customization entry replaces. </summary>
public enum CustomizationKind
{
    Hair,
    Face,
    Tail,
    Ears,
    Body,
}

/// <summary>
/// The races the game has models for. Penumbra names customizations by these (an "Au Ra Female" hair serves Raen and
/// Xaela alike); only Hyur splits into two model races.
/// </summary>
public enum ModelRace
{
    Midlander,
    Highlander,
    Elezen,
    Lalafell,
    Miqote,
    Roegadyn,
    AuRa,
    Hrothgar,
    Viera,
}

/// <summary>
/// One parsed customization affected item ("Customization: Midlander Female Hair 194"). <see cref="Value"/> is the game's
/// number for that part (hairstyle 194, face 1, tail shape 2).
/// </summary>
public sealed record CustomizationEntry(string Name, ModelRace Race, int Gender, CustomizationKind Kind, string? Marker, int Value)
{
    /// <summary> "Au Ra", "Miqo'te", "Midlander". </summary>
    public string RaceName
        => CustomizationParser.RaceName(Race);

    /// <summary> "male" / "female". </summary>
    public string GenderName
        => Gender == 0 ? "male" : "female";

    /// <summary> True for per-face iris / eye textures ("Face (Iris) 1"): the try-on sets the face, the eye colour is picked separately. </summary>
    public bool IsEyes
        => Marker is not null && (Marker.Equals("Iris", StringComparison.OrdinalIgnoreCase) || Marker.Equals("Eyes", StringComparison.OrdinalIgnoreCase));

    /// <summary> The Glamourer customize key the try-on sets, or null when the entry cannot be shown by changing a value (body textures). </summary>
    public string? GlamourerKey
        => Kind switch
        {
            CustomizationKind.Hair => "Hairstyle",
            CustomizationKind.Face => "Face",
            CustomizationKind.Tail => "TailShape",
            CustomizationKind.Ears => "TailShape",
            _                      => null,
        };

    /// <summary> True when the entry is made for a character of this clan (1..16) and gender (0 male, 1 female). </summary>
    public bool Matches(int clan, int gender)
        => CustomizationParser.RaceOfClan(clan) == Race && Gender == gender;

    /// <summary> Short chip text: "Hair 194" / "Eyes · Face 1" / "Tail 3". </summary>
    public string ShortLabel
        => Kind switch
        {
            CustomizationKind.Face when IsEyes => $"Eyes · Face {Value}",
            CustomizationKind.Face when Marker is { Length: > 0 } => $"Face {Value} ({Marker})",
            CustomizationKind.Body => Value > 0 ? $"Body {Value}" : "Body",
            _ => $"{Kind} {Value}",
        };
}

/// <summary>
/// Parses Penumbra's customization changed-item names into race, gender, kind and number. Penumbra names them
/// "Customization: &lt;Race&gt; &lt;Gender&gt; &lt;Part&gt; [(Marker)] &lt;N&gt;" with the model race ("Au Ra Female
/// Face (Iris) 1"); older names carry race and clan ("Miqo'te Keeper of the Moon Female Hair 108"). Pure logic, unit tested.
/// </summary>
public static partial class CustomizationParser
{
    [GeneratedRegex(
        @"^\s*(?:Customization\s*:\s*)?" +
        @"(?<race>Hyur|Elezen|Lalafell|Miqo'?te|Roegadyn|Au\s*Ra|Hrothgar|Viera|Midlander|Highlander|Wildwood|Duskwight|Plainsfolk|Dunesfolk|Seekers?(?:\s+of\s+the\s+Sun)?|Keepers?(?:\s+of\s+the\s+Moon)?|Sea\s*Wolf|Hellsguard|Raen|Xaela|Helion|Lost|Rava|Veena)" +
        @"(?:\s+(?<clan>Midlander|Highlander|Wildwood|Duskwight|Plainsfolk|Dunesfolk|Seekers?(?:\s+of\s+the\s+Sun)?|Keepers?(?:\s+of\s+the\s+Moon)?|Sea\s*Wolf|Hellsguard|Raen|Xaela|Helion|Lost|Rava|Veena))?\s+" +
        @"(?<gender>Male|Female)\s+(?<kind>Hairstyle|Hair|Face|Tail|Ears?|Body)\s*(?:\((?<marker>[^)]*)\))?\s*(?<n>\d+)?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex EntryRegex();

    /// <summary> Parses one affected-item name. False when the name is not a customization entry. </summary>
    public static bool TryParse(string name, out CustomizationEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var m = EntryRegex().Match(name);
        if (!m.Success)
            return false;

        // The clan (when given) is more specific than the race word before it: "Hyur Midlander" → Midlander.
        var race = m.Groups["clan"].Success ? RaceOf(m.Groups["clan"].Value) : RaceOf(m.Groups["race"].Value);
        if (race is null)
            return false;

        var gender = m.Groups["gender"].Value.Equals("Female", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var marker = m.Groups["marker"].Success ? m.Groups["marker"].Value.Trim() : null;
        var kind   = KindOf(m.Groups["kind"].Value, marker);
        var n      = 0;
        var hasN   = m.Groups["n"].Success && int.TryParse(m.Groups["n"].Value, out n);
        if (!hasN && kind != CustomizationKind.Body)
            return false;

        entry = new CustomizationEntry(name.Trim(), race.Value, gender, kind, string.IsNullOrEmpty(marker) ? null : marker, hasN ? n : 0);
        return true;
    }

    /// <summary> Parses every customization entry of a mod, in order. </summary>
    public static List<CustomizationEntry> ParseAll(IEnumerable<AffectedItem> items)
    {
        var result = new List<CustomizationEntry>();
        foreach (var item in items)
            if (TryParse(item.Name, out var entry))
                result.Add(entry);
        return result;
    }

    /// <summary> The model race of a clan id (1..16, as in the character's customize byte 4). Null outside that range. </summary>
    public static ModelRace? RaceOfClan(int clan)
        => clan switch
        {
            1       => ModelRace.Midlander,
            2       => ModelRace.Highlander,
            3 or 4  => ModelRace.Elezen,
            5 or 6  => ModelRace.Lalafell,
            7 or 8  => ModelRace.Miqote,
            9 or 10 => ModelRace.Roegadyn,
            11 or 12 => ModelRace.AuRa,
            13 or 14 => ModelRace.Hrothgar,
            15 or 16 => ModelRace.Viera,
            _       => null,
        };

    /// <summary> Display name of a model race. </summary>
    public static string RaceName(ModelRace race)
        => race switch
        {
            ModelRace.Miqote => "Miqo'te",
            ModelRace.AuRa   => "Au Ra",
            _                => race.ToString(),
        };

    /// <summary> Clan name by id (1..16), or "clan N". </summary>
    public static string ClanName(int clan)
        => clan switch
        {
            1  => "Midlander",
            2  => "Highlander",
            3  => "Wildwood",
            4  => "Duskwight",
            5  => "Plainsfolk",
            6  => "Dunesfolk",
            7  => "Seeker of the Sun",
            8  => "Keeper of the Moon",
            9  => "Sea Wolf",
            10 => "Hellsguard",
            11 => "Raen",
            12 => "Xaela",
            13 => "Helion",
            14 => "Lost",
            15 => "Rava",
            16 => "Veena",
            _  => $"clan {clan}",
        };

    private static ModelRace? RaceOf(string text)
    {
        var t = Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();
        return t switch
        {
            "midlander" => ModelRace.Midlander,
            "highlander" => ModelRace.Highlander,
            "hyur" => ModelRace.Midlander, // bare "Hyur" without a clan: the common model
            "elezen" or "wildwood" or "duskwight" => ModelRace.Elezen,
            "lalafell" or "plainsfolk" or "dunesfolk" => ModelRace.Lalafell,
            "miqote" or "miqo'te" or "seeker" or "seekers" or "seeker of the sun" or "seekers of the sun"
                or "keeper" or "keepers" or "keeper of the moon" or "keepers of the moon" => ModelRace.Miqote,
            "roegadyn" or "sea wolf" or "seawolf" or "hellsguard" => ModelRace.Roegadyn,
            "au ra" or "aura" or "raen" or "xaela" => ModelRace.AuRa,
            "hrothgar" or "helion" or "lost" => ModelRace.Hrothgar,
            "viera" or "rava" or "veena" => ModelRace.Viera,
            _ => null,
        };
    }

    /// <summary> A "(Tail)" / "(Ears)" marker on a face or body entry names the part; any other marker keeps the plain kind. </summary>
    private static CustomizationKind KindOf(string kind, string? marker)
    {
        if (marker is not null)
        {
            if (marker.Equals("Tail", StringComparison.OrdinalIgnoreCase))
                return CustomizationKind.Tail;
            if (marker.Equals("Ears", StringComparison.OrdinalIgnoreCase) || marker.Equals("Ear", StringComparison.OrdinalIgnoreCase))
                return CustomizationKind.Ears;
        }

        return kind.ToLowerInvariant() switch
        {
            "hair" or "hairstyle" => CustomizationKind.Hair,
            "face"                => CustomizationKind.Face,
            "tail"                => CustomizationKind.Tail,
            "ear" or "ears"       => CustomizationKind.Ears,
            _                     => CustomizationKind.Body,
        };
    }
}
