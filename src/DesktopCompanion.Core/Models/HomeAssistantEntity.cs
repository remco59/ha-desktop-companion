namespace DesktopCompanion.Core.Models;

public sealed record HomeAssistantEntity(
    string EntityId,
    string State,
    string? FriendlyName,
    string? Domain,
    IReadOnlyDictionary<string, object?> Attributes);
