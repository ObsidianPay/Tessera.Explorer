using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class ApplicationsPage : UserControl, IPage
{
    private sealed record AppRow(string Aid, string IsoFid, string DfName, string KeySettings, string Keys, string KeyType)
    {
        public uint AidValue => Convert.ToUInt32(Aid, 16);
    }

    private readonly CardSession _s = CardSession.Instance;

    public ApplicationsPage()
    {
        InitializeComponent();
        CreType.Select(NxpscKeyType.Aes128);
        CfgType.Select(NxpscKeyType.Aes128);
        DefaultKey.SetKey(NxpscKeyType.TwoKey3Des, new string('0', 32));
    }

    // ---- list -------------------------------------------------------------------------------------------

    private void Refresh_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var ids = await _s.Run("GetApplicationIds", c => c.GetApplicationIds());
        if (!ids.Ok || ids.Value is null) return;
        var names = await _s.Run("GetDFNames", c => c.GetDfNames(), quiet: true);
        var rows = new List<AppRow>();
        foreach (var aid in ids.Value)
        {
            var app = names.Ok ? names.Value?.FirstOrDefault(a => a.Aid == aid) : null;
            rows.Add(app is null
                ? new AppRow(aid.ToString("X6"), "", "", "", "", "")
                : new AppRow(aid.ToString("X6"), app.IsoFidEnabled ? app.IsoFid.ToString("X4") : "",
                    app.DfName.Length == 0 ? "" : $"{Hex.Format(app.DfName)}  {Ascii(app.DfName)}",
                    $"{app.KeySettings:X2}", app.NumKeys.ToString(), NxpscLibrary.KeyTypeName(app.KeyType)));
        }
        AppGrid.ItemsSource = rows;
    });

    private static string Ascii(byte[] data) =>
        data.All(b => b is >= 0x20 and < 0x7F) ? $"\"{Encoding.ASCII.GetString(data)}\"" : "";

    private AppRow? Selected => AppGrid.SelectedItem as AppRow;

    private void AppGrid_DoubleClick(object sender, MouseButtonEventArgs e) => SelectRow_Click(sender, e);

    private void SelectRow_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        if (Selected is not { } row) { OutputLog.Warn("SelectApplication", "Pick a row first."); return; }
        var aid = row.AidValue;
        await _s.Run("SelectApplication", c => c.SelectApplication(aid), $"AID {aid:X6}");
    });

    private void DeleteRow_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        if (Selected is not { } row) { OutputLog.Warn("DeleteApplication", "Pick a row first."); return; }
        var aid = row.AidValue;
        if (!Ui.Confirm("Delete application", $"DeleteApplication {aid:X6} removes the application and all of its files.", "Delete")) return;
        if (await _s.Run("DeleteApplication", c => c.DeleteApplication(aid), $"AID {aid:X6}"))
            Refresh_Click(sender, e);
    });

    private void Delete_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(DeleteAid.Text);
        if (!Ui.Confirm("Delete application", $"DeleteApplication {aid:X6} removes the application and all of its files.", "Delete")) return;
        await _s.Run("DeleteApplication", c => c.DeleteApplication(aid), $"AID {aid:X6}");
    });

    // ---- create -------------------------------------------------------------------------------------------

    private void CreIso_Click(object sender, RoutedEventArgs e) =>
        CreIsoGrid.Visibility = CreIso.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void Create_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(CreAid.Text);
        var ks = Hex.ParseByte(CreKs.Text, "key settings");
        var keys = (byte)Hex.ParseUInt(CreKeys.Text, "keys", 14);
        var type = CreType.Selected<NxpscKeyType>();
        if (CreIso.IsChecked == true)
        {
            var fid = Hex.ParseUShort(CreFid.Text, "ISO FID");
            var name = Hex.ParseData(CreDfName.Text, "DF name", 16);
            await _s.Run("CreateApplicationISO", c => c.CreateApplicationIso(aid, ks, keys, type, fid, name),
                $"AID {aid:X6}, settings {ks:X2}, {keys} x {type}, FID {fid:X4}, DF {Hex.Format(name)}");
        }
        else
        {
            await _s.Run("CreateApplication", c => c.CreateApplication(aid, ks, keys, type), $"AID {aid:X6}, settings {ks:X2}, {keys} x {type}");
        }
    });

    private void CreateCfg_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(CfgAid.Text);
        var dfText = CfgDfName.Text.Trim();
        var config = new NxpscApplicationConfig
        {
            KeySettings = Hex.ParseByte(CfgKs.Text, "KeySettings1"),
            NumKeys = (byte)Hex.ParseUInt(CfgKeys.Text, "keys", 14),
            KeyType = CfgType.Selected<NxpscKeyType>(),
            IsoFidEnabled = CfgIsoOn.IsChecked == true,
            IsoFid = string.IsNullOrWhiteSpace(CfgFid.Text) ? (ushort)0 : Hex.ParseUShort(CfgFid.Text, "ISO FID"),
            DfName = dfText.Length == 0 ? null : Hex.ParseData(dfText, "DF name", 16),
            NumKeySets = (byte)Hex.ParseUInt(CfgSets.Text, "key sets", 16),
            KeySetVersion = Hex.ParseByte(CfgAks.Text, "AKS version"),
            MaxKeySize = (byte)Hex.ParseUInt(CfgMaxKey.Text, "max key size", 32),
            KeySetSettings = (byte)Hex.ParseUInt(CfgSetSett.Text, "AppKeySetSett", 7),
            SpecificVcKeys = CfgVc.IsChecked == true,
            SpecificCapabilityData = CfgCap.IsChecked == true,
        };
        await _s.Run("CreateApplicationEx", c => c.CreateApplication(aid, config), $"AID {aid:X6}, {config.NumKeys} x {config.KeyType}, {config.NumKeySets} key set(s)");
    });

    // ---- PICC -------------------------------------------------------------------------------------------------

    private void Format_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        if (!Ui.ConfirmOneWay("FormatPICC", "Every application and file on the card is erased.")) return;
        await _s.Run("FormatPICC", c => c.FormatPicc(), "card wiped");
    });

    private NxpscPiccConfig PiccConfig() => new(CfgDisableFormat.IsChecked == true, CfgRandomUid.IsChecked == true,
        CfgPcMandatory.IsChecked == true, CfgAuthVc.IsChecked == true);

    private void SetPiccConfig_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var cfg = PiccConfig();
        if ((cfg.DisableFormat || cfg.RandomUid) &&
            !Ui.ConfirmOneWay("SetPiccConfig", $"DisableFormat={cfg.DisableFormat}, RandomUid={cfg.RandomUid}. Both are permanent on most cards.")) return;
        await _s.Run("SetPiccConfig", c => c.SetPiccConfig(cfg), cfg.ToString());
    });

    private void SetPiccConfig2_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var cfg = PiccConfig();
        if ((cfg.DisableFormat || cfg.RandomUid) &&
            !Ui.ConfirmOneWay("SetPiccConfig", $"DisableFormat={cfg.DisableFormat}, RandomUid={cfg.RandomUid}. Both are permanent on most cards.")) return;
        await _s.Run("SetPiccConfig", c => c.SetPiccConfig(cfg.DisableFormat, cfg.RandomUid), $"disableFormat={cfg.DisableFormat}, randomUid={cfg.RandomUid}");
    });

    private void SetDefaultKey_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        using var key = DefaultKey.Build();
        await _s.Run("SetDefaultKey", c => c.SetDefaultKey(key), key.ToString());
    });

    private void SetAts_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var ats = Hex.ParseBytes(AtsBox.Text, "ATS");
        await _s.Run("SetATS", c => c.SetAts(ats), Hex.Format(ats));
    });

    private void SetConfig_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var option = Hex.ParseByte(RawOption.Text, "option");
        var data = Hex.ParseBytes(RawPayload.Text, "payload");
        if (!Ui.ConfirmOneWay("SetConfiguration", $"Option 0x{option:X2} with {data.Length} byte(s). Some options cannot be reverted.")) return;
        await _s.Run("SetConfiguration", c => c.SetConfiguration(option, data), $"option {option:X2}, {Hex.Format(data)}");
    });
}
