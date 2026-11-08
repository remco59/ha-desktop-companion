using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>Publishes selected Windows sensor data through the authenticated Home Assistant integration.</summary>
public interface IHomeAssistantTelemetryClient : IAsyncDisposable
{
    void Configure(Uri baseUri, Func<CancellationToken, Task<string>> accessTokenProvider);

    Task<string> RegisterDeviceAsync(
        string installationId,
        string deviceName,
        string appVersion,
        IReadOnlyCollection<SensorDescriptor> sensors,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        string deviceId,
        IReadOnlyCollection<SensorReading> readings,
        IReadOnlyCollection<string> selectedSensorIds,
        CancellationToken cancellationToken = default);

    Task UnpairDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
