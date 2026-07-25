using HorosSaver.Models;
using HorosSaver.Services;

var tempRoot = Path.Combine(Path.GetTempPath(), "horossaver-inc-test-" + Guid.NewGuid().ToString("N")[..8]);
var dataRoot = Path.Combine(tempRoot, "data");
var sourceDir = Path.Combine(tempRoot, "source");
var sourceFile = Path.Combine(sourceDir, "settings.json");
Directory.CreateDirectory(sourceDir);
await File.WriteAllTextAsync(sourceFile, """{"version":1}""");

var paths = new TestStoragePathResolver(dataRoot);
paths.EnsureDataDirectories();
var settings = new AppSettingsService(paths);
await settings.LoadAsync();

var locationsIndex = new SnapshotLocationsIndexService(paths);
var snapshotService = new SnapshotService(paths, settings, new ProgramInstallService(), new ProgramProfileService(paths), locationsIndex);
var profile = new ProgramProfile
{
    Id = "smoke-test",
    Name = "Smoke Test",
    Paths =
    [
        new ProfilePathEntry
        {
            Label = "settings.json",
            SourcePath = sourceFile,
            RelativeTarget = "settings.json",
            IsDirectory = false
        }
    ]
};

var full = await snapshotService.CreateSnapshotAsync(profile);
Console.WriteLine($"FULL: {full.Status} | {full.Message}");

await File.WriteAllTextAsync(sourceFile, """{"version":2}""");
var incremental = await snapshotService.CreateSnapshotAsync(profile);
var incManifest = await snapshotService.LoadSnapshotManifestAsync(profile.Id, incremental.Snapshot!.Id);
Console.WriteLine($"INC: {incremental.Message} | kind={incManifest!.Kind}");

var restore = await snapshotService.RestoreSnapshotAsync(profile, incremental.Snapshot);
var restoredContent = File.Exists(sourceFile) ? await File.ReadAllTextAsync(sourceFile) : string.Empty;
Console.WriteLine($"RESTORE: {restore.Success} | content={restoredContent}");

var referenceSnap = await snapshotService.CreateSnapshotAsync(profile);
var refManifest = await snapshotService.LoadSnapshotManifestAsync(profile.Id, referenceSnap.Snapshot!.Id);
var refItem = refManifest!.CapturedItems.First();
Console.WriteLine($"REF: {referenceSnap.Message} | storage={refItem.StorageKind}");

var ok = full.Snapshot?.Kind == SnapshotKind.Full
    && incManifest.Kind == SnapshotKind.Incremental
    && refItem.StorageKind == SnapshotStorageKind.Reference
    && restore.Success
    && restoredContent.Contains("version");

if (ok)
{
    try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { }
}

Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_FAIL");
return ok ? 0 : 1;

sealed class TestStoragePathResolver : IStoragePathResolver
{
    public TestStoragePathResolver(string dataRoot)
    {
        AppDirectory = dataRoot;
        DataRoot = dataRoot;
        LogsRoot = Path.Combine(dataRoot, "logs");
        SnapshotsRoot = Path.Combine(dataRoot, "snapshots");
        ProfilesFilePath = Path.Combine(dataRoot, "profiles.json");
        SettingsFilePath = Path.Combine(dataRoot, "settings.json");
        IsPortable = true;
    }

    public string AppDirectory { get; }
    public string DataRoot { get; }
    public string LogsRoot { get; }
    public string SnapshotsRoot { get; }
    public string ProfilesFilePath { get; }
    public string SettingsFilePath { get; }
    public bool IsPortable { get; }

    public string GetSnapshotDirectory(string snapshotId) => Path.Combine(SnapshotsRoot, snapshotId);
    public string GetProgramSnapshotsDirectory(string programId) => Path.Combine(SnapshotsRoot, programId);
    public void EnsureDataDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SnapshotsRoot);
        Directory.CreateDirectory(LogsRoot);
    }
}
