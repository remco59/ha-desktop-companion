namespace DesktopCompanion.Core.Models;

/// <summary>Cumulative Windows CPU time counters captured at one point in time.</summary>
public readonly record struct CpuTimesSnapshot(
    ulong IdleTime,
    ulong KernelTime,
    ulong UserTime);
