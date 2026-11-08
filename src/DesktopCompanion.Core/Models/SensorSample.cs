namespace DesktopCompanion.Core.Models;

/// <summary>A timestamped batch of the latest known sensor readings and any current read errors.</summary>
public sealed record SensorSample(
    DateTimeOffset SampledAt,
    IReadOnlyDictionary<string, SensorReading> Readings,
    IReadOnlyDictionary<string, string> Errors);
