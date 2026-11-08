using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Dashboards;

/// <summary>Pure selection rules for configured Home Assistant dashboards.</summary>
public static class DashboardSelection
{
    /// <summary>Initial views in the Desktop Companion Home Assistant dashboard.</summary>
    public static IReadOnlyList<DashboardDefinition> Presets { get; } = Array.AsReadOnly(
        new DashboardDefinition[]
        {
            DashboardDefinition.Default,
            new("media", "Media", "desktop-companion/media", "mdi:music", 1),
            new("system", "System", "desktop-companion/system", "mdi:server", 2)
        });

    public static IReadOnlyList<DashboardDefinition> Normalize(IEnumerable<DashboardDefinition>? dashboards)
    {
        var values = (dashboards ?? []).Where(IsValid).ToList();
        if (values.Count == 0) return Presets;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        values = values.Where(item => ids.Add(item.Id)).ToList();
        if (values.Count == 0) return Presets;

        var defaultIndex = values.FindIndex(item => item.IsDefault);
        if (defaultIndex < 0)
        {
            values[0] = values[0] with { IsDefault = true };
        }
        else
        {
            for (var index = 0; index < values.Count; index++)
                if (index != defaultIndex && values[index].IsDefault)
                    values[index] = values[index] with { IsDefault = false };
        }

        return values.OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static DashboardDefinition Select(
        IReadOnlyList<DashboardDefinition> dashboards,
        string? selectedId)
    {
        ArgumentNullException.ThrowIfNull(dashboards);
        var normalized = Normalize(dashboards);
        return normalized.FirstOrDefault(item =>
                   !string.IsNullOrWhiteSpace(selectedId) &&
                   string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
               ?? normalized.First(item => item.IsDefault);
    }

    private static bool IsValid(DashboardDefinition item) =>
        item is not null &&
        !string.IsNullOrWhiteSpace(item.Id) &&
        !string.IsNullOrWhiteSpace(item.Name) &&
        (string.IsNullOrWhiteSpace(item.Path) ||
         !Uri.TryCreate(item.Path, UriKind.Absolute, out _)) &&
        !item.Path.Contains('?') &&
        !item.Path.Contains('#');
}
