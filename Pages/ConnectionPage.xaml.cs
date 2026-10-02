using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class ConnectionPage : UserControl, IPage
{
    private const string MockEntry = "Mock card (simulated, libnxpsc mockcard)";
    private readonly CardSession _s = CardSession.Instance;
    private CancellationTokenSource? _wait;

    public ConnectionPage()
    {
        InitializeComponent();
        CmdSetBox.Select(NxpscCmdSet.NativeIso);
        LiveCmdSet.Select(NxpscCmdSet.NativeIso);
        ScanReaders();
        ((IPage)this).OnStateChanged();
    }

    private void ScanReaders()
    {
        var readers = PcscTransport.ListReaders();
        var previous = SourceBox.SelectedItem as string;
        var items = readers.Append(MockEntry).ToList();
        SourceBox.ItemsSource = items;
        var preferred = PcscTransport.ChooseReader(readers);
        SourceBox.SelectedItem = previous is not null && items.Contains(previous) ? previous : preferred ?? MockEntry;
        OutputLog.Info("readers", readers.Count == 0 ? "No PC/SC readers found." : $"{readers.Count} reader(s): {string.Join("; ", readers)}");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ScanReaders();

    private void Source_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceBox.SelectedItem is not string s) return;
        if (s == MockEntry)
            SourceHint.Text = "Card type and UID are chosen in the next step. Creating a mock fixes libnxpsc's RNG for this process.";
        else if (CardSession.MockRngActive)
            SourceHint.Text = "A mock card was created earlier in this process, so libnxpsc refuses real readers until you restart (Session > Restart explorer).";
        else
            SourceHint.Text = "Waits for a card on this reader, then connects over T=1 (SCardTransmit).";
    }

    private void Open_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        if (SourceBox.SelectedItem is not string source)
        {
            OutputLog.Error("connect", "No source selected.");
            return;
        }
        if (source == MockEntry)
        {
            MainWindow.Current!.NewMock_Click(this, e);
            return;
        }
        if (CardSession.MockRngActive)
        {
            if (Ui.Confirm("Restart needed", "libnxpsc's RNG was fixed by the mock card earlier in this process, so it refuses real cards. Restart the explorer to open this reader?", "Restart", danger: false))
            {
                System.Diagnostics.Process.Start(Environment.ProcessPath!);
                Application.Current.Shutdown();
            }
            return;
        }
        var seconds = Hex.ParseUInt(TimeoutBox.Text, "wait", 600);
        _wait = new CancellationTokenSource();
        OpenButton.IsEnabled = false;
        CancelWaitButton.Visibility = Visibility.Visible;
        try
        {
            await _s.ConnectReaderAsync(source, TimeSpan.FromSeconds(seconds), CmdSetBox.Selected<NxpscCmdSet>(), _wait.Token);
        }
        catch (TimeoutException ex) { OutputLog.Error("connect", ex.Message); }
        catch (OperationCanceledException) { OutputLog.Warn("connect", "Stopped waiting."); }
        catch (Exception ex) { OutputLog.Error("connect", ex.Message); }
        finally
        {
            OpenButton.IsEnabled = true;
            CancelWaitButton.Visibility = Visibility.Collapsed;
            _wait = null;
        }
    });

    private void CancelWait_Click(object sender, RoutedEventArgs e) => _wait?.Cancel();
    private void Close_Click(object sender, RoutedEventArgs e) => _s.Disconnect();

    void IPage.OnShown() => ((IPage)this).OnStateChanged();

    void IPage.OnStateChanged()
    {
        var st = _s.State;
        if (!st.Connected)
        {
            StateText.Text = "no session";
            return;
        }
        StateText.Text =
            $"transport      {st.Transport}\n" +
            $"card type      {NxpscLibrary.CardTypeName(st.CardType)}\n" +
            $"cmd set        {st.CmdSet}\n" +
            $"selected AID   {st.SelectedAid:X6}\n" +
            $"authenticated  {(st.Authenticated ? "yes" : "no")}\n" +
            $"channel        {st.Channel}\n" +
            $"last status    {Describe.Status(st.LastStatus)}\n" +
            $"session lost   {(st.SessionLost ? "YES" : "no")}\n" +
            $"iso chaining   {st.IsoChaining}";
        LiveCmdSet.Select(st.CmdSet);
        IsoChain.IsChecked = st.IsoChaining;
    }

    private void GetVersion_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("GetVersion", c => c.GetVersion(), Describe.Version));
    private void Identify_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("Identify", c => c.Identify(), t => NxpscLibrary.CardTypeName(t)));
    private void CardType_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("CardType", c => c.CardType, t => NxpscLibrary.CardTypeName(t)));
    private void Uid_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("GetCardUID", c => c.GetCardUid(), b => Hex.Format(b)));
    private void Free_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("GetFreeMemory", c => c.GetFreeMemory(), m => $"{m} bytes"));
    private void Sig_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("GetSignature", c => c.GetSignature()));

    private void Apply_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        var cmd = LiveCmdSet.Selected<NxpscCmdSet>();
        var comm = LiveComm.Selected<NxpscCommMode>();
        var chain = IsoChain.IsChecked == true;
        _s.Configure(c => { c.CmdSet = cmd; c.SetCommMode(comm); c.IsoChaining = chain; });
        OutputLog.Ok("Session", $"CmdSet={cmd}, default comm mode={comm}, ISO chaining={chain}");
        return Task.CompletedTask;
    });

    private void ResetChannel_Click(object sender, RoutedEventArgs e) => Ui.Guard(() =>
    {
        _s.Configure(c => c.ResetChannel());
        OutputLog.Ok("ResetChannel", "Host side secure channel forgotten.");
        return Task.CompletedTask;
    });

    private void LibVersion_Click(object sender, RoutedEventArgs e) => OutputLog.Data("libnxpsc", NxpscLibrary.LibraryVersion);

    private void SelfTest_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var ok = await Task.Run(() => NxpscLibrary.SelfTest(true));
        if (ok) OutputLog.Ok("selftest", "crypto known-answer tests passed");
        else OutputLog.Error("selftest", "crypto self test FAILED");
    });
}
