using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using Windows.Media.Control;

namespace DesktopCompanion.Windows.Media;

/// <summary>
/// Reads and controls the active Windows System Media Transport Controls session.
/// The service only sends standard media transport commands; it does not launch apps
/// or attempt to control sessions that Windows does not expose.
/// </summary>
public sealed class WindowsMediaSessionService : IMediaSessionService
{
    public async Task<MediaSessionState?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var session = manager.GetCurrentSession();
        if (session is null)
            return null;

        var properties = await session.TryGetMediaPropertiesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var playback = session.GetPlaybackInfo();
        var controls = playback.Controls;

        return new MediaSessionState(
            properties.Title ?? string.Empty,
            properties.Artist ?? string.Empty,
            properties.AlbumTitle ?? string.Empty,
            session.SourceAppUserModelId ?? string.Empty,
            playback.PlaybackStatus.ToString(),
            controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled,
            controls.IsPreviousEnabled,
            controls.IsNextEnabled);
    }

    public Task<bool> TogglePlayPauseAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(static session => session.TryTogglePlayPauseAsync(), cancellationToken);

    public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(static session => session.TrySkipPreviousAsync(), cancellationToken);

    public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(static session => session.TrySkipNextAsync(), cancellationToken);

    private static async Task<bool> ExecuteAsync(
        Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var session = manager.GetCurrentSession();
        if (session is null)
            return false;

        return await operation(session);
    }
}
