using System.Windows;
using Tessera.Explorer.Services;

namespace Tessera.Explorer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args.ToList();
        var t = args.IndexOf("--theme");
        ThemeManager.Apply(t >= 0 && t + 1 < args.Count && Enum.TryParse<AppTheme>(args[t + 1], true, out var theme) ? theme : AppTheme.System);
        DispatcherUnhandledException += (_, args) =>
        {
            OutputLog.Error(args.Exception.GetType().Name, args.Exception.Message);
            args.Handled = true;
        };
        try
        {
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tessera-explorer-crash.txt"), ex.ToString()); } catch { }
            MessageBox.Show(ex.ToString(), "Tessera Explorer failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
