using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Controls;

/// <summary>Every field of NxpscSdmSettings.</summary>
public partial class SdmEditor : UserControl
{
    public SdmEditor() => InitializeComponent();

    private static byte Nibble(string text, string field)
    {
        var v = Hex.ParseByte(text, field);
        if (v > 0x0F) throw new FormatException($"{field}: a key nibble is 0 to F.");
        return v;
    }

    public NxpscSdmSettings Build() => new()
    {
        Enabled = Enabled.IsChecked == true,
        UidMirror = UidMirror.IsChecked == true,
        CounterMirror = CounterMirror.IsChecked == true,
        ReadCounterLimit = CounterLimit.IsChecked == true,
        EncFileData = EncData.IsChecked == true,
        MetaReadKey = Nibble(MetaKey.Text, "meta read key"),
        FileReadKey = Nibble(FileKey.Text, "file read key"),
        CounterRetKey = Nibble(CounterKey.Text, "counter ret key"),
        UidOffset = Hex.ParseUInt(UidOff.Text, "UID offset"),
        CounterOffset = Hex.ParseUInt(CtrOff.Text, "counter offset"),
        PiccDataOffset = Hex.ParseUInt(PiccOff.Text, "PICC data offset"),
        MacInputOffset = Hex.ParseUInt(MacInOff.Text, "MAC input offset"),
        EncOffset = Hex.ParseUInt(EncOff.Text, "enc offset"),
        EncLength = Hex.ParseUInt(EncLen.Text, "enc length"),
        MacOffset = Hex.ParseUInt(MacOff.Text, "MAC offset"),
        ReadCounterLimitValue = Hex.ParseUInt(LimitVal.Text, "counter limit"),
    };
}
