namespace HorosSaver.Services;

/// <summary>
/// Koordiniert Abbruch (CancellationToken) und Pause (ManualResetEventSlim) während der Snapshot-Erfassung.
/// </summary>
public sealed class SnapshotCaptureControls : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ManualResetEventSlim _resumeGate = new(initialState: true);
    private bool _disposed;

    public CancellationToken CancellationToken => _cancellation.Token;

    public bool IsPaused => !_resumeGate.IsSet;

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _resumeGate.Reset();
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _resumeGate.Set();
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellation.Cancel();
        _resumeGate.Set();
    }

    public void WaitIfAllowed(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _resumeGate.Wait(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _cancellation.Token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _resumeGate.Dispose();
        _cancellation.Dispose();
    }
}
