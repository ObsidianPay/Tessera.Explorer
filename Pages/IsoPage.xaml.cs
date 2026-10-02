using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class IsoPage : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;

    public IsoPage()
    {
        InitializeComponent();
        SdmComm.Select(NxpscCommMode.Plain);
        SdmAccess.Value = new NxpscAccessRights(0x0E, 0, 0, 0);
    }

    private void SelectName_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var name = Hex.ParseData(DfName.Text, "DF name", 16);
        await _s.Run("IsoSelectDfName", c => c.IsoSelectDfName(name), Hex.Format(name));
    });

    private void SelectFid_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var fid = Hex.ParseUShort(SelFid.Text, "FID"); var ef = SelIsEf.IsChecked == true;
        await _s.Run("IsoSelectFid", c => c.IsoSelectFid(fid, ef), $"{fid:X4} ({(ef ? "EF" : "DF")})");
    });

    private void ReadBinary_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var sfi = Hex.ParseByte(BinSfi.Text, "SFI"); var off = Hex.ParseUShort(BinOffset.Text, "offset"); var len = (int)Hex.ParseUInt(BinLength.Text, "length", 4096);
        await _s.Run("IsoReadBinary", c => c.IsoReadBinary(sfi, off, len));
    });

    private void UpdateBinary_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var sfi = Hex.ParseByte(BinSfi.Text, "SFI"); var off = Hex.ParseUShort(BinOffset.Text, "offset"); var data = Hex.ParseData(BinData.Text);
        await _s.Run("IsoUpdateBinary", c => c.IsoUpdateBinary(sfi, off, data), $"{data.Length} bytes at {off:X4}");
    });

    private void NtagSelect_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("Ntag424Select", c => c.Ntag424Select(), "D2760000850101"));

    private void ConfigureSdm_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = Hex.ParseByte(SdmFile.Text, "file no"); var comm = SdmComm.Selected<NxpscCommMode>(); var access = SdmAccess.Value; var sdm = Sdm.Build();
        if (access.Change == NxpscAccessRights.Denied && !Ui.ConfirmOneWay("ConfigureSdm", "Change right 'denied' freezes the file's settings.")) return;
        await _s.Run("ConfigureSdm", c => c.ConfigureSdm(f, comm, access, sdm), $"file {f:X2}");
    });

    private void BuildSdm_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var comm = SdmComm.Selected<NxpscCommMode>(); var access = SdmAccess.Value; var sdm = Sdm.Build();
        try
        {
            var payload = NxpscLibrary.BuildSdmSettings(comm, access, sdm);
            OutputLog.Ok("BuildSdmSettings", $"{payload.Length} bytes");
            OutputLog.Data("BuildSdmSettings", Hex.Dump(payload));
        }
        catch (NxpscException ex) { OutputLog.Error("BuildSdmSettings", ex.Message); }
        return Task.CompletedTask;
    });
}
