using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.HomeAssistant.Rest;
using DesktopCompanion.HomeAssistant.Authentication;
using DesktopCompanion.HomeAssistant.Services;
using DesktopCompanion.HomeAssistant.WebSocket;
using DesktopCompanion.Windows.Security;
using DesktopCompanion.Windows.Tray;
using Microsoft.UI.Xaml;

namespace DesktopCompanion.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayPopupWindow? _trayPopupWindow;
    private TrayIconHost? _trayIconHost;
    private HomeAssistantTokenProvider? _tokenProvider;
    private HomeAssistantConnectionSupervisor? _connectionSupervisor;
    private IHomeAssistantClient? _homeAssistantClient;
    private Uri? _connectedInstance;
    private bool _isExiting;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_mainWindow is not null)
        {
            ShowDashboard();
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.ConnectHomeAssistantRequested += OnConnectHomeAssistantRequested;
        _mainWindow.DisconnectHomeAssistantRequested += OnDisconnectHomeAssistantRequested;
        _mainWindow.RefreshEntitiesRequested += OnRefreshEntitiesRequested;

        var savedUrl = AppSettingsStore.LoadHomeAssistantUrl();
        if (!string.IsNullOrWhiteSpace(savedUrl))
        {
            _mainWindow.SetHomeAssistantUrl(savedUrl);
            _ = RestoreHomeAssistantConnectionAsync(savedUrl);
        }

        _trayPopupWindow = new TrayPopupWindow();
        _trayPopupWindow.OpenDashboardRequested += (_, _) => ShowDashboard();
        _trayPopupWindow.SettingsRequested += (_, _) => ShowSettings();
        _trayPopupWindow.ExitRequested += (_, _) => _ = ExitApplicationAsync();

        _trayIconHost = new TrayIconHost();
        _trayIconHost.Activated += (_, _) => ToggleTrayPopup();
        _trayIconHost.ContextRequested += (_, _) => ToggleTrayPopup();
        _mainWindow.Activate();
    }

    private async void OnConnectHomeAssistantRequested(Uri instanceUri) =>
        await ConnectHomeAssistantAsync(instanceUri, authenticate: true);

    private async Task ConnectHomeAssistantAsync(Uri instanceUri, bool authenticate)
    {
        if (_isExiting || _mainWindow is null) return;

        _mainWindow.UpdateConnectionStatus(
            authenticate ? "Waiting for sign-in" : "Restoring connection",
            authenticate ? "Complete the approval in your browser. This window will update when you return." : "Refreshing your saved Home Assistant session.",
            false,
            busy: true);
        _trayPopupWindow?.UpdateHomeAssistantStatus(
            authenticate ? "Signing in…" : "Connecting…",
            "Home Assistant", false, busy: true);

        try
        {
            await StopHomeAssistantAsync();
            _tokenProvider = new HomeAssistantTokenProvider(instanceUri, new WindowsCredentialStore());

            if (authenticate)
            {
                await _tokenProvider.AuthenticateAsync();
            }

            _connectedInstance = instanceUri;
            AppSettingsStore.SaveHomeAssistantUrl(instanceUri.AbsoluteUri);

            _homeAssistantClient = new HomeAssistantRestClient(new HttpClient());
            _connectionSupervisor = new HomeAssistantConnectionSupervisor(new HomeAssistantWebSocketClient());
            _connectionSupervisor.StateChanged += (_, state) =>
            {
                var connected = state.Status == HomeAssistantConnectionStatus.Connected;
                var statusTitle = state.Status switch
                    {
                        HomeAssistantConnectionStatus.Connected => "Connected",
                        HomeAssistantConnectionStatus.Connecting => "Connecting",
                        HomeAssistantConnectionStatus.Reconnecting => "Reconnecting",
                        HomeAssistantConnectionStatus.AuthenticationRequired => "Sign-in required",
                        HomeAssistantConnectionStatus.Faulted => "Connection error",
                        _ => "Disconnected"
                    };
                var statusDetail = state.Message ?? "Home Assistant is not connected.";
                var isBusy = state.Status is HomeAssistantConnectionStatus.Connecting or HomeAssistantConnectionStatus.Reconnecting;
                _mainWindow?.UpdateConnectionStatus(statusTitle, statusDetail, connected, busy: isBusy);
                _trayPopupWindow?.UpdateHomeAssistantStatus(statusTitle, statusDetail, connected, busy: isBusy);
                if (connected) _ = RefreshEntitiesAsync();
            };

            await _connectionSupervisor.StartAsync(instanceUri, _tokenProvider.GetAccessTokenAsync);
            _ = RefreshEntitiesAsync();
        }
        catch (OperationCanceledException)
        {
            _mainWindow.UpdateConnectionStatus("Sign-in cancelled", "You can try connecting again at any time.", false);
            _trayPopupWindow?.UpdateHomeAssistantStatus("Not connected", "Connect in the app", false);
            await StopHomeAssistantAsync();
        }
        catch (Exception exception)
        {
            _mainWindow.UpdateConnectionStatus("Could not connect", exception.Message, false);
            _trayPopupWindow?.UpdateHomeAssistantStatus("Could not connect", exception.Message, false);
            await StopHomeAssistantAsync();
        }
    }

    private async Task RestoreHomeAssistantConnectionAsync(string savedUrl)
    {
        if (!Uri.TryCreate(savedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _mainWindow?.UpdateConnectionStatus("Check the instance URL", "The saved Home Assistant URL is invalid.", false);
            return;
        }

        await ConnectHomeAssistantAsync(uri, authenticate: false);
    }

    private async void OnDisconnectHomeAssistantRequested() => await DisconnectHomeAssistantAsync();

    private async void OnRefreshEntitiesRequested() => await RefreshEntitiesAsync();

    private async Task RefreshEntitiesAsync()
    {
        if (_mainWindow is null || _tokenProvider is null || _homeAssistantClient is null || _connectedInstance is null)
        {
            _mainWindow?.UpdateEntities(Array.Empty<HomeAssistantEntity>(), "Connect Home Assistant to discover your entities.");
            return;
        }

        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync();
            var entities = await _homeAssistantClient.GetEntitiesAsync(_connectedInstance, token);
            _mainWindow.UpdateEntities(entities);
        }
        catch (UnauthorizedAccessException)
        {
            _mainWindow.UpdateEntities(Array.Empty<HomeAssistantEntity>(), "Sign in to Home Assistant to load entities.");
        }
        catch (Exception exception)
        {
            _mainWindow.UpdateEntities(Array.Empty<HomeAssistantEntity>(), $"Could not load entities: {exception.Message}");
        }
    }

    private async Task DisconnectHomeAssistantAsync()
    {
        await StopHomeAssistantAsync(clearCredentials: true);
        _connectedInstance = null;
        AppSettingsStore.SaveHomeAssistantUrl(string.Empty);
        _mainWindow?.UpdateConnectionStatus("Disconnected", "The saved Home Assistant sign-in has been removed from this PC.", false);
        _trayPopupWindow?.UpdateHomeAssistantStatus("Not connected", "Connect in the app", false);
    }

    private async Task StopHomeAssistantAsync(bool clearCredentials = false)
    {
        if (_connectionSupervisor is not null)
        {
            await _connectionSupervisor.StopAsync();
            await _connectionSupervisor.DisposeAsync();
            _connectionSupervisor = null;
        }

        if (_homeAssistantClient is IDisposable restClientDisposable)
        {
            restClientDisposable.Dispose();
            _homeAssistantClient = null;
        }

        if (_tokenProvider is not null)
        {
            if (clearCredentials) _tokenProvider.Disconnect();
            _tokenProvider.Dispose();
            _tokenProvider = null;
        }
    }

    private void ToggleTrayPopup()
    {
        if (_isExiting || _trayPopupWindow is null) return;
        _trayPopupWindow.ToggleNearCursor();
    }

    private void ShowDashboard()
    {
        if (_isExiting || _mainWindow is null) return;
        _trayPopupWindow?.AppWindow.Hide();
        _mainWindow.ShowFromTray();
    }

    private void ShowSettings()
    {
        if (_isExiting || _mainWindow is null) return;
        _trayPopupWindow?.AppWindow.Hide();
        _mainWindow.ShowSettingsFromTray();
    }

    private async Task ExitApplicationAsync()
    {
        if (_isExiting) return;
        _isExiting = true;
        await StopHomeAssistantAsync();
        _trayIconHost?.Dispose();
        _trayIconHost = null;
        _trayPopupWindow?.Close();
        _trayPopupWindow = null;
        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        _mainWindow = null;
        Exit();
    }
}
