using System.Security.AccessControl;
using HorosSaver.Models;
using HorosSaver.Services;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("ACL_SMOKE_SKIP (non-Windows)");
    return 0;
}

var tempRoot = Path.Combine(Path.GetTempPath(), "horossaver-acl-test-" + Guid.NewGuid().ToString("N")[..8]);
var dataRoot = Path.Combine(tempRoot, "data");
var sourceDir = Path.Combine(tempRoot, "source");
var sourceFile = Path.Combine(sourceDir, "secured.json");
Directory.CreateDirectory(sourceDir);
await File.WriteAllTextAsync(sourceFile, """{"secured":true}""");

var sourceSecurity = new FileSecurity();
var rule = new FileSystemAccessRule(
    Environment.UserName,
    FileSystemRights.Read | FileSystemRights.Write,
    AccessControlType.Allow);
sourceSecurity.AddAccessRule(rule);
new FileInfo(sourceFile).SetAccessControl(sourceSecurity);

var paths = new TestStoragePathResolver(dataRoot);
paths.EnsureDataDirectories();
var settings = new AppSettingsService(paths);
await settings.LoadAsync();
await settings.SaveAsync(new AppSettings
{
    IncrementalSnapshotsEnabled = false,
    CompressSnapshotsEnabled = false,
    CopyAclsEnabled = true
});

var locationsIndex = new SnapshotLocationsIndexService(paths);
var snapshotService = new SnapshotService(paths, settings, new ProgramInstallService(), new ProgramProfileService(paths), locationsIndex);
var profile = new ProgramProfile
{
    Id = "acl-smoke",
    Name = "ACL Smoke",
    Paths =
    [
        new ProfilePathEntry
        {
            Label = "secured.json",
            SourcePath = sourceFile,
            RelativeTarget = "secured.json",
            IsDirectory = false
        }
    ]
};

var snapshot = await snapshotService.CreateSnapshotAsync(profile);
var snapshotDir = Path.Combine(paths.GetProgramSnapshotsDirectory(profile.Id), snapshot.Snapshot!.Id);
var sidecarPath = Path.Combine(snapshotDir, "secured.json.acl.sddl");
var sidecarExists = File.Exists(sidecarPath);

var restore = await snapshotService.RestoreSnapshotAsync(profile, snapshot.Snapshot);
var restoredExists = File.Exists(sourceFile);
var aclReadable = false;
if (restoredExists)
{
    try
    {
        _ = new FileInfo(sourceFile).GetAccessControl(AccessControlSections.Access);
        aclReadable = true;
    }
    catch (UnauthorizedAccessException)
    {
        aclReadable = false;
    }
}

Console.WriteLine($"SNAPSHOT: {snapshot.Status} | sidecar={sidecarExists} | aclWarnings={snapshot.AclWarnings.Count}");
Console.WriteLine($"RESTORE: {restore.Success} | exists={restoredExists} | aclReadable={aclReadable}");

var ok = snapshot.Status == SnapshotResultStatus.Success
    && sidecarExists
    && restore.Success
    && restoredExists;

if (ok)
{
    try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { }
}

Console.WriteLine(ok ? "ACL_SMOKE_OK" : "ACL_SMOKE_FAIL");
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
