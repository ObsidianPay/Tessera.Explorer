using System.Windows.Controls;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Controls;

public partial class AccessEditor : UserControl
{
    private static readonly string[] Items =
        [.. Enumerable.Range(0, 14).Select(i => $"{i:X}  key {i}"), "E  free", "F  denied"];

    public AccessEditor()
    {
        InitializeComponent();
        foreach (var box in new[] { ReadBox, WriteBox, RwBox, ChangeBox })
        {
            box.ItemsSource = Items;
            box.SelectedIndex = 0;
        }
        Changed(null!, null!);
    }

    public NxpscAccessRights Value
    {
        get => new((byte)ReadBox.SelectedIndex, (byte)WriteBox.SelectedIndex, (byte)RwBox.SelectedIndex, (byte)ChangeBox.SelectedIndex);
        set
        {
            ReadBox.SelectedIndex = value.Read & 0xF;
            WriteBox.SelectedIndex = value.Write & 0xF;
            RwBox.SelectedIndex = value.ReadWrite & 0xF;
            ChangeBox.SelectedIndex = value.Change & 0xF;
        }
    }

    private void Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Packed is null || ChangeBox is null) return;
        var v = Value;
        Packed.Text = $"packed 0x{((v.Read & 0xF) << 12 | (v.Write & 0xF) << 8 | (v.ReadWrite & 0xF) << 4 | (v.Change & 0xF)):X4}";
    }
}
