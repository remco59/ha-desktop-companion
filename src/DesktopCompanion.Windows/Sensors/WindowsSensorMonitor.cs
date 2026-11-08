using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Windows.Sensors;

/// <summary>
/// Polls a sensor provider at a fixed, low-frequency interval. Last-known readings
/// are retained and explicitly marked stale when a provider cannot refresh them.
/// </summary>
public sealed class WindowsSensorMonitor : IAsyncDisposable
{
    private readonly ISensorProvider _provider;
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, SensorReading> _lastReadings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastErrors = new(StringComparer.Ordinal);
    private IReadOnlyCollection<SensorDescriptor> _sensors = Array.Empty<SensorDescriptor>();
    private CancellationTokenSource? _stopSource;
    private Task? _loopTask;
    private int _started;

    public WindowsSensorMonitor(ISensorProvider provider, TimeSpan? interval = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _interval = interval ?? TimeSpan.FromSeconds(2);
        if (_interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "The sampling interval must be greater than zero.");
    }

    public event Action<SensorSample>? Sampled;

    public IReadOnlyCollection<SensorDescriptor> Sensors => _sensors;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The sensor monitor has already been started.");

        _stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _stopSource.Token;
        _sensors = await _provider.GetSensorsAsync(token).ConfigureAwait(false);
        await SampleOnceAsync(token).ConfigureAwait(false);
        _loopTask = RunLoopAsync(token);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await SampleOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task SampleOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var sensor in _sensors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!sensor.IsAvailable || !sensor.Definition.IsEnabled)
                continue;

            var sensorId = sensor.Definition.Id;
            try
            {
                var reading = await _provider.ReadAsync(sensorId, cancellationToken).ConfigureAwait(false);
                if (reading is not null)
                {
                    _lastReadings[sensorId] = reading with { IsStale = false };
                    _lastErrors.Remove(sensorId);
                }
                else if (_lastReadings.TryGetValue(sensorId, out var previous))
                {
                    _lastReadings[sensorId] = previous with { IsStale = true };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _lastErrors[sensorId] = exception.Message;
                if (_lastReadings.TryGetValue(sensorId, out var previous))
                    _lastReadings[sensorId] = previous with { IsStale = true };
            }
        }

        var sample = new SensorSample(
            DateTimeOffset.UtcNow,
            new Dictionary<string, SensorReading>(_lastReadings, StringComparer.Ordinal),
            new Dictionary<string, string>(_lastErrors, StringComparer.Ordinal));
        Sampled?.Invoke(sample);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stopSource is null)
            return;

        await _stopSource.CancelAsync().ConfigureAwait(false);
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        _stopSource.Dispose();
        _stopSource = null;
        _loopTask = null;
    }
}
