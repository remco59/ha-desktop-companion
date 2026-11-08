namespace DesktopCompanion.Core.Models;

/// <summary>Cumulative byte counters for one network adapter at a specific time.</summary>
public readonly record struct NetworkCounterSnapshot(
    long BytesReceived,
    long BytesSent,
    DateTimeOffset RecordedAt);

/// <summary>Per-second receive and send rates derived from two counter snapshots.</summary>
public readonly record struct NetworkThroughput(
    double ReceiveBytesPerSecond,
    double SendBytesPerSecond);
