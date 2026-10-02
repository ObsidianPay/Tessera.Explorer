using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

/// <summary>Everything in libnxpsc that needs no card.</summary>
public partial class ToolsPage : UserControl, IPage
{
    public ToolsPage()
    {
        InitializeComponent();
        MasterKey.SetKey(NxpscKeyType.Aes128, "00112233445566778899AABBCCDDEEFF");
        SdmComm.Select(NxpscCommMode.Plain);
        SdmAccess.Value = new NxpscAccessRights(0x0E, 0, 0, 0);
        TmKey.SetKey(NxpscKeyType.Aes128, new string('0', 32), 1);
    }

    // ---- AN10922 -----------------------------------------------------------------------------------------

    private void Diversify_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var input = Hex.ParseBytes(DivInput.Text, "input", maxLength: 31);
        var version = Hex.ParseByte(DivVersion.Text, "version");
        using var master = MasterKey.Build();
        try
        {
            using var derived = NxpscLibrary.DiversifyAn10922(master, input, version);
            var buffer = new byte[NxpscKey.SizeOf(derived.Type)];
            derived.ExportTo(buffer);
            DerivedKey.Text = Hex.Format(buffer);
            CryptographicOperations.ZeroMemory(buffer);
            OutputLog.Ok("DiversifyAn10922", $"{derived.Type} key derived from {input.Length} input byte(s), version {version:X2} (shown on the Tools page)");
        }
        catch (NxpscException ex) { OutputLog.Error("DiversifyAn10922", ex.Message); }
        return Task.CompletedTask;
    });

    private void CopyDerived_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(DerivedKey.Text)) return;
        try { Clipboard.SetText(DerivedKey.Text); } catch { /* clipboard busy */ }
    }

    private void Example_Click(object sender, RoutedEventArgs e)
    {
        MasterKey.SetKey(NxpscKeyType.Aes128, "00112233445566778899AABBCCDDEEFF");
        DivInput.Text = "04782E21801D803042F54E585020416275";
        DivVersion.Text = "00";
        Diversify_Click(sender, e);
        var ok = DerivedKey.Text.Equals("A8DD63A3B89D54B37CA802473FDA9175", StringComparison.OrdinalIgnoreCase);
        OutputLog.Write(ok ? LogLevel.Ok : LogLevel.Error, "AN10922", ok ? "Matches the published AES-128 example (A8DD63A3...9175)." : "Does NOT match the published AES-128 example.");
    }

    // ---- access rights ------------------------------------------------------------------------------------------

    private void Pack_Click(object sender, RoutedEventArgs e)
    {
        var access = PackAccess.Value;
        var packed = NxpscLibrary.PackAccess(access);
        PackedBox.Text = packed.ToString("X4");
        OutputLog.Ok("PackAccess", $"{Describe.Access(access)}");
    }

    private void Unpack_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var raw = Hex.ParseUShort(PackedBox.Text, "packed");
        var access = NxpscLibrary.UnpackAccess(raw);
        PackAccess.Value = access;
        OutputLog.Ok("UnpackAccess", $"0x{raw:X4} -> {Describe.Access(access)}");
        return Task.CompletedTask;
    });

    // ---- SDM ------------------------------------------------------------------------------------------------------

    private void BuildSdm_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var sdm = Sdm.Build();
        try
        {
            var payload = NxpscLibrary.BuildSdmSettings(SdmComm.Selected<NxpscCommMode>(), SdmAccess.Value, sdm);
            OutputLog.Ok("BuildSdmSettings", $"{payload.Length} bytes");
            OutputLog.Data("BuildSdmSettings", Hex.Dump(payload));
        }
        catch (NxpscException ex) { OutputLog.Error("BuildSdmSettings", ex.Message); }
        return Task.CompletedTask;
    });

    // ---- transaction MAC ----------------------------------------------------------------------------------------------

    private void BuildTmi_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var file = Hex.ParseByte(TmiFile.Text, "file no"); var offset = Hex.ParseUInt(TmiOffset.Text, "offset"); var record = Hex.ParseData(TmiRecord.Text, "record");
        try
        {
            var tmi = NxpscTmac.BuildWriteRecordTmi(file, offset, record);
            TmTmi.Text = Hex.Format(tmi);
            OutputLog.Ok("BuildWriteRecordTmi", $"{tmi.Length} bytes (copied into the TMI field of Compute / Verify)");
            OutputLog.Data("BuildWriteRecordTmi", Hex.Dump(tmi));
        }
        catch (NxpscException ex) { OutputLog.Error("BuildWriteRecordTmi", ex.Message); }
        return Task.CompletedTask;
    });

    private void TmCompute_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var uid = Hex.ParseBytes(TmUid.Text, "UID", 7); var tmc = Hex.ParseBytes(TmTmc.Text, "TMC", 4); var tmi = Hex.ParseBytes(TmTmi.Text, "TMI");
        using var key = TmKey.Build();
        try
        {
            var tmv = NxpscTmac.Compute(key, uid, tmc, tmi);
            TmTmv.Text = Hex.Format(tmv);
            OutputLog.Ok("NxpscTmac.Compute", $"TMV {Hex.Format(tmv)}");
        }
        catch (NxpscException ex) { OutputLog.Error("NxpscTmac.Compute", ex.Message); }
        return Task.CompletedTask;
    });

    private void TmVerify_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var uid = Hex.ParseBytes(TmUid.Text, "UID", 7); var tmc = Hex.ParseBytes(TmTmc.Text, "TMC", 4);
        var tmv = Hex.ParseBytes(TmTmv.Text, "TMV", 8); var tmi = Hex.ParseBytes(TmTmi.Text, "TMI");
        using var key = TmKey.Build();
        try
        {
            var ok = NxpscTmac.Verify(key, uid, new TransactionMac(tmc, tmv), tmi);
            OutputLog.Write(ok ? LogLevel.Ok : LogLevel.Error, "NxpscTmac.Verify", ok ? "MAC matches." : "MAC does NOT match. Refuse the transaction.");
        }
        catch (NxpscException ex) { OutputLog.Error("NxpscTmac.Verify", ex.Message); }
        return Task.CompletedTask;
    });

    // ---- lookups ---------------------------------------------------------------------------------------------------------

    private void LookStatus_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var s = Hex.ParseByte(LookStatus.Text, "status");
        OutputLog.Data("StatusText", Describe.Status(s));
        return Task.CompletedTask;
    });

    private void ListStatuses_Click(object sender, RoutedEventArgs e)
    {
        var lines = typeof(DesfireStatus).GetFields().Where(f => f.IsLiteral).Select(f => (byte)f.GetRawConstantValue()!)
            .OrderBy(b => b).Select(b => Describe.Status(b));
        OutputLog.Data("DesfireStatus", string.Join("\n", lines));
    }

    private void ListErrors_Click(object sender, RoutedEventArgs e) =>
        OutputLog.Data("ErrorText", string.Join("\n", Enum.GetValues<NxpscErrorCode>().Select(c => $"{(int)c,3}  {c,-12} {NxpscLibrary.ErrorText(c)}")));

    private void LookCard_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var t = Hex.ParseByte(LookHwType.Text, "type"); var maj = Hex.ParseByte(LookMajor.Text, "major"); var min = Hex.ParseByte(LookMinor.Text, "minor");
        var card = NxpscLibrary.CardTypeFromVersion(t, maj, min);
        OutputLog.Data("CardTypeFromVersion", $"type {t:X2} version {maj:X2}.{min:X2} -> {NxpscLibrary.CardTypeName(card)}");
        return Task.CompletedTask;
    });

    private void KeySizes_Click(object sender, RoutedEventArgs e) =>
        OutputLog.Data("KeySize", string.Join("\n", Enum.GetValues<NxpscKeyType>().Select(t => $"{NxpscLibrary.KeyTypeName(t),-14} {NxpscLibrary.KeySize(t),2} bytes  (managed {NxpscKey.SizeOf(t)})")));
}
