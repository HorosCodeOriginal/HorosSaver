using HorosSaver.Models;

namespace HorosSaver.Services;

internal sealed class SnapshotCaptureProgressTracker
{
    private int _current;

    public SnapshotCaptureProgressTracker(int total, IProgress<SnapshotProgressReport>? progress)
    {
        Total = total;
        Progress = progress;
    }

    public int Total { get; }

    private IProgress<SnapshotProgressReport>? Progress { get; }

    public void ReportPhase(string phaseLabel)
    {
        Progress?.Report(new SnapshotProgressReport
        {
            Current = _current,
            Total = Total,
            PhaseLabel = phaseLabel
        });
    }

    public void Advance(string? currentPath = null)
    {
        var current = Interlocked.Increment(ref _current);
        Progress?.Report(new SnapshotProgressReport
        {
            Current = current,
            Total = Total,
            CurrentPath = currentPath ?? string.Empty
        });
    }
}
