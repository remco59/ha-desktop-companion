using System.Text.Json;
using DesktopCompanion.Core.Dashboards;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.App;

/// <summary>Persists non-secret application preferences in the current user's local profile.</summary>
internal static class AppSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopCompanion",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string? LoadHomeAssistantUrl() => Load().HomeAssistantUrl;

    public static IReadOnlyList<DashboardDefinition> LoadDashboards() =>
        DashboardSelection.Normalize(Load().Dashboards);

    public static string? LoadSelectedDashboardId() => Load().SelectedDashboardId;

    public static void SaveHomeAssistantUrl(string url) =>
        Save(current => current with { HomeAssistantUrl = url });

    public static void SaveDashboardSettings(
        IReadOnlyList<DashboardDefinition> dashboards,
        string selectedDashboardId) =>
        Save(current => current with
        {
            Dashboards = DashboardSelection.Normalize(dashboards).ToArray(),
            SelectedDashboardId = selectedDashboardId
        });

    private static SettingsDocument Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new SettingsDocument();
            return JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(SettingsPath), JsonOptions)
                   ?? new SettingsDocument();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsDocument();
        }
    }

    private static void Save(Func<SettingsDocument, SettingsDocument> update)
    {
        var current = Load();
        var next = update(current);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(next, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private sealed record SettingsDocument
    {
        public string? HomeAssistantUrl { get; init; }
        public DashboardDefinition[]? Dashboards { get; init; }
        public string? SelectedDashboardId { get; init; }
    }
}
