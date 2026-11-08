using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;
using Xunit;

namespace DesktopCompanion.Tests.Sensors;

public sealed class NetworkThroughputCalculatorTests
{
    [Fact]
    public void Calculate_returns_receive_and_send_rates_in_bytes_per_second()
    {
        var previous = new NetworkCounterSnapshot(1_000, 2_000, DateTimeOffset.UnixEpoch);
        var current = new NetworkCounterSnapshot(5_000, 8_000, DateTimeOffset.UnixEpoch.AddSeconds(2));

        var result = NetworkThroughputCalculator.Calculate(previous, current);

        Assert.NotNull(result);
        Assert.Equal(2_000d, result.Value.ReceiveBytesPerSecond);
        Assert.Equal(3_000d, result.Value.SendBytesPerSecond);
    }

    [Fact]
    public void Calculate_returns_zero_when_no_bytes_were_transferred()
    {
        var previous = new NetworkCounterSnapshot(1_000, 2_000, DateTimeOffset.UnixEpoch);
        var current = new NetworkCounterSnapshot(1_000, 2_000, DateTimeOffset.UnixEpoch.AddSeconds(1));

        var result = NetworkThroughputCalculator.Calculate(previous, current);

        Assert.Equal(0d, result?.ReceiveBytesPerSecond);
        Assert.Equal(0d, result?.SendBytesPerSecond);
    }

    [Fact]
    public void Calculate_returns_null_for_counter_reset_or_invalid_time()
    {
        var previous = new NetworkCounterSnapshot(1_000, 2_000, DateTimeOffset.UnixEpoch);
        var reset = new NetworkCounterSnapshot(100, 3_000, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var sameTime = new NetworkCounterSnapshot(2_000, 3_000, DateTimeOffset.UnixEpoch);

        Assert.Null(NetworkThroughputCalculator.Calculate(previous, reset));
        Assert.Null(NetworkThroughputCalculator.Calculate(previous, sameTime));
    }

    [Fact]
    public void Calculate_returns_null_for_negative_counter_values()
    {
        var previous = new NetworkCounterSnapshot(-1, 2_000, DateTimeOffset.UnixEpoch);
        var current = new NetworkCounterSnapshot(100, 3_000, DateTimeOffset.UnixEpoch.AddSeconds(1));

        Assert.Null(NetworkThroughputCalculator.Calculate(previous, current));
    }
}
