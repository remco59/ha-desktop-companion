using System.Text.Json.Serialization;

namespace DesktopCompanion.HomeAssistant.Authentication;

public sealed class HomeAssistantOAuthTokens
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonIgnore]
    public string ClientId { get; set; } = string.Empty;
}
