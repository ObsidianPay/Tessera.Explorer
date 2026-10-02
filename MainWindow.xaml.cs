using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;
using Tessera.Explorer.Dialogs;
using Tessera.Explorer.Pages;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer;

public partial class MainWindow : Window
{
    private sealed record NavItem(string Id, string Title, string Subtitle, string Glyph, string Group, bool NeedsCard, Func<UserControl> Create);

    private readonly CardSession _session = CardSession.Instance;
    private readonly Dictionary<string, UserControl> _pages = [];
    private readonly Dictionary<string, RadioButton> _navButtons = [];
    private readonly List<NavItem> _nav;
    private NavItem? _current;

    public static MainWindow? Current { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Current = this;

        // never open larger than the screen's work area (small laptop displays)
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width - 16);
        Height = Math.Min(Height, work.Height - 16);
        MinHeight = Math.Min(MinHeight, Height);
        MinWidth = Math.Min(MinWidth, Width);

        _nav =
        [
            new("connection", "Connection", "Pick a reader or a mock card, open a session, tune the session settings.", "", "SESSION", false, () => new ConnectionPage()),
            new("keys", "Keys & authentication", "SelectApplication, the Authenticate family, ChangeKey, key settings and versions.", "", "DESFIRE", true, () => new KeysPage()),
            new("apps", "Applications", "Application list, CreateApplication in all its forms, PICC-level configuration.", "", "DESFIRE", true, () => new ApplicationsPage()),
            new("files", "Files", "File explorer for the selected application: settings, data, values, records, transactions.", "", "DESFIRE", true, () => new FilesPage()),
            new("ev2", "EV2 / EV3 features", "Transaction MAC, delegated applications, key sets, MFC mapping, proximity check.", "", "DESFIRE", true, () => new Ev2Page()),
            new("iso", "ISO 7816 & NTAG", "ISO select, READ/UPDATE BINARY, NTAG 424 DNA select and secure dynamic messaging.", "", "OTHER FAMILIES", true, () => new IsoPage()),
            new("plus", "MIFARE Plus", "Security level 3: authenticate, read, write, value blocks, personalisation.", "", "OTHER FAMILIES", true, () => new PlusPage()),
            new("raw", "Raw command", "Send any native command through the active secure channel.", "", "TOOLS", true, () => new RawPage()),
            new("tools", "Offline tools", "No card needed: AN10922, access rights, SDM payloads, transaction MAC, name lookups.", "", "TOOLS", false, () => new ToolsPage()),
            new("mock", "Mock lab", "Shape the mock card: applications, keys, files, signature, tear-off, transaction MAC.", "", "TOOLS", true, () => new MockLabPage()),
        ];

        LoadLayout();
        ApplyDock();
        BuildNav();
        BuildGoMenu();

        OutputLog.Added += OnLogAdded;
        OutputLog.Cleared += RenderOutput;
        _session.StateChanged += OnStateChanged;

        SidebarLib.Text = $"LIBNXPSC {NxpscLibrary.LibraryVersion}";
        ThemeSystem.IsChecked = true;
        OnStateChanged();
        Navigate("connection");
        Loaded += async (_, _) => await ApplyLaunchArguments();

        OutputLog.Info("explorer", $"Tessera Explorer ready. libnxpsc {NxpscLibrary.LibraryVersion}, PC/SC readers: {PcscTransport.ListReaders().Count}.");
        if (!NxpscLibrary.SelfTest())
            OutputLog.Error("selftest", "libnxpsc's crypto self test FAILED. Results from this build cannot be trusted.");
    }

    // ===================================================== launch arguments

    /// <summary>--mock opens a DESFire EV3 mock straight away; --seed adds demo content; --page &lt;id&gt; opens a page.</summary>
    private async Task ApplyLaunchArguments()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToList();
        if (args.Contains("--mock"))
        {
            await _session.ConnectMockAsync(NxpscCardType.DesfireEv3, Convert.FromHexString("04A1B2C3D4E5F6"), NxpscCmdSet.NativeIso);
            if (args.Contains("--seed"))
            {
                Navigate("mock");
                (_pages["mock"] as MockLabPage)?.Seed();
            }
        }
        var i = args.IndexOf("--page");
        if (i >= 0 && i + 1 < args.Count && _nav.Any(n => n.Id == args[i + 1])) Navigate(args[i + 1]);
    }

    // ===================================================== navigation

    private void BuildNav()
    {
        string? group = null;
        foreach (var item in _nav)
        {
            if (item.Group != group)
            {
                group = item.Group;
                NavHost.Children.Add(new TextBlock
                {
                    Text = group,
                    Style = (Style)FindResource("Eyebrow"),
                    Margin = new Thickness(10, NavHost.Children.Count == 0 ? 2 : 16, 0, 6),
                });
            }
            var button = new RadioButton
            {
                Content = item.Title,
                Tag = item.Glyph,
                GroupName = "nav",
                Style = (Style)FindResource("NavButton"),
                Visibility = item.Id == "mock" ? Visibility.Collapsed : Visibility.Visible,
            };
            button.Checked += (_, _) => Navigate(item.Id);
            _navButtons[item.Id] = button;
            NavHost.Children.Add(button);
        }
    }

    private void BuildGoMenu()
    {
        int n = 1;
        foreach (var item in _nav)
        {
            var mi = new MenuItem { Header = item.Title, InputGestureText = n <= 9 ? $"Ctrl+{n}" : "" };
            mi.Click += (_, _) => Navigate(item.Id);
            GoMenu.Items.Add(mi);
            n++;
        }
    }

    public void Navigate(string id)
    {
        var item = _nav.First(n => n.Id == id);
        if (!_pages.TryGetValue(id, out var page))
            _pages[id] = page = item.Create();
        _current = item;
        PageHost.Content = page;
        PageTitle.Text = item.Title;
        PageSubtitle.Text = item.Subtitle;
        if (_navButtons.TryGetValue(id, out var button)) button.IsChecked = true;
        UpdateOverlay();
        (page as IPage)?.OnShown();
    }

    private void UpdateOverlay() =>
        NoSession.Visibility = _current is { NeedsCard: true } && !_session.IsConnected ? Visibility.Visible : Visibility.Collapsed;

    private void Nav_Connection(object sender, RoutedEventArgs e) => Navigate("connection");

    // ===================================================== state

    private void OnStateChanged()
    {
        var s = _session.State;
        DisconnectItem.IsEnabled = s.Connected;
        _navButtons["mock"].Visibility = s.IsMock ? Visibility.Visible : Visibility.Collapsed;
        if (!s.IsMock && _current?.Id == "mock") Navigate("connection");
        UpdateOverlay();
        LostBanner.Visibility = s.SessionLost ? Visibility.Visible : Visibility.Collapsed;

        if (!s.Connected)
        {
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "MutedForeground");
            StatusText.Text = "no session";
            StatusRight.Text = CardSession.MockRngActive ? "MOCK RNG ACTIVE - REAL READERS REFUSED UNTIL RESTART" : "";
            SidebarCard.Text = "no session";
            return;
        }

        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, s.SessionLost ? "Destructive" : "Positive");
        var auth = s.Authenticated ? $"auth {s.Channel}" : "not authenticated";
        StatusText.Text = $"{(s.IsMock ? "mock" : "reader")}  |  {NxpscLibrary.CardTypeName(s.CardType)}  |  AID {s.SelectedAid:X6}  |  {auth}  |  {s.CmdSet}  |  last 0x{s.LastStatus:X2}";
        StatusRight.Text = CardSession.MockRngActive && !s.IsMock ? "" : (s.IsMock ? "MOCK RNG ACTIVE" : "");
        SidebarCard.Text = $"{(s.IsMock ? "mock" : "reader")}: {NxpscLibrary.CardTypeName(s.CardType)}\n{(s.Authenticated ? "authenticated" : "not authenticated")}";
        foreach (var p in _pages.Values) (p as IPage)?.OnStateChanged();
    }

    // ===================================================== window

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyTitleBar(this);

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveLayout();
        _session.Disconnect();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dragging) { EndDrag(); e.Handled = true; return; }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key >= Key.D1 && e.Key <= Key.D9 && e.Key - Key.D1 < _nav.Count)
        {
            var item = _nav[e.Key - Key.D1];
            if (item.Id == "mock" && !_session.IsMock) return;
            Navigate(item.Id);
            e.Handled = true;
        }
        else if (e.Key == Key.L) { OutputLog.Clear(); e.Handled = true; }
        else if (e.Key == Key.T) { TraceToggle.IsChecked = !TraceToggle.IsChecked; TraceToggle_Click(TraceToggle, e); e.Handled = true; }
        else if (e.Key == Key.S) { SaveOutput_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.OemTilde) { OutputToggle.IsChecked = !OutputToggle.IsChecked; OutputToggle_Click(OutputToggle, e); e.Handled = true; }
    }

    // ===================================================== menu: session

    public async void NewMock_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MockSetupDialog();
        if (dlg.ShowDialog() != true) return;
        try
        {
            await _session.ConnectMockAsync(dlg.CardType, dlg.CardUid, dlg.CmdSet);
            Navigate("connection");
        }
        catch (Exception ex)
        {
            OutputLog.Error("mock", ex.Message);
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e) => _session.Disconnect();

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (Environment.ProcessPath is { } path) Process.Start(path);
        Application.Current.Shutdown();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ===================================================== menu: card quick actions

    private void Quick_GetVersion(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("GetVersion", c => c.GetVersion(), Describe.Version));
    private void Quick_Identify(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("Identify", c => c.Identify(), t => NxpscLibrary.CardTypeName(t)));
    private void Quick_Uid(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("GetCardUID", c => c.GetCardUid(), b => Hex.Format(b)));
    private void Quick_FreeMemory(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("GetFreeMemory", c => c.GetFreeMemory(), m => $"{m} bytes"));
    private void Quick_Signature(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("GetSignature", c => c.GetSignature()));
    private void Quick_SelectPicc(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _session.Run("SelectApplication", c => c.SelectApplication(0), "AID 000000"));
    private void Quick_ResetChannel(object sender, RoutedEventArgs e) => Ui.Guard(async () => { _session.Configure(c => c.ResetChannel()); OutputLog.Ok("ResetChannel", "Host side secure channel forgotten."); await Task.CompletedTask; });

    private void Quick_AuthPicc(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        using var key = NxpscKey.FactoryPiccMasterKey();
        await _session.Run("Authenticate", c => c.Authenticate(0, key), "key 0, factory PICC master key (2K3DES zero)");
    });

    // ===================================================== menu: view

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var mode = Enum.Parse<AppTheme>((string)((MenuItem)sender).Tag);
        ThemeManager.Apply(mode);
        ThemeSystem.IsChecked = mode == AppTheme.System;
        ThemeLight.IsChecked = mode == AppTheme.Light;
        ThemeDark.IsChecked = mode == AppTheme.Dark;
        RenderOutput();
    }

    private void TraceToggle_Click(object sender, RoutedEventArgs e)
    {
        TracingTransport.Enabled = TraceToggle.IsChecked;
        if (TraceToggle.IsChecked)
            OutputLog.Warn("trace", "Frame tracing on. Frames contain authentication challenges and enciphered key material.");
        else
            OutputLog.Info("trace", "Frame tracing off.");
    }

    private void SelfTest_Click(object sender, RoutedEventArgs e) =>
        Ui.Guard(async () =>
        {
            var ok = await Task.Run(() => NxpscLibrary.SelfTest(verbose: false));
            if (ok) OutputLog.Ok("selftest", $"libnxpsc {NxpscLibrary.LibraryVersion}: crypto known-answer tests passed (AN10922 vectors included).");
            else OutputLog.Error("selftest", "Crypto self test FAILED.");
        });

    private void About_Click(object sender, RoutedEventArgs e) =>
        Ui.Notice("Tessera Explorer",
            $"A developer front end for Tessera.Nxpsc.\n\nlibnxpsc {NxpscLibrary.LibraryVersion}\nTessera.Nxpsc {typeof(NxpscCard).Assembly.GetName().Version}\n\nEvery page maps onto a call of the managed API; the card's signature is printed on each operation. Keys are never written to the output.");

    // ===================================================== output panel

    private LogLevel[] ActiveLevels() =>
        new[] { (FInfo, LogLevel.Info), (FOk, LogLevel.Ok), (FWarn, LogLevel.Warn), (FError, LogLevel.Error), (FData, LogLevel.Data), (FTrace, LogLevel.Trace) }
            .Where(t => t.Item1.IsChecked == true).Select(t => t.Item2).ToArray();

    private bool Passes(LogEntry entry, LogLevel[] levels, string search) =>
        levels.Contains(entry.Level) && (search.Length == 0 || entry.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));

    private void Filter_Changed(object sender, RoutedEventArgs e) => RenderOutput();

    private void OnLogAdded(LogEntry entry)
    {
        if (Out is null) return;
        if (!Passes(entry, ActiveLevels(), SearchBox.Text)) return;
        var doc = Out.Document;
        // keep the document bounded
        if (doc.Blocks.Count > 6000) for (int i = 0; i < 500; i++) doc.Blocks.Remove(doc.Blocks.FirstBlock);
        doc.Blocks.Add(BuildParagraph(entry));
        if (ScrollLock.IsChecked == true) Out.ScrollToEnd();
    }

    private void RenderOutput()
    {
        var levels = ActiveLevels();
        var search = SearchBox.Text;
        var doc = new FlowDocument { PagePadding = new Thickness(0) };
        foreach (var entry in OutputLog.Snapshot().Where(en => Passes(en, levels, search)))
            doc.Blocks.Add(BuildParagraph(entry));
        Out.Document = doc;
        if (ScrollLock.IsChecked == true) Out.ScrollToEnd();
    }

    private Paragraph BuildParagraph(LogEntry entry)
    {
        var p = new Paragraph { Margin = new Thickness(0, 0, 0, 1), LineHeight = 17 };
        p.Inlines.Add(Colored($"{entry.Time:HH:mm:ss.fff}  ", "MutedForeground"));

        var (tag, brush) = entry.Level switch
        {
            LogLevel.Ok => ("ok   ", "Positive"),
            LogLevel.Warn => ("warn ", "Negative"),
            LogLevel.Error => ("err  ", "Destructive"),
            LogLevel.Data => ("     ", "MutedForeground"),
            LogLevel.Trace => ("frame", "Primary"),
            _ => ("info ", "MutedForeground"),
        };
        p.Inlines.Add(Colored(tag + "  ", brush));

        // side docks are narrow: header on one line, text underneath, instead of a wide hanging indent
        var compact = !IsHorizontalDock;
        var op = compact ? entry.Op : entry.Op.Length > 20 ? entry.Op[..20] : entry.Op.PadRight(20);
        var opRun = Colored(op + (compact ? "\n" : "  "), entry.Level == LogLevel.Trace ? "Primary" : "Foreground");
        opRun.FontWeight = FontWeights.SemiBold;
        p.Inlines.Add(opRun);

        var bodyBrush = entry.Level switch
        {
            LogLevel.Error => "Destructive",
            LogLevel.Warn => "Negative",
            LogLevel.Trace => "MutedForeground",
            _ => "Foreground",
        };
        var indent = compact ? "  " : new string(' ', 14 + 7 + 22);
        if (compact) p.Inlines.Add(Colored("  ", bodyBrush));
        p.Inlines.Add(Colored(entry.Text.Replace("\r", "").Replace("\n", "\n" + indent), bodyBrush));
        return p;
    }

    private static Run Colored(string text, string brushKey)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, brushKey);
        return run;
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        var selection = Out.Selection.Text;
        var text = !string.IsNullOrEmpty(selection)
            ? selection
            : string.Join(Environment.NewLine, OutputLog.Snapshot().Where(en => Passes(en, ActiveLevels(), SearchBox.Text)).Select(en => en.ToString()));
        try { Clipboard.SetText(text); } catch { /* clipboard busy */ }
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e) => OutputLog.Clear();

    private void SaveOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Log (*.log)|*.log|Text (*.txt)|*.txt",
            FileName = $"tessera-explorer-{DateTime.Now:yyyyMMdd-HHmmss}.log",
        };
        if (dlg.ShowDialog(this) != true) return;
        var sb = new StringBuilder();
        foreach (var entry in OutputLog.Snapshot()) sb.AppendLine(entry.ToString());
        System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
        OutputLog.Info("save", $"Output written to {dlg.FileName}");
    }
}
