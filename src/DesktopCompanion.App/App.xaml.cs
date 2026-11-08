using DesktopCompanion.Core.Models;
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

            _connectionSupervisor = new HomeAssistantConnectionSupervisor(new HomeAssistantWebSocketClient());
            _connectionSupervisor.StateChanged += (_, state) =>
            {
                var connected = state.Status == HomeAssistantConnectionStatus.Connected;
                _mainWindow?.UpdateConnectionStatus(
                    state.Status switch
                    {
                        HomeAssistantConnectionStatus.Connected => "Connected",
                        HomeAssistantConnectionStatus.Connecting => "Connecting",
                        HomeAssistantConnectionStatus.Reconnecting => "Reconnecting",
                        HomeAssistantConnectionStatus.AuthenticationRequired => "Sign-in required",
                        HomeAssistantConnectionStatus.Faulted => "Connection error",
                        _ => "Disconnected"
                    },
                    state.Message ?? "Home Assistant is not connected.",
                    connected,
                    busy: state.Status is HomeAssistantConnectionStatus.Connecting or HomeAssistantConnectionStatus.Reconnecting);
            };

            await _connectionSupervisor.StartAsync(instanceUri, _tokenProvider.GetAccessTokenAsync);
        }
        catch (OperationCanceledException)
        {
            _mainWindow.UpdateConnectionStatus("Sign-in cancelled", "You can try connecting again at any time.", false);
            await StopHomeAssistantAsync();
        }
        catch (Exception exception)
        {
            _mainWindow.UpdateConnectionStatus("Could not connect", exception.Message, false);
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

    private async Task DisconnectHomeAssistantAsync()
    {
        await StopHomeAssistantAsync(clearCredentials: true);
        _connectedInstance = null;
        AppSettingsStore.SaveHomeAssistantUrl(string.Empty);
        _mainWindow?.UpdateConnectionStatus("Disconnected", "The saved Home Assistant sign-in has been removed from this PC.", false);
    }

    private async Task StopHomeAssistantAsync(bool clearCredentials = false)
    {
        if (_connectionSupervisor is not null)
        {
            await _connectionSupervisor.StopAsync();
            await _connectionSupervisor.DisposeAsync();
            _connectionSupervisor = null;
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
