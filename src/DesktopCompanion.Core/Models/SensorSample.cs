namespace DesktopCompanion.Core.Models;

/// <summary>A timestamped batch of the latest sensor readings, current catalogue and any read errors.</summary>
public sealed record SensorSample(
    DateTimeOffset SampledAt,
    IReadOnlyDictionary<string, SensorReading> Readings,
    IReadOnlyDictionary<string, string> Errors)
{
    public IReadOnlyCollection<SensorDescriptor> Sensors { get; init; } = Array.Empty<SensorDescriptor>();
}
