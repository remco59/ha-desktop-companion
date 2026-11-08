using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>Controls the default Windows audio-render endpoint.</summary>
public interface IAudioControlService
{
    Task<AudioEndpointState> GetDefaultOutputStateAsync(CancellationToken cancellationToken = default);

    Task<AudioEndpointState> SetMasterVolumeAsync(
        double volumePercent,
        CancellationToken cancellationToken = default);

    Task<AudioEndpointState> SetMutedAsync(
        bool muted,
        CancellationToken cancellationToken = default);
}
