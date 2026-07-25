using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class ProfileRecoveryResult
{
    public int ProfilesRecovered { get; init; }
    public bool Changed => ProfilesRecovered > 0;
}

public sealed class ProfileRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ProfileRecoveryResult> TryRecoverFromSnapshotsAsync(
        IStoragePathResolver paths,
        IProgramProfileService profileService,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDataDirectories();

        var existing = await profileService.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return new ProfileRecoveryResult();
        }

        if (!Directory.Exists(paths.SnapshotsRoot))
        {
            return new ProfileRecoveryResult();
        }

        var recoveredProfiles = new List<ProgramProfile>();
        var sortOrder = 0;

        foreach (var programDir in Directory.GetDirectories(paths.SnapshotsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var programId = Path.GetFileName(programDir);
            if (string.IsNullOrWhiteSpace(programId)
                || programId.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            var manifest = await LoadLatestManifestAsync(programDir, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                continue;
            }

            var profile = BuildProfileFromManifest(manifest, sortOrder++);
            recoveredProfiles.Add(profile);
        }

        if (recoveredProfiles.Count == 0)
        {
            return new ProfileRecoveryResult();
        }

        await profileService.SaveProfilesAsync(recoveredProfiles, cancellationToken).ConfigureAwait(false);
        AppFileLogger.Info(
            $"Profile aus Snapshots wiederhergestellt: {recoveredProfiles.Count} Programme unter {paths.DataRoot}.");

        return new ProfileRecoveryResult
        {
            ProfilesRecovered = recoveredProfiles.Count
        };
    }

    private static async Task<SnapshotManifest?> LoadLatestManifestAsync(
        string programDir,
        CancellationToken cancellationToken)
    {
        SnapshotManifest? latest = null;

        foreach (var snapshotDir in Directory.GetDirectories(programDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestPath = Path.Combine(snapshotDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<SnapshotManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null)
            {
                continue;
            }

            if (latest is null || manifest.CreatedAt > latest.CreatedAt)
            {
                latest = manifest;
            }
        }

        return latest;
    }

    private static ProgramProfile BuildProfileFromManifest(SnapshotManifest manifest, int sortOrder)
    {
        var programName = string.IsNullOrWhiteSpace(manifest.ProgramName)
            ? manifest.ProgramId
            : manifest.ProgramName.Trim();
        var install = manifest.ProgramInstall;

        var profile = new ProgramProfile
        {
            Id = manifest.ProgramId,
            Name = programName,
            Category = "Wiederhergestellt",
            Subtitle = install?.Publisher ?? "Aus Snapshot",
            IconGlyph = BuildIconGlyph(programName),
            IconBackground = "#5865F2",
            IsBound = true,
            Publisher = install?.Publisher,
            InstalledVersion = install?.DisplayVersion,
            InstallLocation = install?.InstallLocation,
            WingetId = install?.WingetId,
            SortOrder = sortOrder,
            LastSnapshotAt = manifest.CreatedAt,
            Paths = BuildPathsFromCapturedItems(manifest.CapturedItems),
            DetailLines =
            [
                "Quelle: Snapshot-Wiederherstellung",
                $"Snapshots: ab {manifest.CreatedAt:yyyy-MM-dd HH:mm}",
                $"Pfade: {manifest.CapturedItems.Count} aus Manifest"
            ]
        };

        if (CursorSnapshotPaths.IsCursorProfile(profile))
        {
            profile.CursorSnapshotLevel = CursorSnapshotLevel.Standard;
            CursorSnapshotPaths.ApplyLevelToProfile(profile, profile.CursorSnapshotLevel);
        }

        KnownWingetIds.EnrichProfile(profile);
        return profile;
    }

    private static List<ProfilePathEntry> BuildPathsFromCapturedItems(IEnumerable<CapturedItem> capturedItems)
    {
        var paths = new List<ProfilePathEntry>();

        foreach (var item in capturedItems)
        {
            if (string.IsNullOrWhiteSpace(item.SourcePath))
            {
                continue;
            }

            paths.Add(new ProfilePathEntry
            {
                Label = string.IsNullOrWhiteSpace(item.Label) ? item.SourcePath : item.Label,
                SourcePath = item.SourcePath,
                RelativeTarget = string.IsNullOrWhiteSpace(item.SnapshotRelativePath)
                    ? item.Label
                    : item.SnapshotRelativePath,
                IsDirectory = item.IsDirectory
            });
        }

        return paths;
    }

    private static string BuildIconGlyph(string programName)
    {
        var letters = new string(programName
            .Where(char.IsLetterOrDigit)
            .Take(2)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(letters) ? "PR" : letters;
    }
}
