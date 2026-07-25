using HorosSaver.Models;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.ViewModels.Previews;

public sealed class ProgramsGridPreviewViewModel : ProgramsRegionViewModel
{
    private ProgramsGridPreviewViewModel()
        : base(new PreviewWorkflowHost())
    {
        SetPrograms(CreateMockPrograms());
        SelectedProgram = GetAllPrograms().FirstOrDefault();
    }

    public static ProgramsGridPreviewViewModel DesignInstance { get; } = new();

    private static IEnumerable<ProgramProfileItemViewModel> CreateMockPrograms()
    {
        var profiles = new[]
        {
            CreateProfile(
                "cursor",
                "Cursor",
                "IDE",
                "Vollständiges Profil",
                "CR",
                "#00D2B4",
                0,
                ["Extensions: 47", "Settings: sync", "Keybindings: custom", "Workspace: HorosSaver"],
                new DateTimeOffset(2026, 7, 24, 14, 32, 0, TimeSpan.Zero),
                isActive: true),
            CreateProfile(
                "vscode",
                "VS Code",
                "Editor",
                "Entwicklertools",
                "VS",
                "#007ACC",
                1,
                ["Extensions: 32", "Settings: sync", "Keybindings: custom", "Workspace: HorosSaver"],
                new DateTimeOffset(2026, 7, 24, 10, 11, 0, TimeSpan.Zero)),
            CreateProfile(
                "chrome",
                "Google Chrome",
                "Browser",
                "Web",
                "GC",
                "#4285F4",
                2,
                ["Profile: Work", "Bookmarks: 1.2k", "Extensions: 24"],
                new DateTimeOffset(2026, 7, 24, 9, 45, 0, TimeSpan.Zero)),
            CreateProfile(
                "docker",
                "Docker Desktop",
                "Container",
                "DevOps",
                "DK",
                "#2496ED",
                3,
                ["Containers: 3", "Images: 12", "Volumes: 8"],
                new DateTimeOffset(2026, 7, 23, 16, 20, 0, TimeSpan.Zero)),
            CreateProfile(
                "steam",
                "Steam",
                "Gaming",
                "Plattform",
                "ST",
                "#1B2838",
                4,
                ["Library: 84 Spiele", "Playtime: 256 Std."],
                new DateTimeOffset(2026, 7, 23, 12, 8, 0, TimeSpan.Zero)),
            CreateProfile(
                "jetbrains",
                "JetBrains Toolbox",
                "Tools",
                "IDE Manager",
                "JB",
                "#000000",
                5,
                ["Tools: 7", "Settings: sync"],
                new DateTimeOffset(2026, 7, 22, 18, 33, 0, TimeSpan.Zero)),
            CreateProfile(
                "notepad",
                "Notepad++",
                "Editor",
                "Ohne Snapshot",
                "NP",
                "#6EAA45",
                6,
                ["Plugins: 12"],
                null)
        };

        var items = profiles.Select(profile =>
        {
            var item = new ProgramProfileItemViewModel(profile);
            if (profile.LastSnapshotAt.HasValue)
            {
                item.RefreshSnapshotStatus(profile.LastSnapshotAt);
            }

            return item;
        });

        return items;
    }

    private static ProgramProfile CreateProfile(
        string id,
        string name,
        string category,
        string subtitle,
        string glyph,
        string color,
        int sortOrder,
        string[] detailLines,
        DateTimeOffset? lastSnapshotAt,
        bool isActive = false)
    {
        return new ProgramProfile
        {
            Id = id,
            Name = name,
            Category = category,
            Subtitle = subtitle,
            IconGlyph = glyph,
            IconBackground = color,
            IsActive = isActive,
            SortOrder = sortOrder,
            DetailLines = detailLines.ToList(),
            LastSnapshotAt = lastSnapshotAt,
            Paths = []
        };
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
