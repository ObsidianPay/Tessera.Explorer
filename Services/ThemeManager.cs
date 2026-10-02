using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Tessera.Explorer.Services;

public enum AppTheme { System, Light, Dark }

public static class ThemeManager
{
    private static ResourceDictionary? _current;
    public static AppTheme Mode { get; private set; } = AppTheme.System;
    public static bool IsDark { get; private set; }

    public static void Apply(AppTheme mode)
    {
        Mode = mode;
        IsDark = mode == AppTheme.Dark || (mode == AppTheme.System && SystemUsesDark());
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/Tokens.{(IsDark ? "Dark" : "Light")}.xaml")
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        merged.Insert(0, dict);
        _current = dict;
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
    }

    private static bool SystemUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void ApplyTitleBar(Window w)
    {
        var handle = new WindowInteropHelper(w).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }
}
