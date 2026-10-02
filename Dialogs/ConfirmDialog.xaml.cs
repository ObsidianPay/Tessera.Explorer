using System.Windows;
using Tessera.Explorer.Services;

namespace Tessera.Explorer.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, bool danger, bool showCancel)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        Message.Text = message;
        OkButton.Content = confirmText;
        if (!danger)
        {
            OkButton.Style = (Style)FindResource("PrimaryButton");
            Glyph.Text = "";
            Glyph.Foreground = (System.Windows.Media.Brush)FindResource("Primary");
        }
        if (!showCancel) CancelButton.Visibility = Visibility.Collapsed;
        Owner = Application.Current.MainWindow;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyTitleBar(this);
    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
