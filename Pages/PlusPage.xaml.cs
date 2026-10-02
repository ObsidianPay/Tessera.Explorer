using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class PlusPage : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;

    public PlusPage()
    {
        InitializeComponent();
        PlusKey.SetKey(NxpscKeyType.Aes128, new string('F', 32));
    }

    private void Auth_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var block = Hex.ParseUShort(AuthBlock.Text, "key block"); var first = AuthFirst.IsChecked == true;
        using var key = PlusKey.Build();
        await _s.Run("PlusAuthenticate", c => c.PlusAuthenticate(block, key, first), $"key block {block:X4}, {(first ? "first" : "following")}");
    });

    private void ResetAuth_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("PlusResetAuth", c => c.PlusResetAuth(), "session ended"));

    private void Read_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var block = Hex.ParseUShort(RwBlock.Text, "block"); var count = (byte)Hex.ParseUInt(RwCount.Text, "count", 255);
        var enc = RwEnc.IsChecked == true; var mac = RwMac.IsChecked == true;
        await _s.Run("PlusRead", c => c.PlusRead(block, count, enc, mac));
    });

    private void Write_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var block = Hex.ParseUShort(RwBlock.Text, "block"); var data = Hex.ParseBytes(RwData.Text, "data"); var enc = RwEnc.IsChecked == true;
        await _s.Run("PlusWrite", c => c.PlusWrite(block, data, enc), $"block {block:X4}, {data.Length} bytes");
    });

    private (ushort block, int delta, bool credit, bool enc) Value() =>
        (Hex.ParseUShort(ValBlock.Text, "block"), Hex.ParseInt(ValDelta.Text, "delta"), ValCredit.IsChecked == true, ValEnc.IsChecked == true);

    private void ValOp_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var (b, d, cr, enc) = Value();
        await _s.Run("PlusValueOperation", c => c.PlusValueOperation(b, d, cr, enc), $"block {b:X4} {(cr ? "+" : "-")}{d}");
    });

    private void Transfer_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var b = Hex.ParseUShort(ValBlock.Text, "block");
        await _s.Run("PlusTransfer", c => c.PlusTransfer(b), $"-> block {b:X4}");
    });

    private void ValTransfer_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var (b, d, cr, enc) = Value();
        await _s.Run("PlusValueTransfer", c => c.PlusValueTransfer(b, d, cr, enc), $"block {b:X4} {(cr ? "+" : "-")}{d}");
    });

    private void Restore_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var b = Hex.ParseUShort(ValBlock.Text, "block");
        await _s.Run("PlusRestore", c => c.PlusRestore(b), $"block {b:X4} -> transfer buffer");
    });

    private void WritePerso_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var b = Hex.ParseUShort(PersoBlock.Text, "block"); var data = Hex.ParseBytes(PersoData.Text, "data");
        await _s.Run("PlusWritePerso", c => c.PlusWritePerso(b, data), $"block {b:X4}, {data.Length} bytes");
    });

    private void CommitPerso_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        if (!Ui.ConfirmOneWay("CommitPerso", "The card leaves security level 0 and personalisation is over.")) return;
        await _s.Run("PlusCommitPerso", c => c.PlusCommitPerso(), "committed");
    });

    private void Sl1_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var p = Hex.ParseBytes(Sl1Payload.Text, "payload");
        await _s.Run("PlusSetConfigSl1", c => c.PlusSetConfigSl1(p), Hex.Format(p));
    });

    private void PersonalizeUid_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var t = Hex.ParseByte(UidType.Text, "UID type");
        if (!Ui.ConfirmOneWay("PersonalizeUID", $"UID type 0x{t:X2} is written to the card.")) return;
        await _s.Run("PlusPersonalizeUid", c => c.PlusPersonalizeUid(t), $"type {t:X2}");
    });

    private void Vc_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("PlusVcSupportLastIsoL3", c => c.PlusVcSupportLastIsoL3()));
}
