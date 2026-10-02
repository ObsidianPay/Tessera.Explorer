using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class RawPage : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;

    public RawPage()
    {
        InitializeComponent();
        ((IPage)this).OnStateChanged();
    }

    void IPage.OnStateChanged() => LastStatusText.Text = _s.IsConnected ? Describe.Status(_s.State.LastStatus) : "-";

    private void Send_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var cmd = Hex.ParseByte(Cmd.Text, "command");
        var data = Hex.ParseData(Data.Text);
        var tx = TxMode.Selected<NxpscCommMode>(); var rx = RxMode.Selected<NxpscCommMode>();
        var capacity = (int)Hex.ParseUInt(Capacity.Text, "capacity", 65536);
        var r = await _s.Run($"Command {cmd:X2}", c => c.Command(cmd, data, tx, rx, capacity));
        if (r.Ok) OutputLog.Data($"Command {cmd:X2}", $"status {Describe.Status(_s.State.LastStatus)}");
    });

    private void Preset(string cmd, string data = "")
    {
        Cmd.Text = cmd;
        Data.Text = data;
        TxMode.Select(NxpscCommMode.Plain);
        RxMode.Select(NxpscCommMode.Plain);
        Send_Click(this, null!);
    }

    private void Preset_Version(object sender, RoutedEventArgs e) => Preset("60");
    private void Preset_AppIds(object sender, RoutedEventArgs e) => Preset("6A");
    private void Preset_Free(object sender, RoutedEventArgs e) => Preset("6E");
}
