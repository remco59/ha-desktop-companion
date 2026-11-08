using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;

namespace DesktopCompanion.Windows.Sensors;

/// <summary>
/// Reads reliable system telemetry through documented Windows kernel APIs.
/// Hardware-specific GPU, temperature and fan providers remain separate capabilities.
/// </summary>
public sealed class WindowsSystemSensorProvider : ISensorProvider
{
    private readonly object _cpuLock = new();
    private CpuTimesSnapshot? _previousCpuTimes;

    public string ProviderId => "windows.system";

    public Task<IReadOnlyCollection<SensorDescriptor>> GetSensorsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<SensorDescriptor> sensors =
        [
            new(new SensorDefinition("cpu_usage_percent", "CPU usage", "processor", "%"), ProviderId, true),
            new(new SensorDefinition("memory_usage_percent", "Memory usage", "memory", "%"), ProviderId, true),
            new(new SensorDefinition("memory_used_bytes", "Memory used", "memory", "B"), ProviderId, true),
            new(new SensorDefinition("memory_available_bytes", "Memory available", "memory", "B"), ProviderId, true),
            new(new SensorDefinition("memory_total_bytes", "Total physical memory", "memory", "B"), ProviderId, true),
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
        var sampledAt = DateTimeOffset.UtcNow;

        SensorReading? reading = sensorId switch
        {
            "cpu_usage_percent" => ReadCpuUsage(sensorId, sampledAt),
            "memory_usage_percent" => ReadMemoryUsage(sensorId, sampledAt),
            "memory_used_bytes" => ReadMemoryValue(sensorId, sampledAt, MemoryValue.Used),
            "memory_available_bytes" => ReadMemoryValue(sensorId, sampledAt, MemoryValue.Available),
            "memory_total_bytes" => ReadMemoryValue(sensorId, sampledAt, MemoryValue.Total),
            "system_uptime_seconds" => new SensorReading(
                sensorId, sampledAt, NumericValue: Environment.TickCount64 / 1000d, Unit: "s"),
            "system_hostname" => new SensorReading(
                sensorId, sampledAt, TextValue: Environment.MachineName),
            "windows_version" => new SensorReading(
                sensorId, sampledAt, TextValue: Environment.OSVersion.VersionString),
            _ => null
        };

        return Task.FromResult(reading);
    }

    private SensorReading? ReadCpuUsage(string sensorId, DateTimeOffset sampledAt)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the system CPU time counters.");

        var current = new CpuTimesSnapshot(idle.ToUInt64(), kernel.ToUInt64(), user.ToUInt64());
        CpuTimesSnapshot? previous;
        lock (_cpuLock)
        {
            previous = _previousCpuTimes;
            _previousCpuTimes = current;
        }

        if (previous is null)
            return null;

        var percentage = CpuUsageCalculator.Calculate(previous.Value, current);
        return percentage is null
            ? null
            : new SensorReading(sensorId, sampledAt, NumericValue: percentage.Value, Unit: "%");
    }

    private static SensorReading? ReadMemoryUsage(string sensorId, DateTimeOffset sampledAt)
    {
        var memory = ReadMemoryStatus();
        if (memory.TotalPhysicalBytes == 0)
            return null;

        var used = memory.TotalPhysicalBytes > memory.AvailablePhysicalBytes
            ? memory.TotalPhysicalBytes - memory.AvailablePhysicalBytes
            : 0;
        var percentage = used / (double)memory.TotalPhysicalBytes * 100d;
        return new SensorReading(sensorId, sampledAt, NumericValue: Math.Clamp(percentage, 0d, 100d), Unit: "%");
    }

    private static SensorReading ReadMemoryValue(string sensorId, DateTimeOffset sampledAt, MemoryValue value)
    {
        var memory = ReadMemoryStatus();
        var numericValue = value switch
        {
            MemoryValue.Used => memory.TotalPhysicalBytes > memory.AvailablePhysicalBytes
                ? memory.TotalPhysicalBytes - memory.AvailablePhysicalBytes
                : 0UL,
            MemoryValue.Available => memory.AvailablePhysicalBytes,
            _ => memory.TotalPhysicalBytes
        };

        return new SensorReading(sensorId, sampledAt, NumericValue: numericValue, Unit: "B");
    }

    private static MemoryStatusEx ReadMemoryStatus()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read physical memory status.");

        return status;
    }

    private enum MemoryValue
    {
        Used,
        Available,
        Total
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }
}
