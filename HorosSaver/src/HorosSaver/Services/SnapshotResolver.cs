using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

internal sealed class SnapshotResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IStoragePathResolver _paths;
    private readonly ISnapshotLocationsIndexService _locationsIndex;
    private readonly Dictionary<string, SnapshotManifest> _manifestCache = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotResolver(IStoragePathResolver paths, ISnapshotLocationsIndexService locationsIndex)
    {
        _paths = paths;
        _locationsIndex = locationsIndex;
    }

    private string GetSnapshotDirectory(string programId, string snapshotId)
        => SnapshotPathPlanner.ResolveSnapshotDirectory(_paths, _locationsIndex, programId, snapshotId);

    public SnapshotManifest? LoadManifest(string programId, string snapshotId)
    {
        var cacheKey = $"{programId}|{snapshotId}";
        if (_manifestCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var manifestPath = Path.Combine(
            GetSnapshotDirectory(programId, snapshotId),
            "manifest.json");

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<SnapshotManifest>(json, JsonOptions);
            if (manifest is not null)
            {
                _manifestCache[cacheKey] = manifest;
            }

            return manifest;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Dictionary<string, SnapshotFileEntry> BuildEffectiveFileIndex(string programId, string snapshotId)
    {
        var manifest = LoadManifest(programId, snapshotId);
        if (manifest is null)
        {
            return new Dictionary<string, SnapshotFileEntry>(StringComparer.OrdinalIgnoreCase);
        }

        var files = new Dictionary<string, SnapshotFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists))
        {
            if (item.IsDirectory)
            {
                if (item.Files.Count > 0)
                {
                    foreach (var file in item.Files.Where(record => record.Exists))
                    {
                        var relativePath = SnapshotContentHasher.NormalizeRelativePath(
                            Path.Combine(item.SnapshotRelativePath, file.RelativePath));

                        AddEffectiveFile(files, programId, snapshotId, relativePath, file);
                    }
                }
                else
                {
                    AddLegacyDirectoryFiles(
                        files,
                        GetSnapshotDirectory(programId, snapshotId),
                        item.SnapshotRelativePath);
                }

                continue;
            }

            AddEffectiveFile(files, programId, snapshotId, item.SnapshotRelativePath, item);
        }

        return files;
    }

    public string? ResolveStoredAbsolutePath(string programId, string snapshotId, string relativePathFromSnapshotRoot)
    {
        var manifest = LoadManifest(programId, snapshotId);
        if (manifest is null)
        {
            return null;
        }

        var normalized = SnapshotContentHasher.NormalizeRelativePath(relativePathFromSnapshotRoot);
        var snapshotDir = GetSnapshotDirectory(programId, snapshotId);

        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists))
        {
            if (item.IsDirectory)
            {
                if (item.Files.Count == 0)
                {
                    continue;
                }

                var itemPrefix = SnapshotContentHasher.NormalizeRelativePath(item.SnapshotRelativePath);
                if (!normalized.StartsWith(itemPrefix + "/", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(normalized, itemPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileRelative = normalized.Length > itemPrefix.Length
                    ? normalized[(itemPrefix.Length + 1)..]
                    : string.Empty;

                var fileRecord = item.Files.FirstOrDefault(file =>
                    string.Equals(
                        SnapshotContentHasher.NormalizeRelativePath(file.RelativePath),
                        fileRelative,
                        StringComparison.OrdinalIgnoreCase));

                if (fileRecord is null)
                {
                    continue;
                }

                return ResolveRecordPath(programId, snapshotId, snapshotDir, item.SnapshotRelativePath, fileRecord);
            }

            if (!string.Equals(
                    SnapshotContentHasher.NormalizeRelativePath(item.SnapshotRelativePath),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return ResolveRecordPath(programId, snapshotId, snapshotDir, item.SnapshotRelativePath, item);
        }

        return null;
    }

    public bool TryResolveSourceForRestore(
        string programId,
        string snapshotId,
        CapturedItem item,
        out string? resolvedSourcePath,
        out bool isCompressed)
    {
        resolvedSourcePath = null;
        isCompressed = false;

        if (!item.Exists)
        {
            return false;
        }

        var snapshotDir = GetSnapshotDirectory(programId, snapshotId);

        if (!item.IsDirectory)
        {
            resolvedSourcePath = ResolveRecordPath(programId, snapshotId, snapshotDir, item.SnapshotRelativePath, item);
            isCompressed = item.StorageKind == SnapshotStorageKind.Compressed;
            return resolvedSourcePath is not null;
        }

        return false;
    }

    public string? ResolveRecordPath(
        string programId,
        string snapshotId,
        string snapshotDir,
        string itemRelativePath,
        CapturedFileRecord record)
        => ResolveStoredFile(
            programId,
            snapshotId,
            snapshotDir,
            record.StorageKind,
            record.ReferencedSnapshotId,
            record.ReferencedRelativePath,
            SnapshotContentHasher.NormalizeRelativePath(
                Path.Combine(itemRelativePath, record.RelativePath)));

    private string? ResolveRecordPath(
        string programId,
        string snapshotId,
        string snapshotDir,
        string itemRelativePath,
        CapturedItem item)
        => ResolveStoredFile(
            programId,
            snapshotId,
            snapshotDir,
            item.StorageKind,
            item.ReferencedSnapshotId,
            item.ReferencedRelativePath,
            SnapshotContentHasher.NormalizeRelativePath(item.SnapshotRelativePath));

    private string? ResolveStoredFile(
        string programId,
        string snapshotId,
        string snapshotDir,
        SnapshotStorageKind storageKind,
        string? referencedSnapshotId,
        string? referencedRelativePath,
        string snapshotRelativePath)
    {
        switch (storageKind)
        {
            case SnapshotStorageKind.Reference:
            {
                if (string.IsNullOrWhiteSpace(referencedSnapshotId)
                    || string.IsNullOrWhiteSpace(referencedRelativePath))
                {
                    return null;
                }

                return ResolveStoredAbsolutePath(programId, referencedSnapshotId, referencedRelativePath);
            }

            case SnapshotStorageKind.Compressed:
            {
                var compressedPath = Path.Combine(
                    snapshotDir,
                    (snapshotRelativePath + ".gz").Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(compressedPath) ? compressedPath : null;
            }

            default:
            {
                var inlinePath = Path.Combine(
                    snapshotDir,
                    snapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(inlinePath) ? inlinePath : null;
            }
        }
    }

    private void AddEffectiveFile(
        Dictionary<string, SnapshotFileEntry> files,
        string programId,
        string snapshotId,
        string relativePath,
        CapturedFileRecord record)
    {
        var absolutePath = ResolveStoredFile(
            programId,
            snapshotId,
            GetSnapshotDirectory(programId, snapshotId),
            record.StorageKind,
            record.ReferencedSnapshotId,
            record.ReferencedRelativePath,
            relativePath);

        if (absolutePath is null || !File.Exists(absolutePath))
        {
            return;
        }

        var info = new FileInfo(absolutePath);
        files[relativePath] = new SnapshotFileEntry
        {
            RelativePath = relativePath,
            SizeBytes = record.OriginalSizeBytes > 0 ? record.OriginalSizeBytes : info.Length,
            ModifiedAt = info.LastWriteTimeUtc,
            ContentHash = record.ContentHash ?? SnapshotContentHasher.TryComputeHash(absolutePath)
        };
    }

    private void AddEffectiveFile(
        Dictionary<string, SnapshotFileEntry> files,
        string programId,
        string snapshotId,
        string relativePath,
        CapturedItem item)
    {
        var absolutePath = ResolveRecordPath(
            programId,
            snapshotId,
            GetSnapshotDirectory(programId, snapshotId),
            item.SnapshotRelativePath,
            item);

        if (absolutePath is null || !File.Exists(absolutePath))
        {
            return;
        }

        var info = new FileInfo(absolutePath);
        files[relativePath] = new SnapshotFileEntry
        {
            RelativePath = relativePath,
            SizeBytes = item.OriginalSizeBytes > 0 ? item.OriginalSizeBytes : info.Length,
            ModifiedAt = info.LastWriteTimeUtc,
            ContentHash = item.ContentHash ?? SnapshotContentHasher.TryComputeHash(absolutePath)
        };
    }

    private static void AddLegacyDirectoryFiles(
        Dictionary<string, SnapshotFileEntry> files,
        string snapshotDir,
        string directoryRelativePath)
    {
        var directoryPath = Path.Combine(
            snapshotDir,
            directoryRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var absolutePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var relative = SnapshotContentHasher.NormalizeRelativePath(
                Path.Combine(directoryRelativePath, Path.GetRelativePath(directoryPath, absolutePath)));

            var info = new FileInfo(absolutePath);
            files[relative] = new SnapshotFileEntry
            {
                RelativePath = relative,
                SizeBytes = info.Length,
                ModifiedAt = info.LastWriteTimeUtc,
                ContentHash = SnapshotContentHasher.TryComputeHash(absolutePath)
            };
        }
    }
}
