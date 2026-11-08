using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

public interface IHomeAssistantClient
{
    Task<bool> CheckConnectionAsync(
        Uri baseUri,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HomeAssistantEntity>> GetEntitiesAsync(
        Uri baseUri,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task CallServiceAsync(
        Uri baseUri,
        string accessToken,
        string domain,
        string service,
        IReadOnlyDictionary<string, object?> serviceData,
        CancellationToken cancellationToken = default);
}
