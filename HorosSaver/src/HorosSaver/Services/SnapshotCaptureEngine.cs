using HorosSaver.Models;

namespace HorosSaver.Services;

internal sealed class SnapshotCaptureEngine
{
    private readonly IStoragePathResolver _paths;
    private readonly SnapshotResolver _resolver;

    public SnapshotCaptureEngine(IStoragePathResolver paths, SnapshotResolver resolver)
    {
        _paths = paths;
        _resolver = resolver;
    }

    public async Task<CapturedItem> CapturePathAsync(
        ProfilePathEntry pathEntry,
        string snapshotDir,
        string programId,
        string? parentSnapshotId,
        bool incrementalEnabled,
        bool compressionEnabled,
        bool copyAclsEnabled,
        ICollection<string> aclWarnings,
        ICollection<string> lockedSkippedFiles,
        SnapshotCaptureProgressTracker? progressTracker,
        SnapshotCaptureControls? captureControls,
        CancellationToken cancellationToken)
    {
        captureControls?.WaitIfAllowed(cancellationToken);

        var exists = pathEntry.IsDirectory
            ? Directory.Exists(pathEntry.SourcePath)
            : File.Exists(pathEntry.SourcePath);

        var captured = new CapturedItem
        {
            Label = pathEntry.Label,
            SourcePath = pathEntry.SourcePath,
            SnapshotRelativePath = SnapshotContentHasher.NormalizeRelativePath(pathEntry.RelativeTarget),
            IsDirectory = pathEntry.IsDirectory,
            Exists = exists
        };

        if (!exists)
        {
            return captured;
        }

        var parentIndex = parentSnapshotId is not null && incrementalEnabled
            ? _resolver.BuildEffectiveFileIndex(programId, parentSnapshotId)
            : new Dictionary<string, SnapshotFileEntry>(StringComparer.OrdinalIgnoreCase);

        if (pathEntry.IsDirectory)
        {
            await CaptureDirectoryAsync(
                captured,
                pathEntry.SourcePath,
                snapshotDir,
                parentSnapshotId,
                parentIndex,
                compressionEnabled,
                copyAclsEnabled,
                aclWarnings,
                lockedSkippedFiles,
                progressTracker,
                captureControls,
                cancellationToken).ConfigureAwait(false);
            return captured;
        }

        await CaptureSingleFileAsync(
            captured,
            pathEntry.SourcePath,
            snapshotDir,
            parentSnapshotId,
            parentIndex,
            compressionEnabled,
            copyAclsEnabled,
            aclWarnings,
            lockedSkippedFiles,
            progressTracker,
            captureControls,
            cancellationToken).ConfigureAwait(false);

        return captured;
    }

    private async Task CaptureSingleFileAsync(
        CapturedItem captured,
        string sourcePath,
        string snapshotDir,
        string? parentSnapshotId,
        IReadOnlyDictionary<string, SnapshotFileEntry> parentIndex,
        bool compressionEnabled,
        bool copyAclsEnabled,
        ICollection<string> aclWarnings,
        ICollection<string> lockedSkippedFiles,
        SnapshotCaptureProgressTracker? progressTracker,
        SnapshotCaptureControls? captureControls,
        CancellationToken cancellationToken)
    {
        captureControls?.WaitIfAllowed(cancellationToken);

        var info = new FileInfo(sourcePath);
        var hash = SnapshotContentHasher.TryComputeHash(sourcePath);
        captured.ContentHash = hash;
        captured.OriginalSizeBytes = info.Length;

        var parentRelative = captured.SnapshotRelativePath;
        if (parentSnapshotId is not null
            && hash is not null
            && parentIndex.TryGetValue(parentRelative, out var parentFile)
            && string.Equals(parentFile.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            captured.StorageKind = SnapshotStorageKind.Reference;
            captured.ReferencedSnapshotId = parentSnapshotId;
            captured.ReferencedRelativePath = parentRelative;
            captured.StoredSizeBytes = 0;

            if (copyAclsEnabled)
            {
                CaptureAclSidecar(
                    sourcePath,
                    snapshotDir,
                    captured.SnapshotRelativePath,
                    isDirectory: false,
                    storedCompressed: false,
                    aclWarnings);
            }

            progressTracker?.Advance(sourcePath);
            return;
        }

        var targetRelative = captured.SnapshotRelativePath;
        var targetPath = Path.Combine(snapshotDir, targetRelative.Replace('/', Path.DirectorySeparatorChar));

        if (SnapshotContentHasher.ShouldCompress(compressionEnabled, info.Length))
        {
            if (!await TryWriteCompressedCopyAsync(sourcePath, targetPath + ".gz", lockedSkippedFiles, cancellationToken)
                    .ConfigureAwait(false))
            {
                captured.StorageKind = SnapshotStorageKind.SkippedLocked;
                progressTracker?.Advance(sourcePath);
                return;
            }

            captured.StorageKind = SnapshotStorageKind.Compressed;
            captured.StoredSizeBytes = new FileInfo(targetPath + ".gz").Length;

            if (copyAclsEnabled)
            {
                CaptureAclSidecar(
                    sourcePath,
                    snapshotDir,
                    captured.SnapshotRelativePath,
                    isDirectory: false,
                    storedCompressed: true,
                    aclWarnings);
            }

            progressTracker?.Advance(sourcePath);
            return;
        }

        if (!await TryCopyFileAsync(sourcePath, targetPath, lockedSkippedFiles, cancellationToken).ConfigureAwait(false))
        {
            captured.StorageKind = SnapshotStorageKind.SkippedLocked;
            progressTracker?.Advance(sourcePath);
            return;
        }

        captured.StorageKind = SnapshotStorageKind.Inline;
        captured.StoredSizeBytes = info.Length;

        if (copyAclsEnabled)
        {
            CaptureAclSidecar(
                sourcePath,
                snapshotDir,
                captured.SnapshotRelativePath,
                isDirectory: false,
                storedCompressed: false,
                aclWarnings);
        }

        progressTracker?.Advance(sourcePath);
    }

    private async Task CaptureDirectoryAsync(
        CapturedItem captured,
        string sourceDirectory,
        string snapshotDir,
        string? parentSnapshotId,
        IReadOnlyDictionary<string, SnapshotFileEntry> parentIndex,
        bool compressionEnabled,
        bool copyAclsEnabled,
        ICollection<string> aclWarnings,
        ICollection<string> lockedSkippedFiles,
        SnapshotCaptureProgressTracker? progressTracker,
        SnapshotCaptureControls? captureControls,
        CancellationToken cancellationToken)
    {
        foreach (var absolutePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            captureControls?.WaitIfAllowed(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var relativeWithinDirectory = Path.GetRelativePath(sourceDirectory, absolutePath);
            var snapshotRelative = SnapshotContentHasher.NormalizeRelativePath(
                Path.Combine(captured.SnapshotRelativePath, relativeWithinDirectory));

            var info = new FileInfo(absolutePath);
            var hash = SnapshotContentHasher.TryComputeHash(absolutePath);
            var record = new CapturedFileRecord
            {
                RelativePath = SnapshotContentHasher.NormalizeRelativePath(relativeWithinDirectory),
                Exists = true,
                ContentHash = hash,
                OriginalSizeBytes = info.Length
            };

            if (parentSnapshotId is not null
                && hash is not null
                && parentIndex.TryGetValue(snapshotRelative, out var parentFile)
                && string.Equals(parentFile.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                record.StorageKind = SnapshotStorageKind.Reference;
                record.ReferencedSnapshotId = parentSnapshotId;
                record.ReferencedRelativePath = snapshotRelative;
                record.StoredSizeBytes = 0;
                captured.Files.Add(record);

                if (copyAclsEnabled)
                {
                    CaptureAclSidecar(
                        absolutePath,
                        snapshotDir,
                        snapshotRelative,
                        isDirectory: false,
                        storedCompressed: false,
                        aclWarnings);
                }

                progressTracker?.Advance(absolutePath);
                continue;
            }

            var targetPath = Path.Combine(snapshotDir, snapshotRelative.Replace('/', Path.DirectorySeparatorChar));

            if (SnapshotContentHasher.ShouldCompress(compressionEnabled, info.Length))
            {
                if (!await TryWriteCompressedCopyAsync(absolutePath, targetPath + ".gz", lockedSkippedFiles, cancellationToken)
                        .ConfigureAwait(false))
                {
                    record.StorageKind = SnapshotStorageKind.SkippedLocked;
                    record.Exists = false;
                    progressTracker?.Advance(absolutePath);
                    continue;
                }

                record.StorageKind = SnapshotStorageKind.Compressed;
                record.StoredSizeBytes = new FileInfo(targetPath + ".gz").Length;
                captured.Files.Add(record);

                if (copyAclsEnabled)
                {
                    CaptureAclSidecar(
                        absolutePath,
                        snapshotDir,
                        snapshotRelative,
                        isDirectory: false,
                        storedCompressed: true,
                        aclWarnings);
                }

                progressTracker?.Advance(absolutePath);
                continue;
            }

            if (!await TryCopyFileAsync(absolutePath, targetPath, lockedSkippedFiles, cancellationToken).ConfigureAwait(false))
            {
                record.StorageKind = SnapshotStorageKind.SkippedLocked;
                record.Exists = false;
                progressTracker?.Advance(absolutePath);
                continue;
            }

            record.StorageKind = SnapshotStorageKind.Inline;
            record.StoredSizeBytes = info.Length;
            captured.Files.Add(record);

            if (copyAclsEnabled)
            {
                CaptureAclSidecar(
                    absolutePath,
                    snapshotDir,
                    snapshotRelative,
                    isDirectory: false,
                    storedCompressed: false,
                    aclWarnings);
            }

            progressTracker?.Advance(absolutePath);
        }

        if (captured.Files.Count == 0)
        {
            var markerPath = Path.Combine(
                snapshotDir,
                captured.SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(markerPath);
        }

        if (!copyAclsEnabled)
        {
            return;
        }

        CaptureDirectoryAcls(sourceDirectory, snapshotDir, captured.SnapshotRelativePath, aclWarnings);
    }

    private static async Task<bool> TryCopyFileAsync(
        string sourcePath,
        string targetPath,
        ICollection<string> lockedSkippedFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (SnapshotFileAccessHelper.IsFileLockedException(ex))
        {
            lockedSkippedFiles.Add(sourcePath);
            return false;
        }
    }

    private static async Task<bool> TryWriteCompressedCopyAsync(
        string sourcePath,
        string destinationPath,
        ICollection<string> lockedSkippedFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            await SnapshotContentHasher.WriteCompressedCopyAsync(sourcePath, destinationPath, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (SnapshotFileAccessHelper.IsFileLockedException(ex))
        {
            lockedSkippedFiles.Add(sourcePath);
            return false;
        }
    }

    private static void CaptureDirectoryAcls(
        string sourceDirectory,
        string snapshotDir,
        string snapshotRelativeRoot,
        ICollection<string> aclWarnings)
    {
        CaptureAclSidecar(sourceDirectory, snapshotDir, snapshotRelativeRoot, isDirectory: true, storedCompressed: false, aclWarnings);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (FileAclService.IsReparsePoint(directory))
            {
                aclWarnings.Add($"Reparse-Punkt übersprungen: {directory}");
                continue;
            }

            var relative = SnapshotContentHasher.NormalizeRelativePath(
                Path.Combine(snapshotRelativeRoot, Path.GetRelativePath(sourceDirectory, directory)));
            CaptureAclSidecar(directory, snapshotDir, relative, isDirectory: true, storedCompressed: false, aclWarnings);
        }
    }

    private static void CaptureAclSidecar(
        string sourcePath,
        string snapshotDir,
        string snapshotRelative,
        bool isDirectory,
        bool storedCompressed,
        ICollection<string> aclWarnings)
        => AclSidecarStore.CaptureToSidecar(
            sourcePath,
            snapshotDir,
            snapshotRelative,
            isDirectory,
            storedCompressed,
            aclWarnings);

    public static (int Referenced, int Stored) CountStorageStats(SnapshotManifest manifest)
    {
        var referenced = 0;
        var stored = 0;

        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists))
        {
            if (item.IsDirectory)
            {
                foreach (var file in item.Files)
                {
                    if (file.StorageKind == SnapshotStorageKind.Reference)
                    {
                        referenced++;
                    }
                    else if (file.Exists && file.StorageKind != SnapshotStorageKind.SkippedLocked)
                    {
                        stored++;
                    }
                }

                continue;
            }

            if (item.StorageKind == SnapshotStorageKind.Reference)
            {
                referenced++;
            }
            else if (item.StorageKind != SnapshotStorageKind.SkippedLocked)
            {
                stored++;
            }
        }

        return (referenced, stored);
    }
}
