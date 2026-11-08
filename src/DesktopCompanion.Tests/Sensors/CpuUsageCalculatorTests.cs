using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;
using Xunit;

namespace DesktopCompanion.Tests.Sensors;

public sealed class CpuUsageCalculatorTests
{
    [Fact]
    public void Calculate_returns_busy_percentage_from_counter_deltas()
    {
        var previous = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 200, UserTime: 100);
        var current = new CpuTimesSnapshot(IdleTime: 150, KernelTime: 300, UserTime: 200);

        var result = CpuUsageCalculator.Calculate(previous, current);

        Assert.NotNull(result);
        Assert.Equal(75d, result.Value, precision: 4);
    }

    [Fact]
    public void Calculate_returns_zero_when_all_elapsed_time_is_idle()
    {
        var previous = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 100, UserTime: 50);
        var current = new CpuTimesSnapshot(IdleTime: 200, KernelTime: 200, UserTime: 50);

        Assert.Equal(0d, CpuUsageCalculator.Calculate(previous, current));
    }

    [Fact]
    public void Calculate_returns_one_hundred_when_no_elapsed_time_is_idle()
    {
        var previous = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 100, UserTime: 50);
        var current = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 200, UserTime: 100);

        Assert.Equal(100d, CpuUsageCalculator.Calculate(previous, current));
    }

    [Fact]
    public void Calculate_returns_null_when_counters_have_not_advanced()
    {
        var sample = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 200, UserTime: 100);

        Assert.Null(CpuUsageCalculator.Calculate(sample, sample));
    }

    [Fact]
    public void Calculate_returns_null_for_regressing_or_inconsistent_counters()
    {
        var previous = new CpuTimesSnapshot(IdleTime: 100, KernelTime: 200, UserTime: 100);
        var regressed = new CpuTimesSnapshot(IdleTime: 90, KernelTime: 300, UserTime: 200);
        var inconsistent = new CpuTimesSnapshot(IdleTime: 300, KernelTime: 250, UserTime: 100);

        Assert.Null(CpuUsageCalculator.Calculate(previous, regressed));
        Assert.Null(CpuUsageCalculator.Calculate(previous, inconsistent));
    }
}
