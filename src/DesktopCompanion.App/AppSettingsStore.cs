using System.Text.Json;

namespace DesktopCompanion.App;

/// <summary>Persists non-secret application preferences in the current user's local profile.</summary>
internal static class AppSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopCompanion",
        "settings.json");

    public static string? LoadHomeAssistantUrl()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            return settings?.HomeAssistantUrl;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveHomeAssistantUrl(string url)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new AppSettings(url)));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private sealed record AppSettings(string HomeAssistantUrl);
}
