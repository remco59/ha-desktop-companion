using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>Reads and controls the current global Windows media session.</summary>
public interface IMediaSessionService
{
    Task<MediaSessionState?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);
    Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default);
    Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default);
    Task<bool> SkipNextAsync(CancellationToken cancellationToken = default);
}
