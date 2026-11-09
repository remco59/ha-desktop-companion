using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using DesktopCompanion.HomeAssistant.Authentication;
using DesktopCompanion.HomeAssistant.Services;
using DesktopCompanion.HomeAssistant.WebSocket;
using DesktopCompanion.Windows.Applications;
using DesktopCompanion.Windows.Audio;
using DesktopCompanion.Windows.Security;
using DesktopCompanion.Windows.Sensors;
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
    private IHomeAssistantTelemetryClient? _telemetryClient;
    private IAudioControlService? _audioControlService;
    private IApplicationCatalogService? _applicationCatalogService;
    private IMediaSessionService? _mediaSessionService;
    private WindowsSensorMonitor? _sensorMonitor;
    private Task? _sensorMonitorStartTask;
    private SensorSample? _latestSensorSample;
    private Uri? _connectedInstance;
    private string? _pairedDeviceId;
    private bool _homeAssistantConnected;
    private bool _isExiting;
    private string? _lastTelemetryError;
    private readonly SemaphoreSlim _telemetryPublishLock = new(1, 1);
    private readonly SemaphoreSlim _telemetryPairLock = new(1, 1);

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
        _mainWindow.PairSensorsRequested += OnPairSensorsRequested;
        _mainWindow.UnpairSensorsRequested += OnUnpairSensorsRequested;
        _mainWindow.MasterVolumeChangeRequested += OnMasterVolumeChangeRequested;
        _mainWindow.AudioMuteChangeRequested += OnAudioMuteChangeRequested;
        _mainWindow.AudioRefreshRequested += OnAudioRefreshRequested;
        _mainWindow.ApplicationsRefreshRequested += OnApplicationsRefreshRequested;
        _mainWindow.ApplicationLaunchRequested += OnApplicationLaunchRequested;
        _mainWindow.MediaRefreshRequested += OnMediaRefreshRequested;
        _mainWindow.MediaTogglePlayPauseRequested += OnMediaTogglePlayPauseRequested;
        _mainWindow.MediaPreviousRequested += OnMediaPreviousRequested;
        _mainWindow.MediaNextRequested += OnMediaNextRequested;

        _audioControlService = new WindowsAudioControlService();
        _applicationCatalogService = new WindowsStartMenuApplicationCatalogService();
        _mediaSessionService = new WindowsMediaSessionService();
        _telemetryClient = new HomeAssistantTelemetryWebSocketClient();
        _pairedDeviceId = AppSettingsStore.LoadPairedDeviceId();
        _mainWindow.UpdateHomeAssistantPublishingStatus(
            _pairedDeviceId is null
                ? "Not paired · readings stay on this PC"
                : "Paired · waiting for Home Assistant connection",
            paired: _pairedDeviceId is not null,
            connected: false);

        _sensorMonitor = new WindowsSensorMonitor(new WindowsSystemSensorProvider());
        _sensorMonitor.Sampled += HandleSensorSample;
        _sensorMonitorStartTask = StartSensorMonitorAsync();

        var savedUrl = AppSettingsStore.LoadHomeAssistantUrl();
        if (!string.IsNullOrWhiteSpace(savedUrl) &&
            Uri.TryCreate(savedUrl, UriKind.Absolute, out var savedUri) &&
            (savedUri.Scheme == Uri.UriSchemeHttp || savedUri.Scheme == Uri.UriSchemeHttps))
        {
            _mainWindow.SetHomeAssistantUrl(savedUri.AbsoluteUri);
            _mainWindow.SetDashboardUri(savedUri);
            _mainWindow.SetSelectedDashboard(AppSettingsStore.LoadSelectedDashboardId());
            _ = RestoreHomeAssistantConnectionAsync(savedUri);
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

    private async void OnApplicationsRefreshRequested() =>
        await RefreshApplicationCatalogAsync();

    private async void OnApplicationLaunchRequested(string applicationId) =>
        await LaunchApplicationAsync(applicationId);

    private async void OnMediaRefreshRequested() =>
        await RefreshMediaSessionAsync();

    private async void OnMediaTogglePlayPauseRequested() =>
        await RunMediaCommandAsync("playback", service => service.TogglePlayPauseAsync());

    private async void OnMediaPreviousRequested() =>
        await RunMediaCommandAsync("previous track", service => service.SkipPreviousAsync());

    private async void OnMediaNextRequested() =>
        await RunMediaCommandAsync("next track", service => service.SkipNextAsync());

    private async Task RefreshMediaSessionAsync()
    {
        if (_mediaSessionService is null || _mainWindow is null) return;
        try
        {
            var state = await _mediaSessionService.GetCurrentSessionAsync();
            _mainWindow.SetMediaSessionState(state);
        }
        catch (Exception exception)
        {
            _mainWindow.SetMediaSessionError($"Could not read Windows media session: {exception.Message}");
        }
    }

    private async Task RunMediaCommandAsync(
        string action,
        Func<IMediaSessionService, Task<bool>> command)
    {
        if (_mediaSessionService is null || _mainWindow is null) return;
        try
        {
            var succeeded = await command(_mediaSessionService);
            if (!succeeded)
            {
                _mainWindow.SetMediaSessionError(
                    $"Windows could not perform {action}; the active media session may not support it.");
                return;
            }

            await RefreshMediaSessionAsync();
        }
        catch (Exception exception)
        {
            _mainWindow.SetMediaSessionError($"Could not control {action}: {exception.Message}");
        }
    }



    private async Task RefreshApplicationCatalogAsync()
    {
        if (_applicationCatalogService is null || _mainWindow is null) return;
        _mainWindow.SetApplicationCatalogStatus("Discovering Start Menu applications…");
        try
        {
            var applications = await _applicationCatalogService.GetApplicationsAsync();
            _mainWindow.SetInstalledApplications(applications);
        }
        catch (Exception exception)
        {
            _mainWindow.SetApplicationCatalogStatus($"Could not discover applications: {exception.Message}");
        }
    }

    private async Task LaunchApplicationAsync(string applicationId)
    {
        if (_applicationCatalogService is null || _mainWindow is null) return;
        _mainWindow.SetApplicationCatalogStatus("Starting application…");
        try
        {
            await _applicationCatalogService.LaunchAsync(applicationId);
            _mainWindow.SetApplicationCatalogStatus("Windows accepted the application launch request.");
        }
        catch (Exception exception)
        {
            _mainWindow.SetApplicationCatalogStatus($"Could not start the application: {exception.Message}");
        }
    }

    private async void OnAudioRefreshRequested() =>
        await RefreshAudioEndpointAsync();

    private async void OnMasterVolumeChangeRequested(double volumePercent)
    {
        if (_audioControlService is null || _mainWindow is null) return;
        try
        {
            var state = await _audioControlService.SetMasterVolumeAsync(volumePercent);
            _mainWindow.SetAudioEndpointState(state);
        }
        catch (Exception exception)
        {
            _mainWindow.SetAudioControlError($"Could not change Windows volume: {exception.Message}");
        }
    }

    private async void OnAudioMuteChangeRequested(bool muted)
    {
        if (_audioControlService is null || _mainWindow is null) return;
        try
        {
            var state = await _audioControlService.SetMutedAsync(muted);
            _mainWindow.SetAudioEndpointState(state);
        }
        catch (Exception exception)
        {
            _mainWindow.SetAudioControlError($"Could not change the mute state: {exception.Message}");
        }
    }

    private async Task RefreshAudioEndpointAsync()
    {
        if (_audioControlService is null || _mainWindow is null) return;
        try
        {
            var state = await _audioControlService.GetDefaultOutputStateAsync();
            _mainWindow.SetAudioEndpointState(state);
        }
        catch (Exception exception)
        {
            _mainWindow.SetAudioControlError(
                $"Windows playback audio is unavailable. Connect an output device and refresh. {exception.Message}");
        }
    }

    private void HandleSensorSample(SensorSample sample)
    {
        _latestSensorSample = sample;
        _mainWindow?.UpdateSensorSample(sample);
        if (_homeAssistantConnected && _pairedDeviceId is not null)
            _ = PublishSensorSampleAsync(sample);
    }

    private async Task StartSensorMonitorAsync()
    {
        try
        {
            if (_sensorMonitor is not null)
                await _sensorMonitor.StartAsync();
        }
        catch (Exception exception)
        {
            _mainWindow?.UpdateSensorMonitoringError(exception.Message);
        }
    }

    private async void OnConnectHomeAssistantRequested(Uri instanceUri) =>
        await ConnectHomeAssistantAsync(instanceUri, authenticate: true);

    private async Task ConnectHomeAssistantAsync(Uri instanceUri, bool authenticate)
    {
        if (_isExiting || _mainWindow is null) return;

        _mainWindow.UpdateConnectionStatus(
            authenticate ? "Waiting for sign-in" : "Restoring connection",
            authenticate
                ? "Complete the approval in your browser. The native connection will update when you return."
                : "Refreshing your saved Home Assistant session.",
            connected: false,
            busy: true);

        _mainWindow.UpdateHomeAssistantPublishingStatus(
            "Connecting to Home Assistant…",
            paired: _pairedDeviceId is not null,
            connected: false);
        _trayPopupWindow?.UpdateHomeAssistantStatus(
            authenticate ? "Signing in…" : "Connecting…",
            "Home Assistant", connected: false, busy: true);

        try
        {
            await StopHomeAssistantAsync();
            _tokenProvider = new HomeAssistantTokenProvider(instanceUri, new WindowsCredentialStore());

            if (authenticate)
                await _tokenProvider.AuthenticateAsync();

            _connectedInstance = instanceUri;
            AppSettingsStore.SaveHomeAssistantUrl(instanceUri.AbsoluteUri);
            _mainWindow.SetDashboardUri(instanceUri);
            _mainWindow.SetSelectedDashboard(AppSettingsStore.LoadSelectedDashboardId());
            _telemetryClient?.Configure(instanceUri, _tokenProvider.GetAccessTokenAsync);

            _connectionSupervisor = new HomeAssistantConnectionSupervisor(new HomeAssistantWebSocketClient());
            _connectionSupervisor.StateChanged += (_, state) =>
            {
                var connected = state.Status == HomeAssistantConnectionStatus.Connected;
                var wasConnected = _homeAssistantConnected;
                _homeAssistantConnected = connected;
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
                _mainWindow?.UpdateHomeAssistantPublishingStatus(
                    connected
                        ? _pairedDeviceId is null
                            ? "Home Assistant connected · choose sensors and pair this PC"
                            : "Paired · publishing selected sensors"
                        : "Home Assistant disconnected · no sensor data is being sent",
                    paired: _pairedDeviceId is not null,
                    connected);

                _trayPopupWindow?.UpdateHomeAssistantStatus(statusTitle, statusDetail, connected, busy: isBusy);

                if (!connected && wasConnected)
                    _ = DisconnectTelemetrySocketAsync();
                else if (connected && _pairedDeviceId is not null && _latestSensorSample is not null)
                    _ = PublishSensorSampleAsync(_latestSensorSample);
            };

            await _connectionSupervisor.StartAsync(instanceUri, _tokenProvider.GetAccessTokenAsync);
        }
        catch (OperationCanceledException)
        {
            _homeAssistantConnected = false;
            _mainWindow.UpdateConnectionStatus(
                "Sign-in cancelled",
                "You can try connecting again at any time.",
                connected: false);
            _mainWindow.UpdateHomeAssistantPublishingStatus(
                "Sign-in cancelled · sensor data stays on this PC",
                _pairedDeviceId is not null,
                connected: false);
            _trayPopupWindow?.UpdateHomeAssistantStatus("Not connected", "Connect in the app", connected: false);
            await StopHomeAssistantAsync();
        }
        catch (Exception exception)
        {
            _homeAssistantConnected = false;
            _mainWindow.UpdateConnectionStatus("Could not connect", exception.Message, connected: false);
            _mainWindow.UpdateHomeAssistantPublishingStatus(
                $"Home Assistant connection failed: {exception.Message}",
                _pairedDeviceId is not null,
                connected: false);
            _trayPopupWindow?.UpdateHomeAssistantStatus("Could not connect", exception.Message, connected: false);
            await StopHomeAssistantAsync();
        }
    }

    private async Task RestoreHomeAssistantConnectionAsync(Uri savedUri) =>
        await ConnectHomeAssistantAsync(savedUri, authenticate: false);

    private async void OnPairSensorsRequested(IReadOnlyCollection<string> selectedSensorIds) =>
        await PairSensorsAsync(selectedSensorIds);

    private async Task PairSensorsAsync(IReadOnlyCollection<string> selectedSensorIds)
    {
        if (!_homeAssistantConnected || _tokenProvider is null || _connectedInstance is null ||
            _telemetryClient is null || _latestSensorSample is null || _mainWindow is null)
        {
            _mainWindow?.UpdateHomeAssistantPublishingStatus(
                "Connect to Home Assistant before pairing this PC.",
                _pairedDeviceId is not null,
                _homeAssistantConnected);
            return;
        }

        await _telemetryPairLock.WaitAsync();
        try
        {
            var selected = selectedSensorIds.ToHashSet(StringComparer.Ordinal);
            var descriptors = _latestSensorSample.Sensors
                .Where(sensor => selected.Contains(sensor.Definition.Id))
                .ToArray();
            if (descriptors.Length == 0)
                throw new InvalidOperationException("Select at least one available sensor.");

            _mainWindow.UpdateHomeAssistantPublishingStatus(
                "Registering this PC with Home Assistant…",
                _pairedDeviceId is not null,
                connected: true);

            var installationId = AppSettingsStore.LoadOrCreateInstallationId();
            var appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.2.0";
            var deviceId = await _telemetryClient.RegisterDeviceAsync(
                installationId,
                Environment.MachineName,
                appVersion,
                descriptors);

            _pairedDeviceId = deviceId;
            AppSettingsStore.SavePairedDeviceId(deviceId);
            AppSettingsStore.SavePublishedSensorIds(selectedSensorIds);
            _lastTelemetryError = null;

            _mainWindow.UpdateHomeAssistantPublishingStatus(
                $"Paired as {Environment.MachineName} · {descriptors.Length} selected sensors",
                paired: true,
                connected: true);
        }
        catch (Exception exception)
        {
            _mainWindow.UpdateHomeAssistantPublishingStatus(
                $"Pairing failed: {exception.Message}",
                _pairedDeviceId is not null,
                connected: _homeAssistantConnected);
        }
        finally
        {
            _telemetryPairLock.Release();
        }

        if (_pairedDeviceId is not null && _latestSensorSample is not null)
            await PublishSensorSampleAsync(_latestSensorSample);
    }

    private async void OnUnpairSensorsRequested() => await UnpairSensorsAsync();

    private async Task UnpairSensorsAsync()
    {
        if (!_homeAssistantConnected || _pairedDeviceId is null || _telemetryClient is null)
        {
            _mainWindow?.UpdateHomeAssistantPublishingStatus(
                "Connect to Home Assistant before unpairing this PC.",
                paired: _pairedDeviceId is not null,
                connected: _homeAssistantConnected);
            return;
        }

        await _telemetryPairLock.WaitAsync();
        try
        {
            await _telemetryClient.UnpairDeviceAsync(_pairedDeviceId);
            _pairedDeviceId = null;
            AppSettingsStore.SavePairedDeviceId(null);
            _lastTelemetryError = null;
            _mainWindow?.UpdateHomeAssistantPublishingStatus(
                "PC unpaired · readings stay on this computer",
                paired: false,
                connected: true);
        }
        catch (Exception exception)
        {
            _mainWindow?.UpdateHomeAssistantPublishingStatus(
                $"Could not unpair this PC: {exception.Message}",
                paired: true,
                connected: _homeAssistantConnected);
        }
        finally
        {
            _telemetryPairLock.Release();
        }
    }

    private async Task PublishSensorSampleAsync(SensorSample sample)
    {
        var deviceId = _pairedDeviceId;
        var telemetryClient = _telemetryClient;
        if (!_homeAssistantConnected || deviceId is null || telemetryClient is null)
            return;
        if (!await _telemetryPublishLock.WaitAsync(0))
            return;

        try
        {
            var selectedIds = AppSettingsStore.LoadPublishedSensorIds();
            await telemetryClient.PublishAsync(deviceId, sample.Readings.Values.ToArray(), selectedIds);
            if (_lastTelemetryError is not null)
            {
                _lastTelemetryError = null;
                _mainWindow?.UpdateHomeAssistantPublishingStatus(
                    "Paired · publishing selected sensors",
                    paired: true,
                    connected: true);
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            _lastTelemetryError = exception.Message;
            _mainWindow?.UpdateHomeAssistantPublishingStatus(
                $"Home Assistant rejected telemetry: {exception.Message}",
                paired: true,
                connected: _homeAssistantConnected);
        }
        catch (Exception exception)
        {
            if (!string.Equals(_lastTelemetryError, exception.Message, StringComparison.Ordinal))
            {
                _lastTelemetryError = exception.Message;
                _mainWindow?.UpdateHomeAssistantPublishingStatus(
                    $"Publishing paused: {exception.Message}",
                    paired: true,
                    connected: _homeAssistantConnected);
            }
        }
        finally
        {
            _telemetryPublishLock.Release();
        }
    }

    private async Task DisconnectTelemetrySocketAsync()
    {
        if (_telemetryClient is null)
            return;
        try
        {
            await _telemetryClient.DisconnectAsync();
        }
        catch
        {
            // The next authenticated session will establish a new telemetry socket.
        }
    }

    private async void OnDisconnectHomeAssistantRequested() => await DisconnectHomeAssistantAsync();

    private async Task DisconnectHomeAssistantAsync()
    {
        await StopHomeAssistantAsync(clearCredentials: true);
        string? browserSessionWarning = null;
        try
        {
            if (_mainWindow is not null)
                await _mainWindow.ClearDashboardSessionAsync();
        }
        catch (Exception exception)
        {
            browserSessionWarning = $" The embedded browser session could not be cleared: {exception.Message}";
        }

        _connectedInstance = null;
        AppSettingsStore.SaveHomeAssistantUrl(string.Empty);
        _mainWindow?.SetDashboardUri(null);
        _mainWindow?.UpdateConnectionStatus(
            "Disconnected",
            "The native Home Assistant connection and saved sign-in have been removed from this PC." + browserSessionWarning,
            connected: false);
        _mainWindow?.UpdateHomeAssistantPublishingStatus(
            _pairedDeviceId is null
                ? "Not paired · readings stay on this PC"
                : "Pairing retained in Home Assistant · reconnect to resume publishing",
            _pairedDeviceId is not null,
            connected: false);
        _trayPopupWindow?.UpdateHomeAssistantStatus("Not connected", "Connect in the app", connected: false);
    }

    private async Task StopHomeAssistantAsync(bool clearCredentials = false)
    {
        _homeAssistantConnected = false;
        if (_telemetryClient is not null)
            await _telemetryClient.DisconnectAsync();

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
        _mainWindow.NavigateToDashboard();
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
        if (_sensorMonitorStartTask is not null)
            await _sensorMonitorStartTask;
        if (_sensorMonitor is not null)
        {
            await _sensorMonitor.DisposeAsync();
            _sensorMonitor = null;
        }
        await StopHomeAssistantAsync();
        if (_telemetryClient is not null)
        {
            await _telemetryClient.DisposeAsync();
            _telemetryClient = null;
        }
        _telemetryPublishLock.Dispose();
        _telemetryPairLock.Dispose();
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
