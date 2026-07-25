using HorosSaver.Models;
using HorosSaver.ViewModels;

namespace HorosSaver.Services;

public enum SnapshotJobState
{
    Queued,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}

public sealed class SnapshotJobCompletedEventArgs
{
    public required string ProgramId { get; init; }
    public required SnapshotOperationResult Result { get; init; }
}

public interface ISnapshotJobManager
{
    event EventHandler<SnapshotJobCompletedEventArgs>? JobCompleted;

    bool Enqueue(
        ProgramProfile profile,
        SnapshotCaptureTargetChoice captureTarget,
        ProgramProfileItemViewModel? programViewModel,
        out string? rejectionReason);

    void Pause(string programId);

    void Resume(string programId);

    void Cancel(string programId);

    SnapshotJobState? GetState(string programId);

    int QueueLength { get; }
}
