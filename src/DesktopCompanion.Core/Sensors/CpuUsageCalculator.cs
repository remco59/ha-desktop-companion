using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Sensors;

/// <summary>Calculates system CPU utilisation from two cumulative Windows time-counter samples.</summary>
public static class CpuUsageCalculator
{
    public static double? Calculate(CpuTimesSnapshot previous, CpuTimesSnapshot current)
    {
        if (current.IdleTime < previous.IdleTime ||
            current.KernelTime < previous.KernelTime ||
            current.UserTime < previous.UserTime)
            return null;

        var idleDelta = current.IdleTime - previous.IdleTime;
        var kernelDelta = current.KernelTime - previous.KernelTime;
        var userDelta = current.UserTime - previous.UserTime;
        var totalDelta = (decimal)kernelDelta + userDelta;

        // Kernel time includes idle time. The first sample is only a baseline;
        // zero-length or inconsistent deltas must never be reported as a reading.
        if (totalDelta <= 0 || idleDelta > totalDelta)
            return null;

        var busyPercentage = (double)((totalDelta - idleDelta) / totalDelta * 100m);
        return Math.Clamp(busyPercentage, 0d, 100d);
    }
}
