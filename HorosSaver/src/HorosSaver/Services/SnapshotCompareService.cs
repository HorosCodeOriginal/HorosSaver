using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class SnapshotCompareService : ISnapshotCompareService
{
    private const string ManifestFileName = "manifest.json";

    private readonly IStoragePathResolver _paths;
    private readonly ISnapshotLocationsIndexService _locationsIndex;
    private readonly SnapshotResolver _resolver;

    public SnapshotCompareService(IStoragePathResolver paths, ISnapshotLocationsIndexService locationsIndex)
    {
        _paths = paths;
        _locationsIndex = locationsIndex;
        _resolver = new SnapshotResolver(paths, locationsIndex);
    }

    public async Task<SnapshotCompareResult> CompareAsync(
        string programId,
        string olderSnapshotId,
        string newerSnapshotId,
        string programName,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(olderSnapshotId, newerSnapshotId, StringComparison.Ordinal))
        {
            return Failure(programId, programName, "Beide Snapshots sind identisch — bitte zwei verschiedene Zeitzustände wählen.");
        }

        var olderDir = SnapshotPathPlanner.ResolveSnapshotDirectory(_paths, _locationsIndex, programId, olderSnapshotId);
        var newerDir = SnapshotPathPlanner.ResolveSnapshotDirectory(_paths, _locationsIndex, programId, newerSnapshotId);

        if (!Directory.Exists(olderDir) || !Directory.Exists(newerDir))
        {
            return Failure(programId, programName, "Mindestens ein Snapshot-Ordner wurde nicht gefunden.");
        }

        var olderFiles = await Task.Run(
            () => ResolveEffectiveFiles(programId, olderSnapshotId, olderDir),
            cancellationToken).ConfigureAwait(false);
        var newerFiles = await Task.Run(
            () => ResolveEffectiveFiles(programId, newerSnapshotId, newerDir),
            cancellationToken).ConfigureAwait(false);

        var differences = BuildDifferences(olderFiles, newerFiles);
        var olderLabel = BuildSnapshotLabel(programId, olderSnapshotId, olderDir);
        var newerLabel = BuildSnapshotLabel(programId, newerSnapshotId, newerDir);

        return new SnapshotCompareResult
        {
            Success = true,
            Message = $"{differences.Count} Unterschied(e) gefunden.",
            ProgramId = programId,
            ProgramName = programName,
            OlderSnapshotId = olderSnapshotId,
            NewerSnapshotId = newerSnapshotId,
            OlderSnapshotLabel = olderLabel,
            NewerSnapshotLabel = newerLabel,
            OlderCreatedAt = ReadCreatedAt(programId, olderSnapshotId, olderDir),
            NewerCreatedAt = ReadCreatedAt(programId, newerSnapshotId, newerDir),
            Differences = differences,
            AddedCount = differences.Count(diff => diff.Kind == SnapshotDiffKind.Added),
            RemovedCount = differences.Count(diff => diff.Kind == SnapshotDiffKind.Removed),
            ChangedCount = differences.Count(diff => diff.Kind == SnapshotDiffKind.Changed)
        };
    }

    private Dictionary<string, SnapshotFileEntry> ResolveEffectiveFiles(
        string programId,
        string snapshotId,
        string snapshotDirectory)
    {
        var manifest = _resolver.LoadManifest(programId, snapshotId);
        if (manifest is not null && manifest.SchemaVersion >= 2)
        {
            return _resolver.BuildEffectiveFileIndex(programId, snapshotId);
        }

        return EnumerateLegacySnapshotFiles(snapshotDirectory);
    }

    private static SnapshotCompareResult Failure(string programId, string programName, string message)
    {
        return new SnapshotCompareResult
        {
            Success = false,
            Message = message,
            ProgramId = programId,
            ProgramName = programName
        };
    }

    private static List<SnapshotFileDiff> BuildDifferences(
        Dictionary<string, SnapshotFileEntry> olderFiles,
        Dictionary<string, SnapshotFileEntry> newerFiles)
    {
        var allPaths = olderFiles.Keys.Union(newerFiles.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var differences = new List<SnapshotFileDiff>();

        foreach (var relativePath in allPaths)
        {
            var hasOlder = olderFiles.TryGetValue(relativePath, out var older);
            var hasNewer = newerFiles.TryGetValue(relativePath, out var newer);

            if (!hasOlder && hasNewer)
            {
                differences.Add(new SnapshotFileDiff
                {
                    Kind = SnapshotDiffKind.Added,
                    RelativePath = relativePath,
                    NewerSizeBytes = newer!.SizeBytes,
                    NewerModifiedAt = newer.ModifiedAt,
                    NewerHash = newer.ContentHash,
                    Detail = FormatAddedDetail(newer)
                });
                continue;
            }

            if (hasOlder && !hasNewer)
            {
                differences.Add(new SnapshotFileDiff
                {
                    Kind = SnapshotDiffKind.Removed,
                    RelativePath = relativePath,
                    OlderSizeBytes = older!.SizeBytes,
                    OlderModifiedAt = older.ModifiedAt,
                    OlderHash = older.ContentHash,
                    Detail = FormatRemovedDetail(older)
                });
                continue;
            }

            if (hasOlder && hasNewer && !AreEquivalent(older!, newer!))
            {
                differences.Add(new SnapshotFileDiff
                {
                    Kind = SnapshotDiffKind.Changed,
                    RelativePath = relativePath,
                    OlderSizeBytes = older!.SizeBytes,
                    NewerSizeBytes = newer!.SizeBytes,
                    OlderModifiedAt = older.ModifiedAt,
                    NewerModifiedAt = newer.ModifiedAt,
                    OlderHash = older.ContentHash,
                    NewerHash = newer.ContentHash,
                    Detail = FormatChangedDetail(older, newer)
                });
            }
        }

        return differences;
    }

    private static bool AreEquivalent(SnapshotFileEntry older, SnapshotFileEntry newer)
    {
        if (!string.IsNullOrEmpty(older.ContentHash) && !string.IsNullOrEmpty(newer.ContentHash))
        {
            return string.Equals(older.ContentHash, newer.ContentHash, StringComparison.OrdinalIgnoreCase);
        }

        return older.SizeBytes == newer.SizeBytes
            && older.ModifiedAt == newer.ModifiedAt;
    }

    private static Dictionary<string, SnapshotFileEntry> EnumerateLegacySnapshotFiles(string snapshotDirectory)
    {
        var files = new Dictionary<string, SnapshotFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var absolutePath in Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(snapshotDirectory, absolutePath)
                .Replace('\\', '/');

            if (string.Equals(relativePath, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new FileInfo(absolutePath);
            files[relativePath] = new SnapshotFileEntry
            {
                RelativePath = relativePath,
                SizeBytes = info.Length,
                ModifiedAt = info.LastWriteTimeUtc,
                ContentHash = SnapshotContentHasher.TryComputeHash(absolutePath)
            };
        }

        return files;
    }

    private string BuildSnapshotLabel(string programId, string snapshotId, string snapshotDirectory)
    {
        var manifest = _resolver.LoadManifest(programId, snapshotId);
        if (manifest is not null)
        {
            var kind = manifest.Kind == SnapshotKind.Incremental ? "Inkrementell" : "Vollständig";
            var displayName = SnapshotDisplayName.ResolveProgramDisplayName(manifest);
            return $"{kind} · {SnapshotDisplayName.Build(displayName, manifest.CreatedAt)}";
        }

        var createdAt = ReadCreatedAt(programId, snapshotId, snapshotDirectory);
        return SnapshotDisplayName.Build(programId, createdAt);
    }

    private DateTimeOffset ReadCreatedAt(string programId, string snapshotId, string snapshotDirectory)
    {
        var manifest = _resolver.LoadManifest(programId, snapshotId);
        if (manifest is not null)
        {
            return manifest.CreatedAt;
        }

        try
        {
            return new DateTimeOffset(Directory.GetCreationTimeUtc(snapshotDirectory), TimeSpan.Zero);
        }
        catch (IOException)
        {
            return DateTimeOffset.MinValue;
        }
    }

    private static string FormatAddedDetail(SnapshotFileEntry newer)
        => $"Neu · {FormatSize(newer.SizeBytes)} · {FormatTimestamp(newer.ModifiedAt)}"
           + (newer.ContentHash is null ? " · Hash n/a" : $" · #{newer.ContentHash}");

    private static string FormatRemovedDetail(SnapshotFileEntry older)
        => $"Entfernt · {FormatSize(older.SizeBytes)} · {FormatTimestamp(older.ModifiedAt)}"
           + (older.ContentHash is null ? " · Hash n/a" : $" · #{older.ContentHash}");

    private static string FormatChangedDetail(SnapshotFileEntry older, SnapshotFileEntry newer)
    {
        var sizePart = $"{FormatSize(older.SizeBytes)} → {FormatSize(newer.SizeBytes)}";
        var timePart = $"{FormatTimestamp(older.ModifiedAt)} → {FormatTimestamp(newer.ModifiedAt)}";

        if (!string.IsNullOrEmpty(older.ContentHash) && !string.IsNullOrEmpty(newer.ContentHash)
            && !string.Equals(older.ContentHash, newer.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return $"Geändert · {sizePart} · Hash #{older.ContentHash} → #{newer.ContentHash}";
        }

        return $"Geändert · {sizePart} · {timePart}";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var value = bytes / 1024d;
        if (value < 1024)
        {
            return $"{value:0.#} KB";
        }

        value /= 1024d;
        if (value < 1024)
        {
            return $"{value:0.#} MB";
        }

        value /= 1024d;
        return $"{value:0.#} GB";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
        => timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
