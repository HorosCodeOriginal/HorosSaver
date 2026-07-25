using CommunityToolkit.Mvvm.ComponentModel;

namespace HorosSaver.Models;

public sealed class ProgramProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public string IconBackground { get; set; } = "#00D2B4";
    public bool IsActive { get; set; }
    public bool IsBound { get; set; }
    public string? Publisher { get; set; }
    public string? InstalledVersion { get; set; }
    public string? InstallLocation { get; set; }
    public string? WingetId { get; set; }
    public int SortOrder { get; set; }
    public List<ProfilePathEntry> Paths { get; set; } = [];
    public List<string> DetailLines { get; set; } = [];
    public DateTimeOffset? LastSnapshotAt { get; set; }
    public CursorSnapshotLevel CursorSnapshotLevel { get; set; } = CursorSnapshotLevel.Standard;
    public string? CustomSnapshotRoot { get; set; }
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
}

public sealed class ProgramGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class DiscoveredProgram
{
    public string DisplayName { get; init; } = string.Empty;
    public string? DisplayVersion { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public string RegistryKeyName { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public ProgramDiscoverySource Sources { get; init; } = ProgramDiscoverySource.Registry;
    public string? TargetPath { get; init; }
    public string? ShortcutPath { get; init; }

    public string SourceLabel => Sources switch
    {
        ProgramDiscoverySource.Registry => "Registry",
        ProgramDiscoverySource.StartMenu => "Startmenü",
        ProgramDiscoverySource.Registry | ProgramDiscoverySource.StartMenu => "Registry · Startmenü",
        _ => "Unbekannt"
    };
}

[Flags]
public enum ProgramDiscoverySource
{
    None = 0,
    Registry = 1,
    StartMenu = 2
}

public enum BindProgramWizardStep
{
    Discover,
    Configure
}

public enum ProfilePathsWizardMode
{
    CreateCustom,
    EditExisting
}

public enum CursorSnapshotLevel
{
    Minimal = 1,
    Standard = 2,
    Full = 3
}

public enum SystemAbbildMode
{
    WindowsSystemImage = 1,
    AllProgramsBundle = 2,
    AllVolumes = 3
}

public sealed class ProfilePathEntry
{
    public string Label { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string RelativeTarget { get; set; } = string.Empty;
}

public sealed class SnapshotInfo
{
    public string Id { get; set; } = string.Empty;
    public string ProgramId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public bool IsCurrent { get; set; }
    public SnapshotKind Kind { get; set; } = SnapshotKind.Full;
    public string? ParentSnapshotId { get; set; }
    public bool CompressionEnabled { get; set; }
    public int ReferencedFileCount { get; set; }
    public int StoredFileCount { get; set; }
    public string? StoragePath { get; set; }
    public bool IsExternal { get; set; }
}

public enum SnapshotKind
{
    Full,
    Incremental
}

public enum ProgramSnapshotDisplayStatus
{
    None,
    Current,
    Outdated,
    Partial
}

public enum SnapshotCaptureTargetMode
{
    StandardInternal,
    ProfileDefault,
    CustomFolder
}

public sealed class SnapshotCaptureTargetChoice
{
    public SnapshotCaptureTargetMode Mode { get; init; } = SnapshotCaptureTargetMode.StandardInternal;
    public string? CustomFolderPath { get; init; }
}

public sealed class SnapshotLocationEntry
{
    public string ProgramId { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
}

public sealed class SnapshotLocationsDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<SnapshotLocationEntry> Locations { get; set; } = [];
}

public enum SnapshotStorageKind
{
    Inline,
    Reference,
    Compressed,
    SkippedLocked
}

public sealed class SnapshotManifest
{
    public int SchemaVersion { get; set; } = 2;
    public string SnapshotId { get; set; } = string.Empty;
    public string ProgramId { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public SnapshotKind Kind { get; set; } = SnapshotKind.Full;
    public string? ParentSnapshotId { get; set; }
    public bool CompressionEnabled { get; set; }
    public bool AclCopyEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? StorageRoot { get; set; }
    public bool IsExternal { get; set; }
    public bool BatRestoreOptimized { get; set; }
    public List<CapturedItem> CapturedItems { get; set; } = [];
    public List<string> SkippedItems { get; set; } = [];
    public List<string> AclWarnings { get; set; } = [];
    public ProgramInstallMetadata? ProgramInstall { get; set; }
}

public sealed class ProgramInstallMetadata
{
    public string? ProgramName { get; set; }
    public string? WingetId { get; set; }
    public string? InstallLocation { get; set; }
    public string? DisplayVersion { get; set; }
    public string? Publisher { get; set; }
}

public sealed class CapturedItem
{
    public string Label { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SnapshotRelativePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool Exists { get; set; }
    public SnapshotStorageKind StorageKind { get; set; } = SnapshotStorageKind.Inline;
    public string? ContentHash { get; set; }
    public string? ReferencedSnapshotId { get; set; }
    public string? ReferencedRelativePath { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long StoredSizeBytes { get; set; }
    public List<CapturedFileRecord> Files { get; set; } = [];
}

public sealed class CapturedFileRecord
{
    public string RelativePath { get; set; } = string.Empty;
    public bool Exists { get; set; } = true;
    public SnapshotStorageKind StorageKind { get; set; } = SnapshotStorageKind.Inline;
    public string? ContentHash { get; set; }
    public string? ReferencedSnapshotId { get; set; }
    public string? ReferencedRelativePath { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long StoredSizeBytes { get; set; }
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 2;
    public bool IncrementalSnapshotsEnabled { get; set; } = true;
    public bool CompressSnapshotsEnabled { get; set; } = true;
    public bool CopyAclsEnabled { get; set; } = true;
    public bool ZuSavenSeedEnabled { get; set; }
    public string? EngineRootPath { get; set; }
    public CursorSnapshotLevel CursorSnapshotLevel { get; set; } = CursorSnapshotLevel.Standard;
    public SystemAbbildMode SystemAbbildMode { get; set; } = SystemAbbildMode.AllProgramsBundle;
    public string? SystemAbbildTarget { get; set; }
    public string? DefaultSnapshotRoot { get; set; }
    public double DetailPanelWidth { get; set; } = 360;
    public bool IsToolbarExpanded { get; set; } = true;
    public SnapshotViewMode SnapshotViewLayout { get; set; } = SnapshotViewMode.Cards;
    public SnapshotGroupMode SnapshotGroupMode { get; set; } = SnapshotGroupMode.ProgramGroup;
    public SnapshotSortMode SnapshotSortMode { get; set; } = SnapshotSortMode.NewestFirst;
    public bool SnapshotOnlyWithSnapshots { get; set; } = true;
    public int SnapshotDateRangeDays { get; set; }
}

public enum SnapshotViewMode
{
    Cards,
    CompactList,
    Table,
    Gallery,
    CompactGrid,
    Chronology,
    Tree
}

public enum SnapshotGroupMode
{
    ProgramGroup,
    Program,
    None,
    ByDate
}

public enum SnapshotSortMode
{
    NewestFirst,
    OldestFirst,
    NameAsc,
    SizeDesc
}

public partial class NavigationItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isActive;
}

public sealed class ProfileStoreDocument
{
    public int SchemaVersion { get; set; } = 2;
    public List<ProgramProfile> Profiles { get; set; } = [];
    public List<ProgramGroup> Groups { get; set; } = [];
}

public enum MainContentView
{
    Programme,
    Snapshots,
    Timeline,
    Wiederherstellen,
    Einstellungen
}

public enum SnapshotResultStatus
{
    Success,
    Partial,
    Failed,
    Cancelled
}

public sealed class SnapshotOperationResult
{
    public SnapshotResultStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public SnapshotInfo? Snapshot { get; init; }
    public IReadOnlyList<string> AclWarnings { get; init; } = [];
    public int SkippedLockedCount { get; init; }
    public IReadOnlyList<string> SkippedLockedPaths { get; init; } = [];
}

public sealed class RestoreOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RestoredCount { get; init; }
    public int SkippedCount { get; init; }
    public int TotalCount { get; init; }
    public bool IsPartialSuccess => Success && SkippedCount > 0;
    public IReadOnlyList<string> ErrorDetails { get; init; } = [];
    public IReadOnlyList<string> AclWarnings { get; init; } = [];
    public bool ProgramInstallAttempted { get; init; }
    public bool ProgramInstallSucceeded { get; init; }
    public IReadOnlyList<string> InstallLog { get; init; } = [];
}

public sealed class RestoreProgressReport
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string CurrentItemLabel { get; init; } = string.Empty;
    public double Percent => Total == 0 ? 0 : (double)Current / Total * 100;
}

public sealed class SnapshotProgressReport
{
    public int Current { get; init; }
    public int Total { get; init; }
    public string CurrentPath { get; init; } = string.Empty;
    public string PhaseLabel { get; init; } = string.Empty;
    public double Percent => Total == 0 ? 0 : (double)Current / Total * 100;
}

public sealed record RestoreBatchSelection(string ProgramId, SnapshotInfo Snapshot);

public enum RestoreWizardStep
{
    Auswahl,
    Fortschritt,
    Ergebnis
}

public enum RestoreTargetMode
{
    OriginalPaths,
    CustomRoot,
    AlternateUserProfile
}

public sealed class RestoreOptions
{
    public RestoreTargetMode Mode { get; init; } = RestoreTargetMode.OriginalPaths;
    public string? CustomRootPath { get; init; }
    public string? AlternateUserProfilePath { get; init; }
    public bool OverwriteConfirmed { get; init; }
    public bool ReinstallProgram { get; init; }

    public bool RequiresExplicitOverwrite => Mode != RestoreTargetMode.OriginalPaths;

    public static RestoreOptions Original => new() { Mode = RestoreTargetMode.OriginalPaths };
}

public sealed class RestorePathPreview
{
    public string Label { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public bool TargetExists { get; init; }
    public bool IsRemapped { get; init; }
}

public enum SnapshotDiffKind
{
    Added,
    Removed,
    Changed
}

public sealed class SnapshotFileEntry
{
    public string RelativePath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public string? ContentHash { get; init; }
}

public sealed class SnapshotFileDiff
{
    public SnapshotDiffKind Kind { get; init; }
    public string RelativePath { get; init; } = string.Empty;
    public long? OlderSizeBytes { get; init; }
    public long? NewerSizeBytes { get; init; }
    public DateTimeOffset? OlderModifiedAt { get; init; }
    public DateTimeOffset? NewerModifiedAt { get; init; }
    public string? OlderHash { get; init; }
    public string? NewerHash { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class SystemBundleManifest
{
    public string Kind { get; set; } = "system-bundle";
    public string BundleId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public SystemAbbildMode Mode { get; set; } = SystemAbbildMode.AllProgramsBundle;
    public List<SystemBundleProfileEntry> Profiles { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class SystemBundleProfileEntry
{
    public string ProgramId { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class SystemImageOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public SystemAbbildMode Mode { get; init; }
    public string? BundleId { get; init; }
    public string? LogFilePath { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class SnapshotCompareResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ProgramId { get; init; } = string.Empty;
    public string ProgramName { get; init; } = string.Empty;
    public string OlderSnapshotId { get; init; } = string.Empty;
    public string NewerSnapshotId { get; init; } = string.Empty;
    public string OlderSnapshotLabel { get; init; } = string.Empty;
    public string NewerSnapshotLabel { get; init; } = string.Empty;
    public DateTimeOffset OlderCreatedAt { get; init; }
    public DateTimeOffset NewerCreatedAt { get; init; }
    public IReadOnlyList<SnapshotFileDiff> Differences { get; init; } = [];
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ChangedCount { get; init; }
}
