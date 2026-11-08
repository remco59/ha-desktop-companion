namespace DesktopCompanion.Core.Models;

public sealed record SensorDefinition(
    string Id,
    string Name,
    string Category,
    string Unit,
    bool IsEnabled = true);

public sealed record SensorReading(
    string SensorId,
    DateTimeOffset RecordedAt,
    double? NumericValue = null,
    string? TextValue = null,
    string? Unit = null);

public sealed record SensorDescriptor(
    SensorDefinition Definition,
    string ProviderId,
    bool IsAvailable,
    string? UnavailableReason = null);
