using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;

namespace DesktopCompanion.App;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Closing += OnAppWindowClosing;
        UptimeValue.Text = FormatUptime();
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    public void ShowSettingsFromTray()
    {
        ShowFromTray();
        NavigateTo("Settings");
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        // Closing the main window should hide it, not stop background services.
        args.Cancel = true;
        sender.Hide();
    }

    private static string FormatUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var days = (int)uptime.TotalDays;
        return days > 0
            ? $"{days}d {uptime.Hours}h"
            : $"{uptime.Hours}h {uptime.Minutes}m";
    }

    private void OnNavigationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
        {
            NavigateTo(page);
        }
    }

    private void NavigateTo(string page)
    {
        PageTitle.Text = page;
        var isOverview = page == "Overview";
        OverviewContent.Visibility = isOverview ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isOverview ? Visibility.Collapsed : Visibility.Visible;

        PageSubtitle.Text = page switch
        {
            "Overview" => "Your PC and Home Assistant, in one place.",
            "Home Assistant" => "Connect your smart home and control entities.",
            "Sensors" => "Discover and configure Windows telemetry.",
            "Actions" => "Build actions that work across Windows and Home Assistant.",
            "Dashboards" => "Create your full and tray dashboards.",
            "Applications & Media" => "Manage your applications and media controls.",
            "Notifications" => "Choose which events appear on your desktop.",
            "Settings" => "Configure Desktop Companion.",
            _ => "Desktop Companion"
        };

        PlaceholderTitle.Text = page;
        PlaceholderDescription.Text = $"{page} is part of the implementation roadmap. The navigation shell is in place; this feature will be built as a complete vertical slice.";
    }
}
