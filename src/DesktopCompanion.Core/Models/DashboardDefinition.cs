namespace DesktopCompanion.Core.Models;

/// <summary>Describes a Home Assistant dashboard or a specific view within one.</summary>
public sealed record DashboardDefinition(
    string Id,
    string Name,
    string Path,
    string? Icon = null,
    int SortOrder = 0,
    bool IsDefault = false)
{
    public static DashboardDefinition Default { get; } =
        new("control", "Control", "desktop-companion/control", "mdi:home-variant", 0, true);
}
