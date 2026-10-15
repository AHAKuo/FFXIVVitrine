using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Vitrine.Ipc;

/// <summary> JSON shape of designs as shared over IPC (docs/IPC.md): camelCase, enums as strings, nulls kept. </summary>
public static class DesignJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver  = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true) },
        Converters        = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Include,
        Formatting        = Formatting.None,
    };

    /// <summary> Serialises one design. </summary>
    public static string Serialize(VitrineDesign design)
        => JsonConvert.SerializeObject(design, Settings);

    /// <summary> Serialises a list of designs as a JSON array. </summary>
    public static string Serialize(IEnumerable<VitrineDesign> designs)
        => JsonConvert.SerializeObject(designs.ToList(), Settings);

    /// <summary> Parses a design back (used by tests and by consumers that share this file). Null on malformed input. </summary>
    public static VitrineDesign? Deserialize(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<VitrineDesign>(json, Settings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary> Parses a design array. Empty on malformed input. </summary>
    public static List<VitrineDesign> DeserializeList(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<List<VitrineDesign>>(json, Settings) ?? new List<VitrineDesign>();
        }
        catch (JsonException)
        {
            return new List<VitrineDesign>();
        }
    }
}
