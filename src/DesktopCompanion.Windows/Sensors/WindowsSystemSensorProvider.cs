using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Windows.Sensors;

/// <summary>
/// First Windows sensor provider. It starts with reliable OS-level values;
/// hardware-specific CPU/GPU/temperature providers will be added separately.
/// </summary>
public sealed class WindowsSystemSensorProvider : ISensorProvider
{
    public string ProviderId => "windows.system";

    public Task<IReadOnlyCollection<SensorDescriptor>> GetSensorsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<SensorDescriptor> sensors =
        [
            new(new SensorDefinition("system_uptime_seconds", "System uptime", "system", "s"), ProviderId, true),
            new(new SensorDefinition("system_hostname", "Computer name", "system", ""), ProviderId, true),
            new(new SensorDefinition("windows_version", "Windows version", "system", ""), ProviderId, true)
        ];

        return Task.FromResult(sensors);
    }

    public Task<SensorReading?> ReadAsync(
        string sensorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SensorReading? reading = sensorId switch
        {
            "system_uptime_seconds" => new SensorReading(
                sensorId,
                DateTimeOffset.UtcNow,
                NumericValue: Environment.TickCount64 / 1000d,
                Unit: "s"),
            "system_hostname" => new SensorReading(
                sensorId,
                DateTimeOffset.UtcNow,
                TextValue: Environment.MachineName),
            "windows_version" => new SensorReading(
                sensorId,
                DateTimeOffset.UtcNow,
                TextValue: Environment.OSVersion.VersionString),
            _ => null
        };

        return Task.FromResult(reading);
    }
}
