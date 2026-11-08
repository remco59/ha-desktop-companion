using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace DesktopCompanion.App;

public sealed partial class TrayPopupWindow : Window
{
    private const int PopupWidth = 368;
    private const int PopupHeight = 440;

    public event EventHandler? OpenDashboardRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayPopupWindow()
    {
        InitializeComponent();

        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.Resize(new SizeInt32(PopupWidth, PopupHeight));

        ComputerNameText.Text = Environment.MachineName;
        UpdateUptime();

        Activated += OnWindowActivated;
        RootGrid.Loaded += (_, _) => RootGrid.Focus(FocusState.Programmatic);
    }

    public void ToggleNearCursor()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
            return;
        }

        ShowNearCursor();
    }

    public void ShowNearCursor()
    {
        UpdateUptime();

        if (!GetCursorPos(out var cursor))
        {
            cursor = new NativePoint(0, 0);
        }

        var cursorPoint = new PointInt32(cursor.X, cursor.Y);
        var displayArea = DisplayArea.GetFromPoint(cursorPoint, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;

        var left = cursor.X - (PopupWidth / 2);
        var top = cursor.Y < workArea.Y + (workArea.Height / 2)
            ? cursor.Y + 12
            : cursor.Y - PopupHeight - 12;

        left = Math.Clamp(left, workArea.X, workArea.X + workArea.Width - PopupWidth);
        top = Math.Clamp(top, workArea.Y, workArea.Y + workArea.Height - PopupHeight);

        AppWindow.MoveAndResize(
            new RectInt32(left, top, PopupWidth, PopupHeight),
            displayArea);
        AppWindow.Show();
        Activate();
    }

    private void UpdateUptime()
    {
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var days = (int)elapsed.TotalDays;
        UptimeText.Text = days > 0
            ? $"{days}d {elapsed.Hours}h"
            : $"{elapsed.Hours}h {elapsed.Minutes}m";
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            AppWindow.Hide();
        }
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape)
        {
            AppWindow.Hide();
            args.Handled = true;
        }
    }

    private void OnOpenDashboardClicked(object sender, RoutedEventArgs args)
    {
        AppWindow.Hide();
        OpenDashboardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs args)
    {
        AppWindow.Hide();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitClicked(object sender, RoutedEventArgs args)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
