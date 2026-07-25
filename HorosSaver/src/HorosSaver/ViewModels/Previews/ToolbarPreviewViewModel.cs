using HorosSaver.Models;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.ViewModels.Previews;

public sealed class ToolbarPreviewViewModel : ToolbarRegionViewModel
{
    private ToolbarPreviewViewModel()
        : base(new PreviewWorkflowHost())
    {
        Breadcrumb = "Programme / App-Profile";
        ProgramCountLabel = "8 Programme";
        SnapshotCountLabel = "24 Snapshots";
        LastSavePrefix = "Letzter Save: ";
        LastSaveAccent = "vor 2 Std.";
    }

    public static ToolbarPreviewViewModel DesignInstance { get; } = CreateDesignInstance();

    public static ToolbarPreviewViewModel CreateDesignInstance(bool isExpanded = true)
    {
        var vm = new ToolbarPreviewViewModel();
        vm.IsToolbarExpanded = isExpanded;
        return vm;
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
