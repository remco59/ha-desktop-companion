namespace DesktopCompanion.HomeAssistant.WebSocket;

/// <summary>
/// Helpers for deriving Home Assistant API endpoints from the configured base URI.
/// Reverse-proxy path prefixes are preserved.
/// </summary>
public static class HomeAssistantEndpoint
{
    public static Uri CreateWebSocketUri(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The Home Assistant URL must be absolute.", nameof(baseUri));
        }

        var scheme = baseUri.Scheme.ToLowerInvariant() switch
        {
            "http" => "ws",
            "https" => "wss",
            _ => throw new ArgumentException(
                "The Home Assistant URL must use HTTP or HTTPS.",
                nameof(baseUri))
        };

        var builder = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/api/websocket",
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }
}
