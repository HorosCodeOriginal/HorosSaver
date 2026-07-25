using HorosSaver.Models;

namespace HorosSaver.Services;

public interface ISnapshotCompareService
{
    Task<SnapshotCompareResult> CompareAsync(
        string programId,
        string olderSnapshotId,
        string newerSnapshotId,
        string programName,
        CancellationToken cancellationToken = default);
}
