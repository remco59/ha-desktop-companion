using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.Web.WebView2.Core;
using Windows.UI;

namespace DesktopCompanion.App;

public sealed partial class MainWindow : Window
{
    private readonly string _webViewUserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopCompanion",
        "WebView2");

    private bool _allowClose;
    private bool _webViewInitialized;
    private bool _webViewInitializing;
    private Uri? _dashboardUri;
    private Uri? _lastNavigatedDashboardUri;
    private string _currentPage = "Overview";

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Closing += OnAppWindowClosing;
        UptimeValue.Text = FormatUptime();
        SetNavigationSelection(_currentPage);
    }

    public event Action<Uri>? ConnectHomeAssistantRequested;
    public event Action? DisconnectHomeAssistantRequested;

    public void SetHomeAssistantUrl(string url) => HomeAssistantUrlInput.Text = url;

    /// <summary>
    /// Sets the Home Assistant frontend URL. The embedded browser keeps its own
    /// persistent profile and never receives the native OAuth access/refresh tokens.
    /// </summary>
    public void SetDashboardUri(Uri? uri)
    {
        if (uri is not null &&
            (!uri.IsAbsoluteUri ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("The Home Assistant dashboard URL must use HTTP or HTTPS.", nameof(uri));
        }

        _dashboardUri = uri is null ? null : EnsureTrailingSlash(uri);
        _lastNavigatedDashboardUri = null;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateDashboardAvailability();
            if (_currentPage == "Dashboard" && _dashboardUri is not null)
            {
                _ = LoadDashboardAsync();
            }
        });
    }

    public void NavigateToDashboard() => NavigateTo("Dashboard");

    public async Task ClearDashboardSessionAsync()
    {
        if (!_webViewInitialized) return;

        await HomeAssistantWebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
            CoreWebView2BrowsingDataKinds.AllProfile);
        HomeAssistantWebView.CoreWebView2.Navigate("about:blank");
        _lastNavigatedDashboardUri = null;
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    public void ShowSettingsFromTray()
    {
        ShowFromTray();
        NavigateTo("Home Assistant");
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
            SidebarConnectionText.Text = connected ? "Connected" : busy ? "Connecting…" : title;

            var statusColor = connected
                ? Color.FromArgb(255, 76, 175, 80)
                : Color.FromArgb(255, 224, 169, 77);
            ConnectionStatusDot.Fill = new SolidColorBrush(statusColor);
            SidebarConnectionDot.Fill = new SolidColorBrush(statusColor);

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
        if (sender is Button { Tag: string page })
        {
            NavigateTo(page);
        }
    }

    private void OnConnectHomeAssistantClicked(object sender, RoutedEventArgs e)
    {
        var value = HomeAssistantUrlInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            UpdateConnectionStatus(
                "Check the instance URL",
                "Enter a complete URL beginning with http:// or https://.",
                connected: false);
            return;
        }

        ConnectHomeAssistantRequested?.Invoke(EnsureTrailingSlash(uri));
    }

    private void OnDisconnectHomeAssistantClicked(object sender, RoutedEventArgs e) =>
        DisconnectHomeAssistantRequested?.Invoke();

    private void OnOpenConnectionSettingsClicked(object sender, RoutedEventArgs e) =>
        NavigateTo("Home Assistant");

    private async void OnRefreshDashboardClicked(object sender, RoutedEventArgs e)
    {
        if (_dashboardUri is null)
        {
            NavigateTo("Home Assistant");
            return;
        }

        try
        {
            await EnsureDashboardWebViewAsync();
            HomeAssistantWebView.CoreWebView2.Reload();
            DashboardStatusText.Text = "Refreshing Home Assistant…";
        }
        catch (Exception exception)
        {
            ShowDashboardError(exception);
        }
    }

    private void OnOpenDashboardInBrowserClicked(object sender, RoutedEventArgs e)
    {
        if (_dashboardUri is null) return;

        try
        {
            var url = HomeAssistantWebView.CoreWebView2?.Source ?? _dashboardUri.AbsoluteUri;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            DashboardStatusText.Text = $"Could not open the browser: {exception.Message}";
        }
    }

    private async Task LoadDashboardAsync()
    {
        if (_dashboardUri is null)
        {
            UpdateDashboardAvailability();
            return;
        }

        if (_webViewInitializing) return;

        if (_webViewInitialized &&
            _lastNavigatedDashboardUri is not null &&
            Uri.Compare(_lastNavigatedDashboardUri, _dashboardUri,
                UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            return;
        }

        try
        {
            DashboardUnavailableOverlay.Visibility = Visibility.Collapsed;
            HomeAssistantWebView.Visibility = Visibility.Visible;
            DashboardStatusText.Text = "Preparing Home Assistant…";

            await EnsureDashboardWebViewAsync();

            var target = AddKioskModeParameter(_dashboardUri).AbsoluteUri;
            _lastNavigatedDashboardUri = _dashboardUri;
            DashboardStatusText.Text = "Loading your Home Assistant dashboard…";
            HomeAssistantWebView.CoreWebView2.Navigate(target);
        }
        catch (Exception exception)
        {
            ShowDashboardError(exception);
        }
    }

    private async Task EnsureDashboardWebViewAsync()
    {
        if (_webViewInitialized) return;
        if (_webViewInitializing)
        {
            // There is only one XAML/UI thread. Yield briefly through the dispatcher
            // rather than starting a second WebView2 initialization.
            while (_webViewInitializing && !_webViewInitialized)
            {
                await Task.Delay(50);
            }

            if (_webViewInitialized) return;
        }

        _webViewInitializing = true;
        try
        {
            Directory.CreateDirectory(_webViewUserDataFolder);

            // Fail early with a clear UI message if the Evergreen WebView2 Runtime
            // is missing. Windows 11 normally includes it.
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                _webViewUserDataFolder,
                EnvironmentVariableTarget.Process);
            await HomeAssistantWebView.EnsureCoreWebView2Async();

            var core = HomeAssistantWebView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;

            core.NavigationStarting += (_, args) =>
            {
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var navigationUri) ||
                    (navigationUri.Scheme != Uri.UriSchemeHttp && navigationUri.Scheme != Uri.UriSchemeHttps))
                {
                    args.Cancel = true;
                }
            };

            core.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    var title = core.DocumentTitle;
                    DashboardStatusText.Text = string.IsNullOrWhiteSpace(title)
                        ? "Home Assistant"
                        : title;
                }
                else
                {
                    DashboardStatusText.Text = $"Home Assistant page could not be loaded ({args.WebErrorStatus}).";
                }
            };

            core.DocumentTitleChanged += (_, _) =>
            {
                var title = core.DocumentTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    DashboardStatusText.Text = title;
                }
            };

            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var requestedUri) ||
                    (requestedUri.Scheme != Uri.UriSchemeHttp && requestedUri.Scheme != Uri.UriSchemeHttps))
                {
                    return;
                }

                // Keep Home Assistant-origin pages inside the app. Open external
                // destinations in the user's default browser.
                if (_dashboardUri is not null && IsSameOrigin(requestedUri, _dashboardUri))
                {
                    core.Navigate(requestedUri.AbsoluteUri);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(requestedUri.AbsoluteUri) { UseShellExecute = true });
                }
            };

            _webViewInitialized = true;
        }
        finally
        {
            _webViewInitializing = false;
        }
    }

    private void ShowDashboardError(Exception exception)
    {
        HomeAssistantWebView.Visibility = Visibility.Collapsed;
        DashboardUnavailableOverlay.Visibility = Visibility.Visible;
        DashboardUnavailableTitle.Text = "Dashboard could not be opened";
        var details = exception.GetType().Name.Contains("RuntimeNotFound", StringComparison.OrdinalIgnoreCase)
            ? "The Microsoft Edge WebView2 Runtime is missing. Install the Evergreen WebView2 Runtime and try again."
            : exception.Message;
        DashboardUnavailableDetail.Text = details;
        DashboardStatusText.Text = "Home Assistant dashboard unavailable";
    }

    private void UpdateDashboardAvailability()
    {
        if (_dashboardUri is null)
        {
            HomeAssistantWebView.Visibility = Visibility.Collapsed;
            DashboardUnavailableOverlay.Visibility = Visibility.Visible;
            DashboardUnavailableTitle.Text = "Connect Home Assistant";
            DashboardUnavailableDetail.Text =
                "Add your Home Assistant instance first. Its native dashboard will appear here.";
            DashboardStatusText.Text = "No Home Assistant instance configured";
            return;
        }

        DashboardUnavailableOverlay.Visibility = Visibility.Collapsed;
        HomeAssistantWebView.Visibility = Visibility.Visible;
        DashboardStatusText.Text = "Ready to load your Home Assistant dashboard";
    }

    /// <summary>
    /// Requests a dashboard-only presentation from the Home Assistant Kiosk Mode
    /// frontend resource. If that resource is not installed, Home Assistant simply
    /// ignores the query parameter and renders its regular frontend.
    /// </summary>
    private static Uri AddKioskModeParameter(Uri dashboardUri)
    {
        var builder = new UriBuilder(dashboardUri);
        var query = builder.Query.TrimStart('?');
        var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

        var hasKioskParameter = parameters.Any(parameter =>
            string.Equals(
                Uri.UnescapeDataString(parameter.Split('=', 2)[0]),
                "kiosk",
                StringComparison.OrdinalIgnoreCase));

        // Respect an explicit opt-out included in the configured instance URL.
        var hasKioskModeOptOut = parameters.Any(parameter =>
            string.Equals(
                Uri.UnescapeDataString(parameter.Split('=', 2)[0]),
                "disable_km",
                StringComparison.OrdinalIgnoreCase));

        if (hasKioskParameter || hasKioskModeOptOut)
        {
            return dashboardUri;
        }

        builder.Query = string.IsNullOrEmpty(query) ? "kiosk" : $"{query}&kiosk";
        return builder.Uri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var absolute = uri.AbsoluteUri;
        return absolute.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(absolute + "/", UriKind.Absolute);
    }

    private static bool IsSameOrigin(Uri candidate, Uri trusted)
    {
        return string.Equals(candidate.Scheme, trusted.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase) &&
               candidate.Port == trusted.Port;
    }

    private void NavigateTo(string page)
    {
        _currentPage = page;
        PageTitle.Text = page;
        var isOverview = page == "Overview";
        var isHomeAssistant = page == "Home Assistant";
        var isDashboard = page == "Dashboard";
        var isPlaceholder = !isOverview && !isHomeAssistant && !isDashboard;

        ScrollablePageContent.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;
        OverviewContent.Visibility = isOverview ? Visibility.Visible : Visibility.Collapsed;
        HomeAssistantContent.Visibility = isHomeAssistant ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isPlaceholder ? Visibility.Visible : Visibility.Collapsed;
        DashboardContent.Visibility = isDashboard ? Visibility.Visible : Visibility.Collapsed;

        PageSubtitle.Text = page switch
        {
            "Overview" => "Your PC and Home Assistant, in one place.",
            "Home Assistant" => "Connection and account settings for your smart home.",
            "Dashboard" => "Your Home Assistant dashboard, without its navigation chrome.",
            "Sensors" => "Discover and configure Windows telemetry.",
            "Actions" => "Build actions that work across Windows and Home Assistant.",
            "Applications & Media" => "Manage your applications and media controls.",
            "Notifications" => "Choose which events appear on your desktop.",
            "Settings" => "Configure Desktop Companion.",
            _ => "Desktop Companion"
        };

        SetNavigationSelection(page);

        if (isDashboard)
        {
            UpdateDashboardAvailability();
            if (_dashboardUri is not null)
            {
                _ = LoadDashboardAsync();
            }
        }

        if (isPlaceholder)
        {
            PlaceholderTitle.Text = page;
            PlaceholderDescription.Text =
                $"{page} is part of the implementation roadmap. The native Windows shell is in place; this feature will be built as a complete vertical slice.";
        }
    }

    private void SetNavigationSelection(string selectedPage)
    {
        SetNavigationButton(OverviewNavigationButton, selectedPage == "Overview");
        SetNavigationButton(DashboardNavigationButton, selectedPage == "Dashboard");
        SetNavigationButton(HomeAssistantNavigationButton, selectedPage == "Home Assistant");
        SetNavigationButton(SensorsNavigationButton, selectedPage == "Sensors");
        SetNavigationButton(ActionsNavigationButton, selectedPage == "Actions");
        SetNavigationButton(ApplicationsNavigationButton, selectedPage == "Applications & Media");
        SetNavigationButton(NotificationsNavigationButton, selectedPage == "Notifications");
        SetNavigationButton(SettingsNavigationButton, selectedPage == "Settings");
    }

    private static void SetNavigationButton(Button button, bool selected)
    {
        button.Background = new SolidColorBrush(Color.FromArgb(
            255,
            selected ? (byte)18 : (byte)23,
            selected ? (byte)59 : (byte)26,
            selected ? (byte)75 : (byte)31));
        button.Foreground = new SolidColorBrush(selected
            ? Colors.White
            : Color.FromArgb(255, 223, 227, 232));
    }
}
