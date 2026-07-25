namespace HorosSaver.Services;

public interface IStoragePathResolver
{
    string AppDirectory { get; }
    string DataRoot { get; }
    string LogsRoot { get; }
    string SnapshotsRoot { get; }
    string ProfilesFilePath { get; }
    string SettingsFilePath { get; }
    string SnapshotLocationsIndexPath { get; }
    bool IsPortable { get; }
    string GetSnapshotDirectory(string snapshotId);
    string GetProgramSnapshotsDirectory(string programId);
    void EnsureDataDirectories();
}
