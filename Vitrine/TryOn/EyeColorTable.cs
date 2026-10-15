using Dalamud.Plugin.Services;

namespace Vitrine.TryOn;

/// <summary>
/// The game's 192 eye colours (shared by every clan and gender), read once from chara/xls/charamake/human.cmp. Each entry
/// is the RGBA the game stores (R in the low byte), which is also ImGui's packed colour layout. Empty when the file is
/// missing; the picker then shows numbers only.
/// </summary>
public sealed class EyeColorTable
{
    /// <summary> Number of eye colour values (0..191). </summary>
    public const int Count = 192;

    // Layout from Penumbra.GameData's CmpData: a Parameters block, then an identical Interface block, each 2304 colours of
    // 4 bytes; the eye colours are the first 192 colours of the Interface block.
    private const int ColorBlock = 2304;
    private const int EyesOffset = ColorBlock;

    private readonly IDataManager _data;
    private readonly IPluginLog _log;
    private uint[]? _colors;
    private bool _tried;

    public EyeColorTable(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary> RGBA per value (alpha forced opaque), or empty when the table could not be read. </summary>
    public IReadOnlyList<uint> Colors
    {
        get
        {
            if (!_tried)
                Load();
            return _colors ?? Array.Empty<uint>();
        }
    }

    private void Load()
    {
        _tried = true;
        try
        {
            var file = _data.GetFile("chara/xls/charamake/human.cmp");
            if (file is null || file.Data.Length < (EyesOffset + Count) * 4)
            {
                _log.Warning("Vitrine: human.cmp is missing or too short; the eye colour picker shows numbers only.");
                return;
            }

            var colors = new uint[Count];
            for (var i = 0; i < Count; i++)
                colors[i] = BitConverter.ToUInt32(file.Data, (EyesOffset + i) * 4) | 0xFF000000u;
            _colors = colors;
        }
        catch (Exception ex)
        {
            _log.Warning($"Vitrine: could not read human.cmp: {ex.Message}");
        }
    }
}
