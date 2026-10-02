using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class MockLabPage : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;

    public MockLabPage()
    {
        InitializeComponent();
        AppType.Select(NxpscKeyType.Aes128);
        FileComm.Select(NxpscCommMode.Plain);
        FileAccess.Value = new NxpscAccessRights(0x0E, 0x0E, 0x0E, 0);
        MockKey.SetKey(NxpscKeyType.TwoKey3Des, new string('0', 32));
        ChangeResultKey.SetKey(NxpscKeyType.Aes128, "00112233445566778899AABBCCDDEEFF", 1);
        TmacKey.SetKey(NxpscKeyType.Aes128, "A0A1A2A3A4A5A6A7A8A9AAABACADAEAF", 1);
        TmComm.Select(NxpscCommMode.Mac);
        TmAccess.Value = new NxpscAccessRights(2, 0x0F, 0x0F, 0);
    }

    // ---- content --------------------------------------------------------------------------------------------------

    private void Seed_Click(object sender, RoutedEventArgs e) => Seed();

    public void Seed() => Ui.Guard(async () =>
    {
        const uint aid = 0x123456;
        var free = new NxpscAccessRights(0x0E, 0x0E, 0x0E, 0);
        if (!await _s.RunMock("AddApplication", m => m.AddApplication(aid, NxpscKeyType.Aes128, 3), $"AID {aid:X6}, AES-128 x 3")) return;
        if (!await _s.Run("SelectApplication", c => c.SelectApplication(aid), $"AID {aid:X6}")) return;
        await _s.RunMock("AddFile 01", m => m.AddFile(0x01, NxpscFileType.Standard, NxpscCommMode.Plain, free, size: 64), "standard, 64 B");
        await _s.RunMock("AddFile 02", m => m.AddFile(0x02, NxpscFileType.CyclicRecord, NxpscCommMode.Plain, free, recordSize: 32, maxRecords: 8), "cyclic record, 8 x 32 B");
        await _s.RunMock("AddFile 03", m => m.AddFile(0x03, NxpscFileType.Backup, NxpscCommMode.Plain, free, size: 32), "backup, 32 B");
        OutputLog.Info("seed", "Open the Files page. Value files are created through the card (Files > Create) after authenticating with the factory AES key.");
    });

    private void AddApp_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(AppAid.Text); var type = AppType.Selected<NxpscKeyType>(); var keys = (byte)Hex.ParseUInt(AppKeys.Text, "keys", 14);
        await _s.RunMock("AddApplication", m => m.AddApplication(aid, type, keys), $"AID {aid:X6}, {type} x {keys}");
    });

    private void HasApp_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(AppAid.Text);
        await _s.RunMock("HasApplication", m => OutputLog.Data("HasApplication", $"AID {aid:X6}: {m.HasApplication(aid)}"), $"AID {aid:X6}");
    });

    private void AddFile_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var no = Hex.ParseByte(FileNo.Text, "file no"); var type = FileType.Selected<NxpscFileType>(); var comm = FileComm.Selected<NxpscCommMode>();
        var size = Hex.ParseUInt(FileSize.Text, "size"); var rs = Hex.ParseUInt(FileRecSize.Text, "record size"); var rm = Hex.ParseUInt(FileRecMax.Text, "max records");
        var access = FileAccess.Value;
        await _s.RunMock("AddFile", m => m.AddFile(no, type, comm, access, rs, rm, size), $"file {no:X2}, {type}, {comm}");
    });

    // ---- keys -------------------------------------------------------------------------------------------------------------

    private void SetKey_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(KeyAid.Text); var no = Hex.ParseByte(KeyNo.Text, "key no");
        using var key = MockKey.Build();
        await _s.RunMock("SetKey", m => m.SetKey(aid, no, key), $"AID {aid:X6} key {no:X2} = {key.Type}");
    });

    private void KeyEquals_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(KeyAid.Text); var no = Hex.ParseByte(KeyNo.Text, "key no");
        using var key = MockKey.Build();
        await _s.RunMock("KeyEquals", m => OutputLog.Data("KeyEquals", $"AID {aid:X6} key {no:X2} {(m.KeyEquals(aid, no, key) ? "EQUALS" : "differs from")} the key entered"));
    });

    private void ChangeResult_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        using var key = ChangeResultKey.Build();
        await _s.RunMock("SetChangeKeyResult", m => m.SetChangeKeyResult(key), $"next ChangeKey leaves {key.Type} v{key.Version:X2}");
    });

    private void ChangeIgnored_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
        await _s.RunMock("SetChangeKeyIgnored", m => m.SetChangeKeyIgnored(), "ChangeKey will be acknowledged and ignored"));

    // ---- transaction MAC ------------------------------------------------------------------------------------------------------

    private void TmKey_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        using var key = TmacKey.Build();
        await _s.RunMock("SetTransactionMacKey", m => m.SetTransactionMacKey(key), $"v{key.Version:X2}");
    });

    private void TmEnable_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
        await _s.RunMock("EnableTransactionMac", m => m.EnableTransactionMac(), "enabled"));

    private void TmCounter_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
        await _s.RunMock("TransactionCounter", m => OutputLog.Data("TransactionCounter", m.TransactionCounter.ToString())));

    private void TmFileSettings_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var no = Hex.ParseByte(TmFile.Text, "file no"); var comm = TmComm.Selected<NxpscCommMode>(); var access = TmAccess.Value;
        await _s.RunMock("SetTransactionMacFileSettings", m => m.SetTransactionMacFileSettings(no, comm, access), $"file {no:X2}, {comm}, {Describe.Access(access)}");
    });

    // ---- behaviour ----------------------------------------------------------------------------------------------------------------

    private void Sig_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var sig = Hex.ParseBytes(Sig.Text, "signature", maxLength: 64);
        await _s.RunMock("SetSignature", m => m.SetSignature(sig), $"{sig.Length} bytes");
    });

    private void Tear_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var cmd = Hex.ParseByte(TearCmd.Text, "command"); var after = TearAfter.IsChecked == true;
        await _s.RunMock("TearOn", m => m.TearOn(cmd, after), $"command {cmd:X2}, {(after ? "after" : "before")} execute");
    });

    private void Torn_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
        await _s.RunMock("Torn", m => OutputLog.Data("Torn", m.Torn.ToString())));

    private void Reinsert_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
        await _s.RunMock("Reinsert", m => m.Reinsert(), "card back in the field"));
}
