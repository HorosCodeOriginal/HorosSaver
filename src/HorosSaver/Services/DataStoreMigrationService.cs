using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class DataStoreMigrationResult
{
    public bool Changed { get; init; }
    public string? SourceDataRoot { get; init; }
    public int ProfilesMerged { get; init; }
    public int SnapshotFoldersCopied { get; init; }
}

public sealed class DataStoreMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<DataStoreMigrationResult> TryMigrateIfNeededAsync(
        IStoragePathResolver paths,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDataDirectories();

        var targetProfileCount = CountProfiles(paths.ProfilesFilePath);
        var targetSnapshotCount = CountSnapshotFolders(paths.SnapshotsRoot);

        var candidates = DiscoverCandidateSources(paths)
            .Select(source => new
            {
                Source = source,
                ProfileCount = CountProfiles(Path.Combine(source, "profiles.json")),
                SnapshotCount = CountSnapshotFolders(Path.Combine(source, "snapshots"))
            })
            .Where(candidate => candidate.ProfileCount > 0 || candidate.SnapshotCount > 0)
            .OrderByDescending(candidate => candidate.ProfileCount)
            .ThenByDescending(candidate => candidate.SnapshotCount)
            .ToList();

        var best = candidates.FirstOrDefault();
        if (best is null)
        {
            return new DataStoreMigrationResult();
        }

        var shouldMigrateProfiles = targetProfileCount == 0 && best.ProfileCount > 0;
        var shouldMigrateSnapshots = best.SnapshotCount > targetSnapshotCount;
        if (!shouldMigrateProfiles && !shouldMigrateSnapshots)
        {
            return new DataStoreMigrationResult();
        }

        var profilesMerged = 0;
        if (shouldMigrateProfiles)
        {
            profilesMerged = await MergeProfilesAsync(
                Path.Combine(best.Source, "profiles.json"),
                paths.ProfilesFilePath,
                cancellationToken).ConfigureAwait(false);
        }

        var snapshotFoldersCopied = 0;
        if (shouldMigrateSnapshots)
        {
            snapshotFoldersCopied = MergeSnapshotsDirectory(
                Path.Combine(best.Source, "snapshots"),
                paths.SnapshotsRoot);
        }

        if (shouldMigrateProfiles)
        {
            MergeFileIfMissing(
                Path.Combine(best.Source, "settings.json"),
                paths.SettingsFilePath);

            MergeFileIfMissing(
                Path.Combine(best.Source, "snapshot-locations.json"),
                paths.SnapshotLocationsIndexPath);
        }

        if (profilesMerged == 0 && snapshotFoldersCopied == 0)
        {
            return new DataStoreMigrationResult();
        }

        AppFileLogger.Info(
            $"Daten-Migration: {profilesMerged} Profile, {snapshotFoldersCopied} Snapshot-Ordner von {best.Source} nach {paths.DataRoot}.");

        return new DataStoreMigrationResult
        {
            Changed = true,
            SourceDataRoot = best.Source,
            ProfilesMerged = profilesMerged,
            SnapshotFoldersCopied = snapshotFoldersCopied
        };
    }

    private static IEnumerable<string> DiscoverCandidateSources(IStoragePathResolver paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var target = Path.GetFullPath(paths.DataRoot);

        void AddCandidate(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (seen.Contains(fullPath)
                || string.Equals(fullPath, target, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(fullPath))
            {
                seen.Add(fullPath);
            }
        }

        AddCandidate(Path.Combine(paths.AppDirectory, "data"));

        var directory = paths.AppDirectory;
        for (var depth = 0; depth < 8; depth++)
        {
            AddCandidate(Path.Combine(directory, "artifacts", "portable", "HorosSaver", "data"));

            var parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
        }

        var legacyRoot = AppStoragePaths.GetLegacyLocalAppDataRoot();
        if (paths.IsPortable)
        {
            AddCandidate(legacyRoot);
        }
        else if (!string.Equals(Path.GetFullPath(legacyRoot), target, StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(legacyRoot);
        }

        var explicitRoot = Environment.GetEnvironmentVariable(AppStoragePaths.DataRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            AddCandidate(explicitRoot.Trim());
        }

        return seen;
    }

    private static int CountProfiles(string profilesFilePath)
    {
        if (!File.Exists(profilesFilePath))
        {
            return 0;
        }

        try
        {
            var json = File.ReadAllText(profilesFilePath);
            var document = JsonSerializer.Deserialize<ProfileStoreDocument>(json, JsonOptions);
            return document?.Profiles?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountSnapshotFolders(string snapshotsRoot)
    {
        if (!Directory.Exists(snapshotsRoot))
        {
            return 0;
        }

        var count = 0;
        foreach (var programDir in Directory.GetDirectories(snapshotsRoot))
        {
            count += Directory.GetDirectories(programDir).Length;
        }

        return count;
    }

    private static async Task<int> MergeProfilesAsync(
        string sourceProfilesPath,
        string targetProfilesPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceProfilesPath))
        {
            return 0;
        }

        await using var sourceStream = File.OpenRead(sourceProfilesPath);
        var sourceDocument = await JsonSerializer.DeserializeAsync<ProfileStoreDocument>(
                sourceStream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        if (sourceDocument?.Profiles is null || sourceDocument.Profiles.Count == 0)
        {
            return 0;
        }

        ProfileStoreDocument targetDocument;
        if (File.Exists(targetProfilesPath))
        {
            await using var targetStream = File.OpenRead(targetProfilesPath);
            targetDocument = await JsonSerializer.DeserializeAsync<ProfileStoreDocument>(
                    targetStream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false) ?? new ProfileStoreDocument();
        }
        else
        {
            targetDocument = new ProfileStoreDocument();
        }

        var profilesById = targetDocument.Profiles.ToDictionary(
            profile => profile.Id,
            StringComparer.OrdinalIgnoreCase);

        var merged = 0;
        foreach (var profile in sourceDocument.Profiles)
        {
            if (profilesById.ContainsKey(profile.Id))
            {
                continue;
            }

            profilesById[profile.Id] = profile;
            merged++;
        }

        if (merged == 0 && targetDocument.Profiles.Count > 0)
        {
            return 0;
        }

        targetDocument.SchemaVersion = Math.Max(targetDocument.SchemaVersion, sourceDocument.SchemaVersion);
        targetDocument.Profiles = profilesById.Values.OrderBy(profile => profile.SortOrder).ToList();

        if (targetDocument.Groups.Count == 0 && sourceDocument.Groups.Count > 0)
        {
            targetDocument.Groups = sourceDocument.Groups;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetProfilesPath)!);
        await using var output = File.Create(targetProfilesPath);
        await JsonSerializer.SerializeAsync(output, targetDocument, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return merged > 0 ? merged : targetDocument.Profiles.Count;
    }

    private static int MergeSnapshotsDirectory(string sourceSnapshotsRoot, string targetSnapshotsRoot)
    {
        if (!Directory.Exists(sourceSnapshotsRoot))
        {
            return 0;
        }

        Directory.CreateDirectory(targetSnapshotsRoot);
        var copied = 0;

        foreach (var sourceProgramDir in Directory.GetDirectories(sourceSnapshotsRoot))
        {
            var programId = Path.GetFileName(sourceProgramDir);
            var targetProgramDir = Path.Combine(targetSnapshotsRoot, programId);

            foreach (var sourceSnapshotDir in Directory.GetDirectories(sourceProgramDir))
            {
                var snapshotId = Path.GetFileName(sourceSnapshotDir);
                var targetSnapshotDir = Path.Combine(targetProgramDir, snapshotId);
                if (Directory.Exists(targetSnapshotDir))
                {
                    continue;
                }

                Directory.CreateDirectory(targetProgramDir);
                CopyDirectoryRecursive(sourceSnapshotDir, targetSnapshotDir);
                copied++;
            }
        }

        return copied;
    }

    private static void MergeFileIfMissing(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(targetPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: false);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            if (!File.Exists(targetFile))
            {
                File.Copy(file, targetFile);
            }
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
    }
}
