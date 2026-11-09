namespace DesktopCompanion.Core.Models;

/// <summary>Metadata and supported controls for the current Windows media session.</summary>
public sealed record MediaSessionState(
    string Title,
    string Artist,
    string Album,
    string ApplicationId,
    string PlaybackStatus,
    bool CanTogglePlayPause,
    bool CanSkipPrevious,
    bool CanSkipNext);
