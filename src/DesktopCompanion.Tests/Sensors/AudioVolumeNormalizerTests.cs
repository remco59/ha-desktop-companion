using DesktopCompanion.Core.Sensors;
using Xunit;

namespace DesktopCompanion.Tests.Sensors;

public sealed class AudioVolumeNormalizerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 0.5)]
    [InlineData(100, 1)]
    [InlineData(-20, 0)]
    [InlineData(150, 1)]
    public void ToScalar_clamps_percentage_to_core_audio_range(double percentage, double expected)
    {
        Assert.Equal(expected, AudioVolumeNormalizer.ToScalar(percentage), precision: 5);
    }

    [Fact]
    public void ToScalar_rejects_non_finite_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioVolumeNormalizer.ToScalar(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioVolumeNormalizer.ToScalar(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(-0.5f, 0)]
    [InlineData(0.5f, 50)]
    [InlineData(1.5f, 100)]
    public void ToPercentage_clamps_scalar_to_percentage(float scalar, double expected)
    {
        Assert.Equal(expected, AudioVolumeNormalizer.ToPercentage(scalar), precision: 4);
    }
}
