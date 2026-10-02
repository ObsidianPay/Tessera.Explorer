using System.IO;
using Tessera.Explorer.Services;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Tessera.Explorer;

public enum DockSide { Top, Bottom, Left, Right }

/// <summary>Where the output panel lives: dragged by its header or chosen from View > Dock output, and remembered.</summary>
public partial class MainWindow
{
    private sealed record LayoutSettings(DockSide Dock = DockSide.Bottom, bool Visible = true, double Height = 220, double Width = 560);

    private static readonly string LayoutPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tessera.Explorer", "layout.json");

    private DockSide _dock = DockSide.Bottom;
    private bool _outputVisible = true;
    private double _outputHeight = 220, _outputWidth = 560;
    private bool _laidOut;
    private DockSide _laidOutDock;

    private bool _pressed, _dragging;
    private Point _dragStart;
    private DockSide? _dropTarget;

    private bool IsHorizontalDock => _dock is DockSide.Top or DockSide.Bottom;

    // ---- persistence ------------------------------------------------------------------------------

    private void LoadLayout()
    {
        try
        {
            if (File.Exists(LayoutPath) && JsonSerializer.Deserialize<LayoutSettings>(File.ReadAllText(LayoutPath)) is { } l)
            {
                _dock = Enum.IsDefined(l.Dock) ? l.Dock : DockSide.Bottom;
                _outputVisible = l.Visible;
                _outputHeight = Math.Clamp(l.Height, 90, 2000);
                _outputWidth = Math.Clamp(l.Width, 260, 3000);
            }
        }
        catch
        {
            // a bad settings file just means defaults
        }
    }

    private void SaveLayout()
    {
        try
        {
            CaptureSize();
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            File.WriteAllText(LayoutPath, JsonSerializer.Serialize(new LayoutSettings(_dock, _outputVisible, _outputHeight, _outputWidth)));
        }
        catch
        {
            // not worth failing a close over
        }
    }

    // ---- layout -------------------------------------------------------------------------------------

    /// <summary>Remember how big the panel currently is, for the orientation it is laid out in.</summary>
    private void CaptureSize()
    {
        if (!_laidOut || OutputPanel.Visibility != Visibility.Visible) return;
        // measured in the orientation it is currently laid out in, not the one being switched to
        if (_laidOutDock is DockSide.Top or DockSide.Bottom) { if (OutputPanel.ActualHeight > 90) _outputHeight = OutputPanel.ActualHeight; }
        else if (OutputPanel.ActualWidth > 260) _outputWidth = OutputPanel.ActualWidth;
    }

    private void ApplyDock()
    {
        CaptureSize();
        _laidOut = true;
        _laidOutDock = _dock;

        var show = _outputVisible;
        if (ActualHeight > 0) _outputHeight = Math.Clamp(_outputHeight, 90, Math.Max(120, ActualHeight * 0.6));
        if (ActualWidth > 0) _outputWidth = Math.Clamp(_outputWidth, 260, Math.Max(300, ActualWidth * 0.6));
        OutputPanel.Visibility = Splitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        Workspace.RowDefinitions.Clear();
        Workspace.ColumnDefinitions.Clear();

        var body = new GridLength(1, GridUnitType.Star);
        var auto = GridLength.Auto;

        if (IsHorizontalDock)
        {
            var out1 = new GridLength(show ? _outputHeight : 0);
            var outDef = new RowDefinition { Height = out1, MinHeight = show ? 90 : 0 };
            var bodyDef = new RowDefinition { Height = body, MinHeight = 240 };
            var splitDef = new RowDefinition { Height = auto };
            bool top = _dock == DockSide.Top;
            Workspace.RowDefinitions.Add(top ? outDef : bodyDef);
            Workspace.RowDefinitions.Add(splitDef);
            Workspace.RowDefinitions.Add(top ? bodyDef : outDef);

            Place(top ? OutputPanel : BodyHost, row: 0);
            Place(Splitter, row: 1);
            Place(top ? BodyHost : OutputPanel, row: 2);

            Splitter.Height = 4;
            Splitter.Width = double.NaN;
            Splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            Splitter.VerticalAlignment = VerticalAlignment.Center;
            Splitter.ResizeDirection = GridResizeDirection.Rows;
        }
        else
        {
            var out1 = new GridLength(show ? _outputWidth : 0);
            var outDef = new ColumnDefinition { Width = out1, MinWidth = show ? 260 : 0 };
            var bodyDef = new ColumnDefinition { Width = body, MinWidth = 560 };
            var splitDef = new ColumnDefinition { Width = auto };
            bool left = _dock == DockSide.Left;
            Workspace.ColumnDefinitions.Add(left ? outDef : bodyDef);
            Workspace.ColumnDefinitions.Add(splitDef);
            Workspace.ColumnDefinitions.Add(left ? bodyDef : outDef);

            Place(left ? OutputPanel : BodyHost, col: 0);
            Place(Splitter, col: 1);
            Place(left ? BodyHost : OutputPanel, col: 2);

            Splitter.Width = 4;
            Splitter.Height = double.NaN;
            Splitter.VerticalAlignment = VerticalAlignment.Stretch;
            Splitter.HorizontalAlignment = HorizontalAlignment.Center;
            Splitter.ResizeDirection = GridResizeDirection.Columns;
        }

        // the drop hint floats over everything
        Grid.SetRow(DropHint, 0);
        Grid.SetColumn(DropHint, 0);
        Grid.SetRowSpan(DropHint, Math.Max(1, Workspace.RowDefinitions.Count));
        Grid.SetColumnSpan(DropHint, Math.Max(1, Workspace.ColumnDefinitions.Count));

        DockTop.IsChecked = _dock == DockSide.Top;
        DockBottom.IsChecked = _dock == DockSide.Bottom;
        DockLeft.IsChecked = _dock == DockSide.Left;
        DockRight.IsChecked = _dock == DockSide.Right;
        OutputToggle.IsChecked = _outputVisible;

        // the header sits above the text in every dock; the panel edge facing the body gets the hairline
        OutputPanel.Margin = new Thickness(0);
        if (IsLoaded) RenderOutput();
    }

    private static void Place(UIElement element, int row = 0, int col = 0)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, col);
        Grid.SetRowSpan(element, 1);
        Grid.SetColumnSpan(element, 1);
    }

    public void DockOutput(DockSide side)
    {
        _dock = side;
        _outputVisible = true;
        ApplyDock();
        SaveLayout();
        OutputLog.Info("layout", $"Output docked {side.ToString().ToLowerInvariant()}.");
    }

    // ---- menu -----------------------------------------------------------------------------------------

    private void Dock_Click(object sender, RoutedEventArgs e)
    {
        var side = Enum.Parse<DockSide>((string)((MenuItem)sender).Tag);
        if (side == _dock && _outputVisible)
        {
            ApplyDock(); // keep the check mark on
            return;
        }
        DockOutput(side);
    }

    private void OutputToggle_Click(object sender, RoutedEventArgs e)
    {
        CaptureSize();
        _outputVisible = OutputToggle.IsChecked;
        ApplyDock();
        SaveLayout();
    }

    // ---- dragging the header ---------------------------------------------------------------------

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragStart = e.GetPosition(Workspace);
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(Workspace);
        if (!_dragging)
        {
            if (Math.Abs(pos.X - _dragStart.X) < 8 && Math.Abs(pos.Y - _dragStart.Y) < 8) return;
            _dragging = true;
            OutputHeader.CaptureMouse();
        }
        UpdateDropHint(pos);
    }

    private void Header_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var target = _dropTarget;
        var wasDragging = _dragging;
        EndDrag();
        if (wasDragging && target is { } side && side != _dock) DockOutput(side);
    }

    private void Header_LostCapture(object sender, MouseEventArgs e) => EndDrag();

    private void EndDrag()
    {
        _pressed = false;
        _dragging = false;
        _dropTarget = null;
        DropHint.Visibility = Visibility.Collapsed;
        if (OutputHeader.IsMouseCaptured) OutputHeader.ReleaseMouseCapture();
    }

    private void UpdateDropHint(Point pos)
    {
        double w = Math.Max(1, Workspace.ActualWidth), h = Math.Max(1, Workspace.ActualHeight);
        double left = pos.X / w, right = 1 - pos.X / w, top = pos.Y / h, bottom = 1 - pos.Y / h;
        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        var side = min == top ? DockSide.Top : min == bottom ? DockSide.Bottom : min == left ? DockSide.Left : DockSide.Right;
        _dropTarget = side;

        DropHint.Width = double.NaN;
        DropHint.Height = double.NaN;
        DropHint.HorizontalAlignment = HorizontalAlignment.Stretch;
        DropHint.VerticalAlignment = VerticalAlignment.Stretch;
        switch (side)
        {
            case DockSide.Top: DropHint.VerticalAlignment = VerticalAlignment.Top; DropHint.Height = h * 0.35; break;
            case DockSide.Bottom: DropHint.VerticalAlignment = VerticalAlignment.Bottom; DropHint.Height = h * 0.35; break;
            case DockSide.Left: DropHint.HorizontalAlignment = HorizontalAlignment.Left; DropHint.Width = w * 0.35; break;
            case DockSide.Right: DropHint.HorizontalAlignment = HorizontalAlignment.Right; DropHint.Width = w * 0.35; break;
        }
        DropHint.Visibility = Visibility.Visible;
    }
}
