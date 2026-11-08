using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Abstractions;

/// <summary>Discovers and launches applications from trusted Windows Start Menu shortcuts.</summary>
public interface IApplicationCatalogService
{
    Task<IReadOnlyList<InstalledApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default);

    Task LaunchAsync(string applicationId, CancellationToken cancellationToken = default);
}
