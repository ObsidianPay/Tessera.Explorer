using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class KeysPage : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;
    private bool _syncing;

    public KeysPage()
    {
        InitializeComponent();
        AuthChannel.Select(NxpscChannel.Auto);
        KsAccess.ItemsSource = Enumerable.Range(0, 16).Select(i => i switch { 0 => "0  master key", 0xE => "E  same key", 0xF => "F  frozen", _ => $"{i:X}  key {i}" }).ToList();
        KsAccess.SelectedIndex = 0;
        AuthKey.SetKey(NxpscKeyType.TwoKey3Des, new string('0', 32));
        OldKey.SetKey(NxpscKeyType.Aes128, new string('0', 32));
        NewKey.SetKey(NxpscKeyType.Aes128, new string('0', 32), 1);
        Ks_Changed(null!, null!);
    }

    // ---- select ----------------------------------------------------------------------------------

    private void Select_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(SelectAid.Text);
        await _s.Run("SelectApplication", c => c.SelectApplication(aid), $"AID {aid:X6}");
    });

    private void SelectPicc_Click(object sender, RoutedEventArgs e)
    {
        SelectAid.Text = "000000";
        Select_Click(sender, e);
    }

    // ---- authenticate ------------------------------------------------------------------------------

    private void Auth_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var keyNo = Hex.ParseByte(AuthKeyNo.Text, "key no");
        var channel = AuthChannel.Selected<NxpscChannel>();
        using var key = AuthKey.Build();
        await _s.Run("Authenticate", c => c.Authenticate(keyNo, key, channel), $"key {keyNo:X2}, {key.Type}, channel {channel}");
    });

    private void AuthNonFirst_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var keyNo = Hex.ParseByte(AuthKeyNo.Text, "key no");
        using var key = AuthKey.Build();
        await _s.Run("AuthenticateNonFirst", c => c.AuthenticateNonFirst(keyNo, key), $"key {keyNo:X2}, {key.Type}");
    });

    private void FactoryPicc_Click(object sender, RoutedEventArgs e)
    {
        AuthKeyNo.Text = "00";
        AuthKey.SetKey(NxpscKeyType.TwoKey3Des, new string('0', 32));
    }

    private void FactoryAes_Click(object sender, RoutedEventArgs e) => AuthKey.SetKey(NxpscKeyType.Aes128, new string('0', 32));

    // ---- change key ---------------------------------------------------------------------------------

    private void ChangeKey_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var keyNo = Hex.ParseByte(ChKeyNo.Text, "key no");
        using var oldKey = OldKey.Build();
        using var newKey = NewKey.Build();
        await _s.Run("ChangeKey", c => c.ChangeKey(keyNo, oldKey, newKey), $"key {keyNo:X2} -> {newKey.Type} v{newKey.Version:X2}");
    });

    private void ChangeKeyEv2_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var keyNo = Hex.ParseByte(ChKeyNo.Text, "key no");
        var keySet = Hex.ParseByte(ChKeySet.Text, "key set");
        using var oldKey = OldKey.Build();
        using var newKey = NewKey.Build();
        await _s.Run("ChangeKeyEV2", c => c.ChangeKeyEv2(keySet, keyNo, oldKey, newKey), $"set {keySet:X2} key {keyNo:X2} -> {newKey.Type} v{newKey.Version:X2}");
    });

    // ---- versions --------------------------------------------------------------------------------------

    private void GetKeyVersion_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var keyNo = Hex.ParseByte(VerKeyNo.Text, "key no");
        await _s.Run("GetKeyVersion", c => c.GetKeyVersion(keyNo), v => $"key {keyNo:X2}: version 0x{v:X2}");
    });

    private void AllKeyVersions_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        await _s.Run("GetKeyVersions", c =>
        {
            var ks = c.GetKeySettings();
            var lines = new List<string>();
            for (byte k = 0; k < ks.NumKeys; k++)
                lines.Add($"key {k:X2}: version 0x{c.GetKeyVersion(k):X2}");
            return string.Join("\n", lines);
        });
    });

    // ---- key settings ---------------------------------------------------------------------------------

    private byte KsValue() => (byte)((KsAccess.SelectedIndex << 4) |
        (Ks0.IsChecked == true ? 1 : 0) | (Ks1.IsChecked == true ? 2 : 0) | (Ks2.IsChecked == true ? 4 : 0) | (Ks3.IsChecked == true ? 8 : 0));

    private void Ks_Changed(object sender, RoutedEventArgs e) => SyncRaw();
    private void Ks_Selection(object sender, SelectionChangedEventArgs e) => SyncRaw();

    private void SyncRaw()
    {
        if (_syncing || KsAccess is null || KsRaw is null) return;
        _syncing = true;
        KsRaw.Text = KsValue().ToString("X2");
        KsBits.Text = Describe.Bits(KsValue());
        _syncing = false;
    }

    private void KsRaw_Changed(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        if (!byte.TryParse(KsRaw.Text, System.Globalization.NumberStyles.HexNumber, null, out var v)) return;
        _syncing = true;
        Ks0.IsChecked = (v & 1) != 0;
        Ks1.IsChecked = (v & 2) != 0;
        Ks2.IsChecked = (v & 4) != 0;
        Ks3.IsChecked = (v & 8) != 0;
        KsAccess.SelectedIndex = v >> 4;
        KsBits.Text = Describe.Bits(v);
        _syncing = false;
    }

    private void GetKeySettings_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var r = await _s.Run("GetKeySettings", c => c.GetKeySettings(), Describe.KeySettings);
        if (r.Ok && r.Value is { } ks)
        {
            KsRaw.Text = ks.Settings.ToString("X2");
            KsRaw_Changed(this, null!);
        }
    });

    private void ChangeKeySettings_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var value = Hex.ParseByte(KsRaw.Text, "key settings");
        if ((value & 0x09) != 0x09 || (value >> 4) == 0xF)
        {
            if (!Ui.ConfirmOneWay("ChangeKeySettings", $"KeySettings 0x{value:X2} clears the master-key-changeable or configuration-changeable bit, or freezes key changing.")) return;
        }
        await _s.Run("ChangeKeySettings", c => c.ChangeKeySettings(value), $"0x{value:X2} ({Describe.Bits(value)})");
    });
}
