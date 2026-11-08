namespace DesktopCompanion.Core.Sensors;

/// <summary>Turns OS-provided device identities into stable, safe sensor-ID segments.</summary>
public static class SensorIdNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var result = new System.Text.StringBuilder(value.Length);
        var previousSeparator = false;
        foreach (var character in value.Trim())
        {
            var isAsciiAlphaNumeric =
                character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
            if (isAsciiAlphaNumeric)
            {
                result.Append(char.ToLowerInvariant(character));
                previousSeparator = false;
            }
            else if (!previousSeparator && result.Length > 0)
            {
                result.Append('_');
                previousSeparator = true;
            }
        }

        var normalized = result.ToString().Trim('_');
        return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
    }
}
