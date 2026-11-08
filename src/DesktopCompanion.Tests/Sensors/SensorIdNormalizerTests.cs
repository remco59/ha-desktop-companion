using DesktopCompanion.Core.Sensors;
using Xunit;

namespace DesktopCompanion.Tests.Sensors;

public sealed class SensorIdNormalizerTests
{
    [Theory]
    [InlineData(@"C:\", "c")]
    [InlineData("Wi-Fi (Intel) #2", "wi_fi_intel_2")]
    [InlineData("{9A5E-ABCD}", "9a5e_abcd")]
    public void Normalize_creates_a_stable_lowercase_segment(string value, string expected)
    {
        Assert.Equal(expected, SensorIdNormalizer.Normalize(value));
    }

    [Fact]
    public void Normalize_uses_unknown_for_empty_or_only_separator_values()
    {
        Assert.Equal("unknown", SensorIdNormalizer.Normalize("  "));
        Assert.Equal("unknown", SensorIdNormalizer.Normalize("___---"));
    }
}
