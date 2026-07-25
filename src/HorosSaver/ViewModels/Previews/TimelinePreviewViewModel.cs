using HorosSaver.Models;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.ViewModels.Previews;

public sealed class TimelinePreviewViewModel : TimelineRegionViewModel
{
    private TimelinePreviewViewModel()
        : base(new PreviewWorkflowHost())
    {
        ProgramName = "Cursor";
        Title = "Cursor — Zeitzustände";
        SetSnapshots(CreateMockSnapshots());
        SelectedSnapshot = Snapshots.FirstOrDefault();
        HasSnapshots = true;
        CanCompare = true;
    }

    public static TimelinePreviewViewModel DesignInstance { get; } = new();

    private static IEnumerable<SnapshotItemViewModel> CreateMockSnapshots()
    {
        var now = DateTimeOffset.Now;
        var entryOneTime = new DateTimeOffset(now.Date.AddHours(14).AddMinutes(32), now.Offset);
        if (entryOneTime > now)
        {
            entryOneTime = now.AddMinutes(-45);
        }

        var snapshots = new[]
        {
            CreateSnapshot(
                "snap-1",
                "Cursor — 2026-07-24 14:32",
                "Vollständig",
                entryOneTime,
                (long)(2.4 * 1024 * 1024 * 1024),
                isCurrent: true,
                index: 1),
            CreateSnapshot(
                "snap-2",
                "Cursor — 2026-07-22 09:15",
                "Vor Installation Docker",
                now.Date.AddDays(-1).AddHours(9).AddMinutes(15),
                0,
                index: 2),
            CreateSnapshot(
                "snap-3",
                "Cursor — 2026-07-18 18:00",
                "Pre-Migration",
                now.AddDays(-6),
                0,
                index: 3),
            CreateSnapshot(
                "snap-4",
                "Cursor — 2026-07-10 11:45",
                "Baseline Setup",
                now.AddDays(-14),
                0,
                index: 4)
        };

        return snapshots;
    }

    private static SnapshotItemViewModel CreateSnapshot(
        string id,
        string name,
        string description,
        DateTimeOffset createdAt,
        long sizeBytes,
        int index,
        bool isCurrent = false)
    {
        return new SnapshotItemViewModel(new SnapshotInfo
        {
            Id = id,
            ProgramId = "cursor",
            Name = name,
            Description = description,
            CreatedAt = createdAt,
            SizeBytes = sizeBytes,
            IsCurrent = isCurrent
        }, index);
    }

    private sealed class PreviewWorkflowHost : IWorkflowHost
    {
        public bool IsBusy => false;
        public bool HasSelectedProgram => true;

        public Task SaveSnapshotAsync() => Task.CompletedTask;

        public Task SaveSnapshotForProgramAsync(string? programId) => Task.CompletedTask;

        public Task SaveGroupSnapshotAsync(string? groupId) => Task.CompletedTask;

        public Task AutoCreateProgramGroupsAsync() => Task.CompletedTask;

        public Task OpenRestoreWizardAsync(string? programId = null, SnapshotInfo? snapshot = null) => Task.CompletedTask;

        public Task OpenRestoreWizardForGroupAsync(
            string groupTitle,
            IReadOnlyList<SnapshotOverviewItemViewModel> snapshots) => Task.CompletedTask;

        public Task OpenRestoreWizardForSelectionAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots)
            => Task.CompletedTask;

        public Task DeleteSnapshotsBatchAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots)
            => Task.CompletedTask;

        public Task OpenBindProgramWizardAsync() => Task.CompletedTask;

        public Task OpenCustomPathsWizardAsync() => Task.CompletedTask;

        public Task OpenEditProfilePathsAsync(ProgramProfile? profile = null) => Task.CompletedTask;

        public Task CompareSnapshotsAsync(SnapshotItemViewModel? selectedSnapshot, SnapshotItemViewModel? compareWithSnapshot)
            => Task.CompletedTask;

        public Task CompareSnapshotOverviewAsync(SnapshotOverviewItemViewModel snapshot) => Task.CompletedTask;

        public Task DeleteProgramProfileAsync(string? programId) => Task.CompletedTask;

        public Task DissolveProgramGroupAsync(string? groupId) => Task.CompletedTask;

        public Task DeleteProgramGroupWithProfilesAsync(string? groupId) => Task.CompletedTask;

        public Task DeleteSnapshotAsync(string programId, string snapshotId) => Task.CompletedTask;

        public Task OpenInstallFolderAsync(string? programId) => Task.CompletedTask;

        public Task OpenSnapshotInExplorerAsync(string programId, string snapshotId) => Task.CompletedTask;

        public Task CopySnapshotPathAsync(string programId, string snapshotId) => Task.CompletedTask;
        public Task EditSnapshotAsync(string programId, string snapshotId) => Task.CompletedTask;

        public Task CreateSystemAbbildAsync() => Task.CompletedTask;

        public Task<EngineExecutionResult> RunEngineActionAsync(
            AppReinstallEngineAction action,
            IProgress<string>? logProgress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EngineExecutionResult.Missing("Preview"));

        public void NavigateToSettings()
        {
        }
    }
}
