using DesktopCompanion.Core.Models;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.UI;

namespace DesktopCompanion.App;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private IReadOnlyList<HomeAssistantEntity> _entities = Array.Empty<HomeAssistantEntity>();

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Closing += OnAppWindowClosing;
        UptimeValue.Text = FormatUptime();
    }

    public event Action<Uri>? ConnectHomeAssistantRequested;
    public event Action? DisconnectHomeAssistantRequested;
    public event Action? RefreshEntitiesRequested;

    public void SetHomeAssistantUrl(string url) => HomeAssistantUrlInput.Text = url;

    public void UpdateEntities(IReadOnlyList<HomeAssistantEntity> entities, string? message = null)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _entities = entities;
            ApplyEntityFilter();
            EntityEmptyText.Text = message ?? (entities.Count == 0
                ? "No entities were returned by Home Assistant."
                : string.Empty);
            EntityEmptyText.Visibility = entities.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
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

    public void AllowClose() => _allowClose = true;

    public void UpdateConnectionStatus(string title, string detail, bool connected, bool busy = false)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ConnectionStatusTitle.Text = title;
            OverviewConnectionTitle.Text = title;
            OverviewConnectionDetail.Text = detail;
            ConnectionStatusDetail.Text = detail;
            ConnectionStatusDot.Fill = new SolidColorBrush(
                connected ? Color.FromArgb(255, 98, 214, 174) :
                busy ? Color.FromArgb(255, 224, 169, 77) :
                Color.FromArgb(255, 224, 169, 77));
            ConnectHomeAssistantButton.IsEnabled = !busy;
            ConnectHomeAssistantButton.Content = busy ? "Connecting…" : "Connect Home Assistant";
            DisconnectHomeAssistantButton.IsEnabled = connected;
        });
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose) return;
        args.Cancel = true;
        sender.Hide();
    }

    private static string FormatUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var days = (int)uptime.TotalDays;
        return days > 0 ? $"{days}d {uptime.Hours}h" : $"{uptime.Hours}h {uptime.Minutes}m";
    }

    private void OnNavigationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) NavigateTo(page);
    }

    private void OnConnectHomeAssistantClicked(object sender, RoutedEventArgs e)
    {
        var value = HomeAssistantUrlInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            UpdateConnectionStatus("Check the instance URL", "Enter a complete URL beginning with http:// or https://.", false);
            return;
        }

        ConnectHomeAssistantRequested?.Invoke(uri);
    }

    private void OnDisconnectHomeAssistantClicked(object sender, RoutedEventArgs e) =>
        DisconnectHomeAssistantRequested?.Invoke();

    private void OnRefreshEntitiesClicked(object sender, RoutedEventArgs e) => RefreshEntitiesRequested?.Invoke();

    private void OnEntitySearchChanged(object sender, TextChangedEventArgs e) => ApplyEntityFilter();

    private void ApplyEntityFilter()
    {
        var query = EntitySearchInput.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _entities
            : _entities.Where(entity =>
                entity.EntityId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (entity.FriendlyName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entity.Domain?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();

        EntityListView.ItemsSource = filtered;
        EntityCountText.Text = filtered.Count == _entities.Count
            ? $"{_entities.Count} entities"
            : $"{filtered.Count} of {_entities.Count} entities";
    }

    private void NavigateTo(string page)
    {
        PageTitle.Text = page;
        var isOverview = page == "Overview";
        var isHomeAssistant = page == "Home Assistant";
        OverviewContent.Visibility = isOverview ? Visibility.Visible : Visibility.Collapsed;
        HomeAssistantContent.Visibility = isHomeAssistant ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isOverview || isHomeAssistant ? Visibility.Collapsed : Visibility.Visible;

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
