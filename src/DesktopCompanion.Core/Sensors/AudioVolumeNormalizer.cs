namespace DesktopCompanion.Core.Sensors;

/// <summary>Validates and normalizes master-volume percentages for Windows Core Audio.</summary>
public static class AudioVolumeNormalizer
{
    public static float ToScalar(double volumePercent)
    {
        if (!double.IsFinite(volumePercent))
            throw new ArgumentOutOfRangeException(nameof(volumePercent), "The volume must be a finite percentage.");

        return (float)(Math.Clamp(volumePercent, 0d, 100d) / 100d);
    }

    public static double ToPercentage(float scalar)
    {
        if (!float.IsFinite(scalar))
            throw new ArgumentOutOfRangeException(nameof(scalar), "The volume scalar must be finite.");

        return Math.Clamp(scalar, 0f, 1f) * 100d;
    }
}
