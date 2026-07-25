using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class HeadlessCliRunner
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var layout = AppStoragePaths.Resolve();
        AppFileLogger.Initialize(layout.LogsRoot);

        if (CliLaunchOptions.RegenerateRestoreBats)
        {
            return await RegenerateRestoreBatsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (CliLaunchOptions.IsRestoreMode)
        {
            return await RestoreSnapshotAsync(
                CliLaunchOptions.RestoreProgramId!,
                CliLaunchOptions.RestoreSnapshotId!,
                cancellationToken).ConfigureAwait(false);
        }

        return 2;
    }

    private static async Task<int> RestoreSnapshotAsync(
        string programId,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        var pathResolver = new StoragePathResolver();
        pathResolver.EnsureDataDirectories();

        var settingsService = new AppSettingsService(pathResolver);
        var profileService = new ProgramProfileService(pathResolver);
        var locationsIndex = new SnapshotLocationsIndexService(pathResolver);
        var programInstallService = new ProgramInstallService();
        var snapshotService = new SnapshotService(
            pathResolver,
            settingsService,
            programInstallService,
            profileService,
            locationsIndex);

        var profiles = await profileService.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        var profile = profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, programId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            Console.Error.WriteLine($"Profil nicht gefunden: {programId}");
            return 3;
        }

        var snapshots = await snapshotService.LoadSnapshotsAsync(programId, cancellationToken).ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, snapshotId, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null)
        {
            Console.Error.WriteLine($"Snapshot nicht gefunden: {snapshotId}");
            return 4;
        }

        var restoreOptions = CliLaunchOptions.RestoreReinstallProgram
            ? new RestoreOptions
            {
                Mode = RestoreTargetMode.OriginalPaths,
                ReinstallProgram = true,
                OverwriteConfirmed = true
            }
            : RestoreOptions.Original;

        var result = await snapshotService.RestoreSnapshotAsync(
            profile,
            snapshot,
            selectedRelativePaths: null,
            options: restoreOptions,
            progress: new Progress<RestoreProgressReport>(report =>
            {
                if (!string.IsNullOrWhiteSpace(report.CurrentItemLabel))
                {
                    Console.WriteLine($"[{report.Current}/{report.Total}] {report.CurrentItemLabel}");
                }
            }),
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine(result.Message);
        return result.Success ? 0 : 5;
    }

    private static async Task<int> RegenerateRestoreBatsAsync(CancellationToken cancellationToken)
    {
        var pathResolver = new StoragePathResolver();
        pathResolver.EnsureDataDirectories();
        var locationsIndex = new SnapshotLocationsIndexService(pathResolver);

        var regenerated = SnapshotRestoreBatGenerator.RegenerateAllKnown(pathResolver, locationsIndex);
        Console.WriteLine($"Wiederherstellen.bat regeneriert: {regenerated}");
        return 0;
    }
}
