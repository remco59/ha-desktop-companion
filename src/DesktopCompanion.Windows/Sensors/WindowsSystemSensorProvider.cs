using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;

namespace DesktopCompanion.Windows.Sensors;

/// <summary>
/// Reads CPU, memory, uptime, storage and network telemetry through documented
/// Windows/.NET APIs. Optional GPU, temperature and fan metrics remain separate.
/// </summary>
public sealed class WindowsSystemSensorProvider : ISensorProvider
{
    private readonly object _cpuLock = new();
    private readonly object _networkLock = new();
    private readonly Dictionary<string, StorageSensorBinding> _storageSensors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NetworkSensorBinding> _networkSensors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NetworkCounterSnapshot> _previousNetworkCounters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedNetworkRate> _cachedNetworkRates = new(StringComparer.Ordinal);
    private CpuTimesSnapshot? _previousCpuTimes;

    public string ProviderId => "windows.system";

    public Task<IReadOnlyCollection<SensorDescriptor>> GetSensorsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _storageSensors.Clear();
        _networkSensors.Clear();

        var sensors = new List<SensorDescriptor>
        {
            new(new SensorDefinition("cpu_usage_percent", "CPU usage", "processor", "%"), ProviderId, true),
            new(new SensorDefinition("memory_usage_percent", "Memory usage", "memory", "%"), ProviderId, true),
            new(new SensorDefinition("memory_used_bytes", "Memory used", "memory", "B"), ProviderId, true),
            new(new SensorDefinition("memory_available_bytes", "Memory available", "memory", "B"), ProviderId, true),
            new(new SensorDefinition("memory_total_bytes", "Total physical memory", "memory", "B"), ProviderId, true),
            new(new SensorDefinition("system_uptime_seconds", "System uptime", "system", "s"), ProviderId, true),
            new(new SensorDefinition("system_hostname", "Computer name", "system", ""), ProviderId, true),
            new(new SensorDefinition("windows_version", "Windows version", "system", ""), ProviderId, true)
        };

        AddStorageSensors(sensors, cancellationToken);
        AddNetworkSensors(sensors, cancellationToken);

        return Task.FromResult<IReadOnlyCollection<SensorDescriptor>>(sensors);
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

        if (reading is null && _storageSensors.TryGetValue(sensorId, out var storageBinding))
            reading = ReadStorageSensor(sensorId, sampledAt, storageBinding);

        if (reading is null && _networkSensors.TryGetValue(sensorId, out var networkBinding))
            reading = ReadNetworkSensor(sensorId, sampledAt, networkBinding);

        return Task.FromResult(reading);
    }

    private void AddStorageSensors(List<SensorDescriptor> sensors, CancellationToken cancellationToken)
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           PlatformNotSupportedException)
        {
            return;
        }

        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var drive in drives.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                    continue;

                var key = GetUniqueKey(SensorIdNormalizer.Normalize(drive.Name), usedKeys);
                var mountName = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string volumeLabel;
                try { volumeLabel = drive.IsReady ? drive.VolumeLabel : string.Empty; }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                   System.Security.SecurityException)
                {
                    volumeLabel = string.Empty;
                }

                var displayName = string.IsNullOrWhiteSpace(volumeLabel)
                    ? mountName
                    : $"{mountName} ({volumeLabel})";

                AddStorageSensor(sensors, $"storage_{key}_status", $"{displayName} — Status",
                    drive, displayName, StorageReadingKind.Status, "");
                AddStorageSensor(sensors, $"storage_{key}_total_bytes", $"{displayName} — Capacity",
                    drive, displayName, StorageReadingKind.Total, "B");
                AddStorageSensor(sensors, $"storage_{key}_used_bytes", $"{displayName} — Used",
                    drive, displayName, StorageReadingKind.Used, "B");
                AddStorageSensor(sensors, $"storage_{key}_free_bytes", $"{displayName} — Available",
                    drive, displayName, StorageReadingKind.Available, "B");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               System.Security.SecurityException or ArgumentException)
            {
                // A single unavailable removable volume must not prevent other sensors from working.
            }
        }
    }

    private void AddStorageSensor(
        List<SensorDescriptor> sensors,
        string sensorId,
        string name,
        DriveInfo drive,
        string displayName,
        StorageReadingKind kind,
        string unit)
    {
        _storageSensors[sensorId] = new StorageSensorBinding(drive, displayName, kind);
        sensors.Add(new SensorDescriptor(
            new SensorDefinition(sensorId, name, "storage", unit),
            ProviderId,
            IsAvailable: true));
    }

    private void AddNetworkSensors(List<SensorDescriptor> sensors, CancellationToken cancellationToken)
    {
        NetworkInterface[] adapters;
        try
        {
            adapters = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception exception) when (exception is NetworkInformationException or
                                           PlatformNotSupportedException or UnauthorizedAccessException)
        {
            return;
        }

        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var adapter in adapters
                     .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rawIdentity = string.IsNullOrWhiteSpace(adapter.Id)
                    ? $"{adapter.Name}_{adapter.Description}"
                    : adapter.Id;
                var key = GetUniqueKey(SensorIdNormalizer.Normalize(rawIdentity), usedKeys);
                var displayName = string.IsNullOrWhiteSpace(adapter.Description)
                    ? adapter.Name
                    : adapter.Description;

                AddNetworkSensor(sensors, $"network_{key}_status", $"{displayName} — Status",
                    key, adapter, NetworkReadingKind.Status, "");
                AddNetworkSensor(sensors, $"network_{key}_receive_bytes_per_second",
                    $"{displayName} — Receive", key, adapter, NetworkReadingKind.Receive, "B/s");
                AddNetworkSensor(sensors, $"network_{key}_send_bytes_per_second",
                    $"{displayName} — Send", key, adapter, NetworkReadingKind.Send, "B/s");
            }
            catch (Exception exception) when (exception is NetworkInformationException or
                                               ArgumentException or InvalidOperationException)
            {
                // Ignore an adapter which cannot expose metadata; continue with the others.
            }
        }
    }

    private void AddNetworkSensor(
        List<SensorDescriptor> sensors,
        string sensorId,
        string name,
        string adapterKey,
        NetworkInterface adapter,
        NetworkReadingKind kind,
        string unit)
    {
        _networkSensors[sensorId] = new NetworkSensorBinding(adapterKey, adapter, kind);
        sensors.Add(new SensorDescriptor(
            new SensorDefinition(sensorId, name, "network", unit),
            ProviderId,
            IsAvailable: true));
    }

    private static SensorReading ReadStorageSensor(
        string sensorId,
        DateTimeOffset sampledAt,
        StorageSensorBinding binding)
    {
        if (binding.Kind == StorageReadingKind.Status)
        {
            return new SensorReading(
                sensorId,
                sampledAt,
                TextValue: binding.Drive.IsReady ? "Ready" : "Not ready");
        }

        if (!binding.Drive.IsReady)
            throw new IOException($"{binding.DisplayName} is not ready.");

        var total = binding.Drive.TotalSize;
        var available = binding.Drive.AvailableFreeSpace;
        var used = Math.Max(0L, total - available);
        var value = binding.Kind switch
        {
            StorageReadingKind.Total => total,
            StorageReadingKind.Used => used,
            _ => available
        };

        return new SensorReading(sensorId, sampledAt, NumericValue: value, Unit: "B");
    }

    private SensorReading? ReadNetworkSensor(
        string sensorId,
        DateTimeOffset sampledAt,
        NetworkSensorBinding binding)
    {
        if (binding.Kind == NetworkReadingKind.Status)
        {
            return new SensorReading(
                sensorId,
                sampledAt,
                TextValue: binding.Adapter.OperationalStatus.ToString());
        }

        if (binding.Adapter.OperationalStatus != OperationalStatus.Up)
            return null;

        var rates = GetNetworkRates(binding.AdapterKey, binding.Adapter, sampledAt);
        if (rates is null)
            return null;

        var rate = binding.Kind == NetworkReadingKind.Receive
            ? rates.Value.ReceiveBytesPerSecond
            : rates.Value.SendBytesPerSecond;
        return new SensorReading(sensorId, sampledAt, NumericValue: rate, Unit: "B/s");
    }

    private NetworkThroughput? GetNetworkRates(
        string adapterKey,
        NetworkInterface adapter,
        DateTimeOffset sampledAt)
    {
        lock (_networkLock)
        {
            // Receive and send descriptors are read sequentially by the sampler.
            // Cache one counter pair briefly so both rates use the same sample.
            if (_cachedNetworkRates.TryGetValue(adapterKey, out var cached) &&
                sampledAt - cached.SampledAt <= TimeSpan.FromMilliseconds(500))
                return cached.Rates;

            var statistics = adapter.GetIPv4Statistics();
            var current = new NetworkCounterSnapshot(
                statistics.BytesReceived,
                statistics.BytesSent,
                sampledAt);

            NetworkThroughput? rates = null;
            if (_previousNetworkCounters.TryGetValue(adapterKey, out var previous))
                rates = NetworkThroughputCalculator.Calculate(previous, current);

            _previousNetworkCounters[adapterKey] = current;
            _cachedNetworkRates[adapterKey] = new CachedNetworkRate(sampledAt, rates);
            return rates;
        }
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
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read physical memory status.");
        return status;
    }

    private static string GetUniqueKey(string proposed, HashSet<string> usedKeys)
    {
        var key = proposed;
        var suffix = 2;
        while (!usedKeys.Add(key))
            key = $"{proposed}_{suffix++}";
        return key;
    }

    private enum MemoryValue { Used, Available, Total }
    private enum StorageReadingKind { Status, Total, Used, Available }
    private enum NetworkReadingKind { Status, Receive, Send }

    private sealed record StorageSensorBinding(
        DriveInfo Drive,
        string DisplayName,
        StorageReadingKind Kind);

    private sealed record NetworkSensorBinding(
        string AdapterKey,
        NetworkInterface Adapter,
        NetworkReadingKind Kind);

    private sealed record CachedNetworkRate(
        DateTimeOffset SampledAt,
        NetworkThroughput? Rates);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

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
