using HorosSaver.Models;

namespace HorosSaver.Services;

public interface ISnapshotService
{
    Task<IReadOnlyList<SnapshotInfo>> LoadSnapshotsAsync(string programId, CancellationToken cancellationToken = default);
    Task<SnapshotManifest?> LoadSnapshotManifestAsync(string programId, string snapshotId, CancellationToken cancellationToken = default);
    Task<SnapshotOperationResult> CreateSnapshotAsync(
        ProgramProfile profile,
        string? description = null,
        SnapshotCaptureTargetChoice? captureTarget = null,
        IProgress<SnapshotProgressReport>? progress = null,
        SnapshotCaptureControls? captureControls = null,
        CancellationToken cancellationToken = default);
    Task<SnapshotOperationResult> UpdateSnapshotAsync(
        ProgramProfile profile,
        string snapshotId,
        string displayName,
        string? newStorageRoot,
        CancellationToken cancellationToken = default);
    Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        CancellationToken cancellationToken = default);
    Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        IReadOnlyCollection<string>? selectedRelativePaths,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
    Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        IReadOnlyCollection<string>? selectedRelativePaths,
        RestoreOptions? options,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
    Task RefreshCurrentFlagsAsync(string programId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSnapshotAsync(string programId, string snapshotId, CancellationToken cancellationToken = default);
    string GetSnapshotDirectoryPath(string programId, string snapshotId);
}
