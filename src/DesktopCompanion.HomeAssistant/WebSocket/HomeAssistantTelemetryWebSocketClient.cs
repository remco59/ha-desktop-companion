using System.Net.WebSockets;
using System.Text.Json;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.HomeAssistant.WebSocket;

/// <summary>
/// Maintains a separate authenticated Home Assistant WebSocket for sensor telemetry.
/// It never shares the state_changed subscription socket or exposes tokens to WebView2.
/// </summary>
public sealed class HomeAssistantTelemetryWebSocketClient : IHomeAssistantTelemetryClient
{
    private const int MaximumResponseBytes = 64 * 1024;
    private const string RegisterCommand = "desktop_companion/register_device";
    private const string StartSessionCommand = "desktop_companion/start_session";
    private const string PublishCommand = "desktop_companion/publish";
    private const string UnpairCommand = "desktop_companion/unpair_device";

    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private ClientWebSocket? _socket;
    private Uri? _baseUri;
    private Func<CancellationToken, Task<string>>? _accessTokenProvider;
    private string? _sessionNonce;
    private string? _sessionDeviceId;
    private long _sequence;
    private int _nextMessageId;
    private bool _disposed;

    public void Configure(Uri baseUri, Func<CancellationToken, Task<string>> accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        if (!baseUri.IsAbsoluteUri ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("The Home Assistant URL must use HTTP or HTTPS.", nameof(baseUri));

        if (_baseUri is not null && _baseUri != baseUri)
            InvalidateSocket();

        _baseUri = baseUri;
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task<string> RegisterDeviceAsync(
        string installationId,
        string deviceName,
        string appVersion,
        IReadOnlyCollection<SensorDescriptor> sensors,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        ArgumentNullException.ThrowIfNull(sensors);
        if (sensors.Count is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(sensors), "Select between 1 and 100 sensors.");

        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureNotDisposed();
            var descriptors = sensors
                .Where(sensor => sensor.IsAvailable && sensor.Definition.IsEnabled)
                .Select(sensor => new
                {
                    sensor_id = sensor.Definition.Id,
                    name = sensor.Definition.Name,
                    category = sensor.Definition.Category,
                    unit = sensor.Definition.Unit
                })
                .ToArray();

            if (descriptors.Length == 0)
                throw new InvalidOperationException("Select at least one available sensor to publish.");

            var result = await SendCommandCoreAsync(
                RegisterCommand,
                new
                {
                    installation_id = installationId,
                    name = deviceName,
                    app_version = appVersion,
                    sensors = descriptors
                },
                cancellationToken).ConfigureAwait(false);

            var deviceId = ReadRequiredString(result, "device_id");
            _sessionNonce = null;
            _sessionDeviceId = deviceId;
            _sequence = 0;
            return deviceId;
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task PublishAsync(
        string deviceId,
        IReadOnlyCollection<SensorReading> readings,
        IReadOnlyCollection<string> selectedSensorIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(selectedSensorIds);
        var selected = selectedSensorIds.ToHashSet(StringComparer.Ordinal);
        var batch = readings
            .Where(reading => selected.Contains(reading.SensorId) && !reading.IsStale)
            .Select(reading => reading.NumericValue is double number
                ? new { sensor_id = reading.SensorId, value = (object)number }
                : reading.TextValue is string text
                    ? new { sensor_id = reading.SensorId, value = (object)text }
                    : null)
            .Where(reading => reading is not null)
            .ToArray();

        if (batch.Length == 0)
            return;
        if (batch.Length > 100)
            throw new InvalidOperationException("A telemetry batch cannot contain more than 100 readings.");

        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureNotDisposed();
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            if (!string.Equals(_sessionDeviceId, deviceId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_sessionNonce))
            {
                var session = await SendCommandOnConnectedSocketAsync(
                    StartSessionCommand,
                    new { device_id = deviceId },
                    cancellationToken).ConfigureAwait(false);
                _sessionNonce = ReadRequiredString(session, "session_nonce");
                _sessionDeviceId = deviceId;
                _sequence = 0;
            }

            var nextSequence = checked(_sequence + 1);
            await SendCommandOnConnectedSocketAsync(
                PublishCommand,
                new
                {
                    device_id = deviceId,
                    session_nonce = _sessionNonce,
                    sequence = nextSequence,
                    readings = batch
                },
                cancellationToken).ConfigureAwait(false);

            _sequence = nextSequence;
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            InvalidateSocket();
            throw;
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task UnpairDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureNotDisposed();
            await SendCommandCoreAsync(
                UnpairCommand,
                new { device_id = deviceId },
                cancellationToken).ConfigureAwait(false);
            if (string.Equals(_sessionDeviceId, deviceId, StringComparison.Ordinal))
            {
                _sessionNonce = null;
                _sessionDeviceId = null;
                _sequence = 0;
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            InvalidateSocket();
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private async Task<JsonElement> SendCommandCoreAsync(
        string command,
        object payload,
        CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await SendCommandOnConnectedSocketAsync(command, payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement> SendCommandOnConnectedSocketAsync(
        string command,
        object payload,
        CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("The telemetry WebSocket is not connected.");
        var messageId = Interlocked.Increment(ref _nextMessageId);
        var message = new Dictionary<string, object?>
        {
            ["id"] = messageId,
            ["type"] = command
        };

        foreach (var property in JsonSerializer.SerializeToElement(payload, _jsonOptions).EnumerateObject())
            message[property.Name] = property.Value;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        if (bytes.Length > MaximumResponseBytes)
            throw new InvalidOperationException("The telemetry WebSocket message exceeds the local size limit.");

        try
        {
            await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);

            using var response = await ReceiveRequiredMessageAsync(socket, cancellationToken).ConfigureAwait(false);
            var root = response.RootElement;
            if (!string.Equals(ReadString(root, "type"), "result", StringComparison.Ordinal) ||
                !root.TryGetProperty("id", out var idElement) ||
                !idElement.TryGetInt32(out var responseId) ||
                responseId != messageId)
                throw new WebSocketException("Home Assistant returned an unexpected telemetry command response.");

            if (!root.TryGetProperty("success", out var successElement) ||
                successElement.ValueKind != JsonValueKind.True)
            {
                var errorElement = root.TryGetProperty("error", out var error) ? error : default;
                var code = ReadString(errorElement, "code");
                var reason = ReadString(errorElement, "message") ?? "Home Assistant rejected the telemetry command.";
                if (string.Equals(code, "unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(code, "auth_invalid", StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException(reason);
                throw new InvalidOperationException(reason);
            }

            if (root.TryGetProperty("result", out var result))
                return result.Clone();

            using var emptyResult = JsonDocument.Parse("{}");
            return emptyResult.RootElement.Clone();
        }
        catch (UnauthorizedAccessException)
        {
            InvalidateSocket();
            throw;
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            InvalidateSocket();
            throw;
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State == WebSocketState.Open)
            return;
        InvalidateSocket();

        var baseUri = _baseUri ?? throw new InvalidOperationException("Home Assistant is not configured.");
        var tokenProvider = _accessTokenProvider ??
                            throw new InvalidOperationException("Home Assistant authentication is not configured.");
        var accessToken = await tokenProvider(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new UnauthorizedAccessException("Home Assistant did not provide an access token.");

        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        try
        {
            await socket.ConnectAsync(HomeAssistantEndpoint.CreateWebSocketUri(baseUri), cancellationToken)
                .ConfigureAwait(false);

            using (var authRequired = await ReceiveRequiredMessageAsync(socket, cancellationToken).ConfigureAwait(false))
            {
                if (!string.Equals(ReadString(authRequired.RootElement, "type"), "auth_required", StringComparison.Ordinal))
                    throw new WebSocketException("Home Assistant returned an unexpected WebSocket handshake.");
            }

            await SendJsonAsync(socket, new { type = "auth", access_token = accessToken }, cancellationToken)
                .ConfigureAwait(false);

            using (var authResponse = await ReceiveRequiredMessageAsync(socket, cancellationToken).ConfigureAwait(false))
            {
                var responseType = ReadString(authResponse.RootElement, "type");
                if (string.Equals(responseType, "auth_invalid", StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("Home Assistant rejected the telemetry connection credentials.");
                if (!string.Equals(responseType, "auth_ok", StringComparison.Ordinal))
                    throw new WebSocketException("Home Assistant returned an unexpected authentication response.");
            }

            _socket = socket;
            _sessionNonce = null;
            _sessionDeviceId = null;
            _sequence = 0;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task SendJsonAsync<T>(
        ClientWebSocket socket,
        T payload,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<JsonDocument> ReceiveRequiredMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8 * 1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Home Assistant closed the telemetry WebSocket.");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new WebSocketException("Home Assistant returned a non-text telemetry message.");
            if (buffer.Length + result.Count > MaximumResponseBytes)
                throw new WebSocketException("Home Assistant returned an oversized telemetry message.");

            buffer.Write(chunk, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return JsonDocument.Parse(buffer.ToArray());
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ReadRequiredString(JsonElement element, string propertyName) =>
        ReadString(element, propertyName) is { Length: > 0 } value
            ? value
            : throw new WebSocketException($"Home Assistant did not return '{propertyName}'.");

    private static bool IsTransportFailure(Exception exception) =>
        exception is WebSocketException or IOException or JsonException or ObjectDisposedException;

    private void InvalidateSocket()
    {
        _socket?.Abort();
        _socket?.Dispose();
        _socket = null;
        _sessionNonce = null;
        _sessionDeviceId = null;
        _sequence = 0;
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await DisconnectAsync().ConfigureAwait(false);
        _disposed = true;
        _commandLock.Dispose();
    }
}
