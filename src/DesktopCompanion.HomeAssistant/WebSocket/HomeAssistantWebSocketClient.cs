using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.HomeAssistant.WebSocket;

/// <summary>
/// Authenticates to Home Assistant's WebSocket API and streams state_changed events.
/// This class deliberately handles one connection only; retry/backoff belongs to
/// the application-level connection supervisor.
/// </summary>
public sealed class HomeAssistantWebSocketClient : IHomeAssistantRealtimeClient
{
    private const int SubscriptionId = 1;
    private const int ReceiveBufferSize = 8 * 1024;
    private const int MaximumMessageSize = 1024 * 1024;

    public async IAsyncEnumerable<HomeAssistantEvent> SubscribeToStateChangesAsync(
        Uri baseUri,
        string accessToken,
        Action? connectionEstablished = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("A Home Assistant access token is required.", nameof(accessToken));
        }

        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        var endpoint = HomeAssistantEndpoint.CreateWebSocketUri(baseUri);
        await socket.ConnectAsync(endpoint, cancellationToken);

        using (var authRequired = await ReceiveRequiredMessageAsync(socket, cancellationToken))
        {
            var messageType = ReadString(authRequired.RootElement, "type");
            if (!string.Equals(messageType, "auth_required", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Home Assistant returned an unexpected WebSocket handshake message: '{messageType ?? "unknown"}'.");
            }
        }

        await SendJsonAsync(
            socket,
            new
            {
                type = "auth",
                access_token = accessToken
            },
            cancellationToken);

        using (var authResponse = await ReceiveRequiredMessageAsync(socket, cancellationToken))
        {
            var responseType = ReadString(authResponse.RootElement, "type");
            if (string.Equals(responseType, "auth_invalid", StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Home Assistant rejected the access token. Reconnect your Home Assistant account.");
            }

            if (!string.Equals(responseType, "auth_ok", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Home Assistant returned an unexpected authentication response: '{responseType ?? "unknown"}'.");
            }
        }

        await SendJsonAsync(
            socket,
            new
            {
                id = SubscriptionId,
                type = "subscribe_events",
                event_type = "state_changed"
            },
            cancellationToken);

        using (var subscriptionResponse = await ReceiveRequiredMessageAsync(socket, cancellationToken))
        {
            var root = subscriptionResponse.RootElement;
            var responseType = ReadString(root, "type");
            var responseId = root.TryGetProperty("id", out var idElement) &&
                             idElement.TryGetInt32(out var parsedId)
                ? parsedId
                : -1;

            if (!string.Equals(responseType, "result", StringComparison.Ordinal) ||
                responseId != SubscriptionId)
            {
                throw new InvalidOperationException(
                    "Home Assistant returned an unexpected response while subscribing to state changes.");
            }

            var successful = root.TryGetProperty("success", out var successElement) &&
                             successElement.ValueKind == JsonValueKind.True;

            if (!successful)
            {
                var reason = root.TryGetProperty("error", out var errorElement)
                    ? ReadString(errorElement, "message")
                    : null;

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(reason)
                        ? "Home Assistant rejected the state-change subscription."
                        : $"Home Assistant rejected the state-change subscription: {reason}");
            }
        }

        connectionEstablished?.Invoke();

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var message = await ReceiveMessageAsync(socket, cancellationToken);
            if (message is null)
            {
                yield break;
            }

            var root = message.RootElement;
            if (!string.Equals(ReadString(root, "type"), "event", StringComparison.Ordinal) ||
                !root.TryGetProperty("event", out var eventElement) ||
                eventElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var eventType = ReadString(eventElement, "event_type");
            if (!string.Equals(eventType, "state_changed", StringComparison.Ordinal) ||
                !eventElement.TryGetProperty("data", out var dataElement))
            {
                continue;
            }

            var timeFired = eventElement.TryGetProperty("time_fired", out var timeElement) &&
                            timeElement.ValueKind == JsonValueKind.String &&
                            timeElement.TryGetDateTimeOffset(out var parsedTime)
                ? parsedTime
                : (DateTimeOffset?)null;

            yield return new HomeAssistantEvent(
                eventType,
                dataElement.Clone(),
                timeFired);
        }
    }

    private static async Task SendJsonAsync<T>(
        ClientWebSocket socket,
        T payload,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await socket.SendAsync(
            bytes.AsMemory(),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    private static async Task<JsonDocument> ReceiveRequiredMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var document = await ReceiveMessageAsync(socket, cancellationToken);
        return document ?? throw new WebSocketException(
            "Home Assistant closed the WebSocket connection before completing the handshake.");
    }

    private static async Task<JsonDocument?> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var receiveBuffer = new byte[ReceiveBufferSize];

        while (true)
        {
            var result = await socket.ReceiveAsync(
                receiveBuffer.AsMemory(),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new WebSocketException("Home Assistant sent a non-text WebSocket message.");
            }

            if (buffer.Length + result.Count > MaximumMessageSize)
            {
                throw new WebSocketException(
                    $"Home Assistant sent a WebSocket message larger than the {MaximumMessageSize} byte limit.");
            }

            buffer.Write(receiveBuffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                break;
            }
        }

        try
        {
            return JsonDocument.Parse(buffer.ToArray());
        }
        catch (JsonException exception)
        {
            throw new WebSocketException("Home Assistant sent an invalid JSON WebSocket message.", exception);
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
