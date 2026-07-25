using System.Collections.Concurrent;
using System.Threading.Channels;
using Avalonia.Threading;
using HorosSaver.Models;
using HorosSaver.ViewModels;

namespace HorosSaver.Services;

public sealed class SnapshotJobManager : ISnapshotJobManager, IDisposable
{
    private readonly ISnapshotService _snapshotService;
    private readonly Channel<SnapshotJob> _queue = Channel.CreateUnbounded<SnapshotJob>();
    private readonly ConcurrentDictionary<string, SnapshotJob> _jobsByProgramId = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _processorTask;
    private int _queueLength;

    public SnapshotJobManager(ISnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
        _processorTask = Task.Run(ProcessQueueAsync);
    }

    public event EventHandler<SnapshotJobCompletedEventArgs>? JobCompleted;

    public int QueueLength => Volatile.Read(ref _queueLength);

    public bool Enqueue(
        ProgramProfile profile,
        SnapshotCaptureTargetChoice captureTarget,
        ProgramProfileItemViewModel? programViewModel,
        out string? rejectionReason)
    {
        rejectionReason = null;

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            rejectionReason = "Programm ohne gültige ID.";
            return false;
        }

        if (_jobsByProgramId.ContainsKey(profile.Id))
        {
            rejectionReason = "Für dieses Programm läuft bereits ein Snapshot oder es wartet in der Warteschlange.";
            return false;
        }

        var job = new SnapshotJob
        {
            ProgramId = profile.Id,
            Profile = profile,
            CaptureTarget = captureTarget,
            ProgramViewModel = programViewModel,
            Controls = new SnapshotCaptureControls()
        };

        if (!_jobsByProgramId.TryAdd(profile.Id, job))
        {
            rejectionReason = "Für dieses Programm läuft bereits ein Snapshot oder es wartet in der Warteschlange.";
            job.Controls.Dispose();
            return false;
        }

        Interlocked.Increment(ref _queueLength);
        PostToUi(() => programViewModel?.BeginQueued());
        AppFileLogger.Info($"Snapshot in Warteschlange: {profile.Name} ({profile.Id}).");

        if (!_queue.Writer.TryWrite(job))
        {
            Interlocked.Decrement(ref _queueLength);
            _jobsByProgramId.TryRemove(profile.Id, out _);
            job.Controls.Dispose();
            rejectionReason = "Warteschlange nicht verfügbar.";
            return false;
        }

        return true;
    }

    public void Pause(string programId)
    {
        if (!_jobsByProgramId.TryGetValue(programId, out var job))
        {
            return;
        }

        if (job.State is not (SnapshotJobState.Running or SnapshotJobState.Paused))
        {
            return;
        }

        job.Controls.Pause();
        job.State = SnapshotJobState.Paused;
        PostToUi(() => job.ProgramViewModel?.SetPaused(true));
        AppFileLogger.Info($"Snapshot pausiert: {job.Profile.Name} ({programId}).");
    }

    public void Resume(string programId)
    {
        if (!_jobsByProgramId.TryGetValue(programId, out var job))
        {
            return;
        }

        if (job.State is not (SnapshotJobState.Running or SnapshotJobState.Paused))
        {
            return;
        }

        job.Controls.Resume();
        job.State = SnapshotJobState.Running;
        PostToUi(() => job.ProgramViewModel?.SetPaused(false));
        AppFileLogger.Info($"Snapshot fortgesetzt: {job.Profile.Name} ({programId}).");
    }

    public void Cancel(string programId)
    {
        if (!_jobsByProgramId.TryGetValue(programId, out var job))
        {
            return;
        }

        if (job.State == SnapshotJobState.Queued)
        {
            job.State = SnapshotJobState.Cancelled;
            job.Controls.Cancel();
            PostToUi(() => job.ProgramViewModel?.EndSnapshotCancelled());
            _jobsByProgramId.TryRemove(programId, out _);
            job.Controls.Dispose();
            AppFileLogger.Warning($"Snapshot aus Warteschlange abgebrochen: {job.Profile.Name} ({programId}).");
            RaiseCompleted(programId, new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Cancelled,
                Message = "Snapshot abgebrochen."
            });
            return;
        }

        job.Controls.Cancel();
        AppFileLogger.Warning($"Snapshot-Abbruch angefordert: {job.Profile.Name} ({programId}).");
    }

    public SnapshotJobState? GetState(string programId)
        => _jobsByProgramId.TryGetValue(programId, out var job) ? job.State : null;

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queueLength);

                if (job.State == SnapshotJobState.Cancelled)
                {
                    continue;
                }

                await ExecuteJobAsync(job).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task ExecuteJobAsync(SnapshotJob job)
    {
        var viewModel = job.ProgramViewModel;
        SnapshotOperationResult result;

        try
        {
            job.State = SnapshotJobState.Running;
            PostToUi(() => viewModel?.BeginSnapshot());
            AppFileLogger.Info($"Snapshot gestartet: {job.Profile.Name} ({job.ProgramId}).");

            var progress = new Progress<SnapshotProgressReport>(report =>
            {
                PostToUi(() => viewModel?.ApplySnapshotProgress(report));
            });

            result = await _snapshotService.CreateSnapshotAsync(
                job.Profile,
                captureTarget: job.CaptureTarget,
                progress: progress,
                captureControls: job.Controls,
                cancellationToken: job.Controls.CancellationToken).ConfigureAwait(false);

            job.State = result.Status switch
            {
                SnapshotResultStatus.Cancelled => SnapshotJobState.Cancelled,
                SnapshotResultStatus.Failed => SnapshotJobState.Failed,
                _ => SnapshotJobState.Completed
            };

            PostToUi(() =>
            {
                switch (result.Status)
                {
                    case SnapshotResultStatus.Cancelled:
                        viewModel?.EndSnapshotCancelled();
                        break;
                    case SnapshotResultStatus.Failed:
                        viewModel?.EndSnapshotError(result.Message);
                        break;
                    default:
                        viewModel?.EndSnapshotSuccess(result.SkippedLockedCount, result.SkippedLockedPaths);
                        AppFileLogger.Info($"Snapshot erfolgreich: {job.Profile.Name} ({job.ProgramId}).");
                        break;
                }
            });
        }
        catch (OperationCanceledException)
        {
            job.State = SnapshotJobState.Cancelled;
            result = new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Cancelled,
                Message = "Snapshot abgebrochen."
            };
            PostToUi(() => viewModel?.EndSnapshotCancelled());
        }
        catch (Exception ex)
        {
            job.State = SnapshotJobState.Failed;
            result = new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Failed,
                Message = ex.Message
            };
            PostToUi(() => viewModel?.EndSnapshotError(ex.Message));
        }
        finally
        {
            _jobsByProgramId.TryRemove(job.ProgramId, out _);
            job.Controls.Dispose();
        }

        RaiseCompleted(job.ProgramId, result);
    }

    private void RaiseCompleted(string programId, SnapshotOperationResult result)
    {
        JobCompleted?.Invoke(this, new SnapshotJobCompletedEventArgs
        {
            ProgramId = programId,
            Result = result
        });
    }

    private static void PostToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _queue.Writer.TryComplete();
        try
        {
            _processorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }

        _shutdown.Dispose();
    }

    private sealed class SnapshotJob
    {
        public required string ProgramId { get; init; }
        public required ProgramProfile Profile { get; init; }
        public required SnapshotCaptureTargetChoice CaptureTarget { get; init; }
        public ProgramProfileItemViewModel? ProgramViewModel { get; init; }
        public required SnapshotCaptureControls Controls { get; init; }
        public SnapshotJobState State { get; set; } = SnapshotJobState.Queued;
    }
}
