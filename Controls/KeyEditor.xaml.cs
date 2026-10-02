using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Controls;

/// <summary>Key type, version and bytes. The bytes stay in this control; nothing here writes them to the output.</summary>
public partial class KeyEditor : UserControl
{
    private bool _loading = true;

    public KeyEditor()
    {
        InitializeComponent();
        _loading = false;
        TypeBox.Select(NxpscKeyType.TwoKey3Des);
        FillZero();
    }

    public string Label { get => LabelText.Text; set { LabelText.Text = value; LabelText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible; } }

    public NxpscKeyType KeyType
    {
        get => TypeBox.Selected<NxpscKeyType>();
        set { TypeBox.Select(value); }
    }

    private int Size => NxpscKey.SizeOf(KeyType);

    private void FillZero() => KeyBox.Text = new string('0', Size * 2);

    private void TypeBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || KeyBox is null) return;
        // keep the field a valid length for the type
        var current = KeyBox.Text.Trim();
        KeyBox.Text = current.Length > 0 && current.All(ch => ch == '0') || current.Length == 0
            ? new string('0', Size * 2)
            : (current + new string('0', Size * 2))[..(Size * 2)];
    }

    private void KeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        var digits = KeyBox.Text.Count(Uri.IsHexDigit);
        LengthText.Text = $"{digits / 2} / {Size} bytes";
    }

    private void Zero_Click(object sender, RoutedEventArgs e) => FillZero();

    private void Random_Click(object sender, RoutedEventArgs e) =>
        KeyBox.Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(Size));

    public void SetKey(NxpscKeyType type, string hex, byte version = 0)
    {
        TypeBox.Select(type);
        KeyBox.Text = hex;
        VersionBox.Text = version.ToString("X2");
    }

    /// <summary>The key as typed. The caller disposes it.</summary>
    public NxpscKey Build()
    {
        var bytes = Hex.ParseBytes(KeyBox.Text, string.IsNullOrEmpty(Label) ? "key" : Label, Size);
        var version = Hex.ParseByte(VersionBox.Text, "key version");
        return new NxpscKey(KeyType, bytes, version);
    }
}
