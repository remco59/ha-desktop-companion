using System.Text.Json;

namespace DesktopCompanion.Core.Models;

/// <summary>
/// A Home Assistant event received over the WebSocket API.
/// Data is cloned from the transport message and remains valid after parsing.
/// </summary>
public sealed record HomeAssistantEvent(
    string EventType,
    JsonElement Data,
    DateTimeOffset? TimeFired);
