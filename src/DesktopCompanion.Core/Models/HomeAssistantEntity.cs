namespace DesktopCompanion.Core.Models;

public sealed record HomeAssistantEntity(
    string EntityId,
    string State,
    string? FriendlyName,
    string? Domain,
    IReadOnlyDictionary<string, object?> Attributes)
{
    public bool CanToggle => Domain is "light" or "switch" or "input_boolean" or "fan" or "automation";
}
