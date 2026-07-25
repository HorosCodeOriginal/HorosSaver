using HorosSaver.Models;
using HorosSaver.Services;

var tempRoot = Path.Combine(Path.GetTempPath(), "horossaver-restore-test-" + Guid.NewGuid().ToString("N")[..8]);
var dataRoot = Path.Combine(tempRoot, "data");
var sourceDir = Path.Combine(tempRoot, "source");
var sourceFile = Path.Combine(sourceDir, "settings.json");
var stagingRoot = Path.Combine(tempRoot, "staging");
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
    Id = "restore-smoke",
    Name = "Restore Smoke",
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

var snapshot = await snapshotService.CreateSnapshotAsync(profile);
await File.WriteAllTextAsync(sourceFile, """{"version":999}""");

var customOptions = new RestoreOptions
{
    Mode = RestoreTargetMode.CustomRoot,
    CustomRootPath = stagingRoot,
    OverwriteConfirmed = true
};

var restore = await snapshotService.RestoreSnapshotAsync(
    profile,
    snapshot.Snapshot!,
    selectedRelativePaths: null,
    customOptions);

var expectedTarget = RestorePathRemapper.MapTargetPath(sourceFile, customOptions);
var restoredExists = File.Exists(expectedTarget);
var restoredContent = restoredExists ? await File.ReadAllTextAsync(expectedTarget) : string.Empty;

var originalRestore = await snapshotService.RestoreSnapshotAsync(profile, snapshot.Snapshot!);
var originalOk = originalRestore.Success;

Console.WriteLine($"CUSTOM: {restore.Success} -> {expectedTarget} | exists={restoredExists}");
Console.WriteLine($"ORIGINAL: {originalOk}");
Console.WriteLine($"CONTENT: {restoredContent}");

var ok = restore.Success
    && restoredExists
    && restoredContent.Contains("version")
    && originalOk;

if (ok)
{
    try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { }
}

Console.WriteLine(ok ? "RESTORE_SMOKE_OK" : "RESTORE_SMOKE_FAIL");
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
