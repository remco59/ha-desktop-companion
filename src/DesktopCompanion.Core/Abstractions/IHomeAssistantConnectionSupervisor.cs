using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>
/// Owns the lifecycle of one Home Assistant real-time connection.
/// Authentication and credential storage are supplied by the caller.
/// </summary>
public interface IHomeAssistantConnectionSupervisor : IAsyncDisposable
{
    event EventHandler<HomeAssistantConnectionStateChangedEventArgs>? StateChanged;

    event EventHandler<HomeAssistantEventReceivedEventArgs>? EventReceived;

    HomeAssistantConnectionStateChangedEventArgs CurrentState { get; }

    Task StartAsync(
        Uri baseUri,
        Func<CancellationToken, Task<string>> accessTokenProvider,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
