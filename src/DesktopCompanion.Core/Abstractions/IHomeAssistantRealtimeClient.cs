using System.Runtime.CompilerServices;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>
/// Streams live Home Assistant events for one authenticated connection.
/// The caller owns reconnect and retry policy.
/// </summary>
public interface IHomeAssistantRealtimeClient
{
    IAsyncEnumerable<HomeAssistantEvent> SubscribeToStateChangesAsync(
        Uri baseUri,
        string accessToken,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}
