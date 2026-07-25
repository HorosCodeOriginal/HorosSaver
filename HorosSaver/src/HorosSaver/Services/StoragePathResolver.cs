namespace HorosSaver.Services;

public sealed class StoragePathResolver : IStoragePathResolver
{
    public StoragePathResolver()
        : this(AppStoragePaths.Resolve())
    {
    }

    public StoragePathResolver(AppStorageLayout layout)
    {
        AppDirectory = layout.AppDirectory;
        DataRoot = layout.DataRoot;
        LogsRoot = layout.LogsRoot;
        IsPortable = layout.IsPortable;

        SnapshotsRoot = Path.Combine(DataRoot, "snapshots");
        ProfilesFilePath = Path.Combine(DataRoot, "profiles.json");
        SettingsFilePath = Path.Combine(DataRoot, "settings.json");
        SnapshotLocationsIndexPath = Path.Combine(DataRoot, "snapshot-locations.json");
    }

    public string AppDirectory { get; }
    public string DataRoot { get; }
    public string LogsRoot { get; }
    public string SnapshotsRoot { get; }
    public string ProfilesFilePath { get; }
    public string SettingsFilePath { get; }
    public string SnapshotLocationsIndexPath { get; }
    public bool IsPortable { get; }

    public string GetSnapshotDirectory(string snapshotId)
        => Path.Combine(SnapshotsRoot, snapshotId);

    public string GetProgramSnapshotsDirectory(string programId)
        => Path.Combine(SnapshotsRoot, programId);

    public void EnsureDataDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SnapshotsRoot);
        Directory.CreateDirectory(LogsRoot);
    }
}
