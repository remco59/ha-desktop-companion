using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.HomeAssistant.Services;

/// <summary>
/// Supervises one Home Assistant real-time connection. Transient transport failures
/// are retried with capped exponential backoff and jitter. Authentication rejection
/// stops retries until the caller explicitly starts the supervisor again.
/// </summary>
public sealed class HomeAssistantConnectionSupervisor : IHomeAssistantConnectionSupervisor
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StableConnectionWindow = TimeSpan.FromMinutes(1);

    private readonly IHomeAssistantRealtimeClient _realtimeClient;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _stateLock = new();

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private HomeAssistantConnectionStateChangedEventArgs _currentState =
        new(HomeAssistantConnectionStatus.Disconnected, DateTimeOffset.UtcNow, null, 0);
    private bool _disposed;

    public HomeAssistantConnectionSupervisor(IHomeAssistantRealtimeClient realtimeClient)
    {
        _realtimeClient = realtimeClient ?? throw new ArgumentNullException(nameof(realtimeClient));
    }

    public event EventHandler<HomeAssistantConnectionStateChangedEventArgs>? StateChanged;

    public event EventHandler<HomeAssistantEventReceivedEventArgs>? EventReceived;

    public HomeAssistantConnectionStateChangedEventArgs CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public async Task StartAsync(
        Uri baseUri,
        Func<CancellationToken, Task<string>> accessTokenProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        if (!baseUri.IsAbsoluteUri ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The Home Assistant URL must be an absolute HTTP or HTTPS URL.", nameof(baseUri));
        }

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_runTask is { IsCompleted: false })
            {
                return;
            }

            _runCancellation?.Dispose();
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var runToken = _runCancellation.Token;
            _runTask = Task.Run(
                () => RunConnectionLoopAsync(baseUri, accessTokenProvider, runToken),
                CancellationToken.None);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? runTask;

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            runTask = _runTask;
            _runCancellation?.Cancel();
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (runTask is not null)
        {
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ReferenceEquals(_runTask, runTask))
            {
                _runTask = null;
                _runCancellation?.Dispose();
                _runCancellation = null;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }

        SetState(HomeAssistantConnectionStatus.Disconnected, null, 0);
    }

    private async Task RunConnectionLoopAsync(
        Uri baseUri,
        Func<CancellationToken, Task<string>> accessTokenProvider,
        CancellationToken cancellationToken)
    {
        var retryAttempt = 0;
        DateTimeOffset? connectedAt = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SetState(
                    retryAttempt == 0
                        ? HomeAssistantConnectionStatus.Connecting
                        : HomeAssistantConnectionStatus.Reconnecting,
                    retryAttempt == 0 ? "Connecting to Home Assistant." : "Reconnecting to Home Assistant.",
                    retryAttempt);

                try
                {
                    var accessToken = await accessTokenProvider(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        throw new UnauthorizedAccessException(
                            "The Home Assistant authentication provider did not return an access token.");
                    }

                    connectedAt = null;
                    var connected = 0;

                    await foreach (var homeAssistantEvent in _realtimeClient
                        .SubscribeToStateChangesAsync(
                            baseUri,
                            accessToken,
                            connectionEstablished: () =>
                            {
                                if (Interlocked.Exchange(ref connected, 1) == 0)
                                {
                                    connectedAt = DateTimeOffset.UtcNow;
                                    SetState(
                                        HomeAssistantConnectionStatus.Connected,
                                        "Connected to Home Assistant.",
                                        retryAttempt);
                                }
                            },
                            cancellationToken)
                        .WithCancellation(cancellationToken)
                        .ConfigureAwait(false))
                    {
                        RaiseEventReceived(homeAssistantEvent);
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        throw new WebSocketException("Home Assistant closed the real-time connection.");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    SetState(
                        HomeAssistantConnectionStatus.AuthenticationRequired,
                        "Home Assistant rejected the credentials. Reconnect your account.",
                        retryAttempt);
                    break;
                }
                catch (Exception exception) when (IsTransientTransportError(exception))
                {
                    if (connectedAt.HasValue &&
                        DateTimeOffset.UtcNow - connectedAt.Value >= StableConnectionWindow)
                    {
                        retryAttempt = 0;
                    }

                    connectedAt = null;
                    retryAttempt++;
                    var delay = GetRetryDelay(retryAttempt);

                    SetState(
                        HomeAssistantConnectionStatus.Reconnecting,
                        $"Home Assistant connection lost. Retrying in {Math.Ceiling(delay.TotalSeconds)} seconds.",
                        retryAttempt);

                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception exception)
                {
                    SetState(
                        HomeAssistantConnectionStatus.Faulted,
                        $"Home Assistant connection stopped: {exception.Message}",
                        retryAttempt);
                    break;
                }
            }
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                SetState(HomeAssistantConnectionStatus.Disconnected, null, 0);
            }
        }
    }

    private static bool IsTransientTransportError(Exception exception)
    {
        return exception is WebSocketException
            or HttpRequestException
            or IOException
            or TimeoutException;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        var exponent = Math.Clamp(attempt - 1, 0, 10);
        var baseMilliseconds = InitialRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var cappedMilliseconds = Math.Min(baseMilliseconds, MaximumRetryDelay.TotalMilliseconds);

        // Add up to ±20% jitter so multiple desktop clients do not reconnect in lockstep.
        var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4);
        return TimeSpan.FromMilliseconds(Math.Min(
            cappedMilliseconds * jitterFactor,
            MaximumRetryDelay.TotalMilliseconds));
    }

    private void SetState(
        HomeAssistantConnectionStatus status,
        string? message,
        int reconnectAttempt)
    {
        var nextState = new HomeAssistantConnectionStateChangedEventArgs(
            status,
            DateTimeOffset.UtcNow,
            message,
            reconnectAttempt);

        lock (_stateLock)
        {
            _currentState = nextState;
        }

        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<HomeAssistantConnectionStateChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, nextState);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Desktop Companion connection-state subscriber failed: {exception}");
            }
        }
    }

    private void RaiseEventReceived(HomeAssistantEvent homeAssistantEvent)
    {
        var eventArgs = new HomeAssistantEventReceivedEventArgs(homeAssistantEvent);
        var handlers = EventReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<HomeAssistantEventReceivedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Desktop Companion Home Assistant event subscriber failed: {exception}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _lifecycleLock.Dispose();
    }
}
