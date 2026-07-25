using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IInstalledProgramDiscoveryService
{
    Task<IReadOnlyList<DiscoveredProgram>> DiscoverInstalledProgramsAsync(
        CancellationToken cancellationToken = default);
}
