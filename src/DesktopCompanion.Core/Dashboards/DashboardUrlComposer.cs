namespace DesktopCompanion.Core.Dashboards;

/// <summary>Composes dashboard URLs without losing an instance's reverse-proxy prefix or query.</summary>
public static class DashboardUrlComposer
{
    public static Uri Compose(Uri instanceUri, string dashboardPath, bool kioskMode = true)
    {
        ArgumentNullException.ThrowIfNull(instanceUri);
        if (!instanceUri.IsAbsoluteUri ||
            (instanceUri.Scheme != Uri.UriSchemeHttp && instanceUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("The Home Assistant instance URL must be an absolute HTTP or HTTPS URL.", nameof(instanceUri));

        var baseBuilder = new UriBuilder(instanceUri);
        var basePath = baseBuilder.Path.TrimEnd('/');
        var requestedPath = NormalizeDashboardPath(dashboardPath);
        var combinedPath = string.IsNullOrEmpty(requestedPath)
            ? (string.IsNullOrEmpty(basePath) ? "/" : basePath + "/")
            : (string.IsNullOrEmpty(basePath) ? "" : basePath) + "/" + requestedPath;

        var dashboardBuilder = new UriBuilder(instanceUri)
        {
            Path = combinedPath,
            Fragment = string.Empty
        };

        if (!kioskMode || HasQueryParameter(dashboardBuilder.Query, "kiosk") ||
            HasQueryParameter(dashboardBuilder.Query, "disable_km"))
            return dashboardBuilder.Uri;

        var query = dashboardBuilder.Query.TrimStart('?');
        dashboardBuilder.Query = string.IsNullOrEmpty(query) ? "kiosk" : query + "&kiosk";
        return dashboardBuilder.Uri;
    }

    private static string NormalizeDashboardPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var trimmed = path.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
            throw new ArgumentException("Dashboard paths must be relative to the Home Assistant instance.", nameof(path));

        return trimmed.Trim('/');
    }

    private static bool HasQueryParameter(string query, string name) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Any(parameter => string.Equals(
                Uri.UnescapeDataString(parameter.Split('=', 2)[0]),
                name,
                StringComparison.OrdinalIgnoreCase));
}
