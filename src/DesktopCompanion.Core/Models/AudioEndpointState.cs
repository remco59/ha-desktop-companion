namespace DesktopCompanion.Core.Models;

/// <summary>Current state of the default Windows playback endpoint.</summary>
public sealed record AudioEndpointState(
    double VolumePercent,
    bool IsMuted,
    string EndpointId);
