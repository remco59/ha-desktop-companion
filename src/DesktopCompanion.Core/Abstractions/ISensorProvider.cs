using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

public interface ISensorProvider
{
    string ProviderId { get; }

    Task<IReadOnlyCollection<SensorDescriptor>> GetSensorsAsync(
        CancellationToken cancellationToken = default);

    Task<SensorReading?> ReadAsync(
        string sensorId,
        CancellationToken cancellationToken = default);
}
