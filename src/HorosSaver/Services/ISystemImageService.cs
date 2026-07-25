using HorosSaver.Models;

namespace HorosSaver.Services;

public interface ISystemImageService
{
    Task<SystemImageOperationResult> CreateAsync(CancellationToken cancellationToken = default);
    Task<RestoreOperationResult> RestoreBundleAsync(
        string bundleId,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListBundleIdsAsync(CancellationToken cancellationToken = default);
}
