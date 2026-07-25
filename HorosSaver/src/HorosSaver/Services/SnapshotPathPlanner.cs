using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class SnapshotPathPlanner
{
    public static SnapshotStoragePlan ResolveCapturePlan(
        IStoragePathResolver paths,
        AppSettings settings,
        ProgramProfile profile,
        string snapshotId,
        SnapshotCaptureTargetChoice? captureTarget)
    {
        var mode = captureTarget?.Mode ?? SnapshotCaptureTargetMode.StandardInternal;
        var chosenRoot = ResolveChosenRoot(paths, settings, profile, mode, captureTarget?.CustomFolderPath);
        var isExternal = !IsInternalRoot(paths, chosenRoot, profile.Id);
        var snapshotDir = BuildSnapshotDirectory(paths, chosenRoot, profile.Id, profile.Name, snapshotId, isExternal);
        var storageRoot = isExternal ? chosenRoot : paths.DataRoot;

        return new SnapshotStoragePlan(chosenRoot, storageRoot, snapshotDir, isExternal);
    }

    public static string ResolveChosenRoot(
        IStoragePathResolver paths,
        AppSettings settings,
        ProgramProfile profile,
        SnapshotCaptureTargetMode mode,
        string? customFolderPath)
    {
        switch (mode)
        {
            case SnapshotCaptureTargetMode.CustomFolder:
                if (string.IsNullOrWhiteSpace(customFolderPath))
                {
                    throw new InvalidOperationException("Bitte einen Zielordner auswählen.");
                }

                return Path.GetFullPath(customFolderPath);

            case SnapshotCaptureTargetMode.ProfileDefault:
                if (!string.IsNullOrWhiteSpace(profile.CustomSnapshotRoot))
                {
                    return Path.GetFullPath(profile.CustomSnapshotRoot);
                }

                if (!string.IsNullOrWhiteSpace(settings.DefaultSnapshotRoot))
                {
                    return Path.GetFullPath(settings.DefaultSnapshotRoot);
                }

                return paths.DataRoot;

            default:
                return paths.DataRoot;
        }
    }

    public static string BuildSnapshotDirectory(
        IStoragePathResolver paths,
        string chosenRoot,
        string programId,
        string programName,
        string snapshotId,
        bool isExternal)
    {
        if (!isExternal)
        {
            return Path.Combine(paths.GetProgramSnapshotsDirectory(programId), snapshotId);
        }

        var sanitizedName = SnapshotDisplayName.SanitizeForFileName(programName);
        return Path.Combine(chosenRoot, "snapshot", sanitizedName, snapshotId);
    }

    public static bool IsInternalRoot(IStoragePathResolver paths, string chosenRoot, string programId)
    {
        var normalizedChosen = Path.GetFullPath(chosenRoot);
        var normalizedDataRoot = Path.GetFullPath(paths.DataRoot);
        return string.Equals(normalizedChosen, normalizedDataRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveSnapshotDirectory(
        IStoragePathResolver paths,
        ISnapshotLocationsIndexService locationsIndex,
        string programId,
        string snapshotId)
    {
        var indexed = locationsIndex.TryGetSnapshotPath(programId, snapshotId);
        if (!string.IsNullOrWhiteSpace(indexed) && Directory.Exists(indexed))
        {
            return indexed;
        }

        return Path.Combine(paths.GetProgramSnapshotsDirectory(programId), snapshotId);
    }

    public static SnapshotStoragePlan ResolveMovePlan(
        IStoragePathResolver paths,
        ProgramProfile profile,
        string snapshotId,
        string newStorageRoot,
        bool wasExternal)
    {
        var chosenRoot = Path.GetFullPath(newStorageRoot);
        var isExternal = !IsInternalRoot(paths, chosenRoot, profile.Id);
        var snapshotDir = BuildSnapshotDirectory(paths, chosenRoot, profile.Id, profile.Name, snapshotId, isExternal);
        var storageRoot = isExternal ? chosenRoot : paths.DataRoot;

        return new SnapshotStoragePlan(chosenRoot, storageRoot, snapshotDir, isExternal);
    }
}

internal sealed class SnapshotStoragePlan
{
    public SnapshotStoragePlan(string chosenRoot, string storageRoot, string snapshotDir, bool isExternal)
    {
        ChosenRoot = chosenRoot;
        StorageRoot = storageRoot;
        SnapshotDir = snapshotDir;
        IsExternal = isExternal;
    }

    public string ChosenRoot { get; }
    public string StorageRoot { get; }
    public string SnapshotDir { get; }
    public bool IsExternal { get; }
}
