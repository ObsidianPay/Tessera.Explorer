using System.Windows;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Dialogs;

public partial class MockSetupDialog : Window
{
    public NxpscCardType CardType { get; private set; }
    public byte[] CardUid { get; private set; } = [];
    public NxpscCmdSet CmdSet { get; private set; }

    public MockSetupDialog()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        TypeBox.Select(NxpscCardType.DesfireEv3);
        CmdSetBox.Select(NxpscCmdSet.NativeIso);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyTitleBar(this);

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try { CardUid = Hex.ParseBytes(UidBox.Text, "UID", 7); }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, "Mock card", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CardType = TypeBox.Selected<NxpscCardType>();
        CmdSet = CmdSetBox.Selected<NxpscCmdSet>();
        DialogResult = true;
    }
}
