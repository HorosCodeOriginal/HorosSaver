using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class SystemImageService : ISystemImageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStoragePathResolver _paths;
    private readonly IAppSettingsService _settings;
    private readonly IProgramProfileService _profileService;
    private readonly ISnapshotService _snapshotService;
    private readonly WbadminBackupRunner _wbadminRunner;

    public SystemImageService(
        IStoragePathResolver paths,
        IAppSettingsService settings,
        IProgramProfileService profileService,
        ISnapshotService snapshotService,
        WbadminBackupRunner wbadminRunner)
    {
        _paths = paths;
        _settings = settings;
        _profileService = profileService;
        _snapshotService = snapshotService;
        _wbadminRunner = wbadminRunner;
    }

    public async Task<SystemImageOperationResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);

        var settings = _settings.Current;
        var mode = SystemAbbildPaths.NormalizeMode(settings.SystemAbbildMode);

        string targetPath;
        try
        {
            targetPath = SystemAbbildPaths.ResolveTargetPath(_paths, settings.SystemAbbildTarget, mode);
        }
        catch (InvalidOperationException ex)
        {
            return new SystemImageOperationResult
            {
                Success = false,
                Mode = mode,
                Message = ex.Message,
                Errors = [ex.Message]
            };
        }

        return mode switch
        {
            SystemAbbildMode.AllProgramsBundle => await CreateProgramBundleAsync(targetPath, cancellationToken)
                .ConfigureAwait(false),
            SystemAbbildMode.WindowsSystemImage or SystemAbbildMode.AllVolumes =>
                await _wbadminRunner.RunAsync(mode, targetPath, cancellationToken).ConfigureAwait(false),
            _ => new SystemImageOperationResult
            {
                Success = false,
                Mode = mode,
                Message = $"Unbekannter System-Abbild-Modus: {mode}",
                Errors = [$"Unbekannter Modus: {mode}"]
            }
        };
    }

    public async Task<IReadOnlyList<string>> ListBundleIdsAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        var root = SystemAbbildPaths.GetSystemBundlesRoot(_paths);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var bundleIds = new List<string>();
        foreach (var directory in Directory.GetDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<SystemBundleManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (manifest is not null && string.Equals(manifest.Kind, SystemAbbildPaths.SystemBundleKind, StringComparison.Ordinal))
            {
                bundleIds.Add(Path.GetFileName(directory));
            }
        }

        return bundleIds.OrderByDescending(id => id, StringComparer.Ordinal).ToList();
    }

    public async Task<RestoreOperationResult> RestoreBundleAsync(
        string bundleId,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        var bundleDir = SystemAbbildPaths.GetSystemBundleDirectory(_paths, bundleId);
        var manifestPath = Path.Combine(bundleDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = $"Bundle-Manifest nicht gefunden: {manifestPath}",
                ErrorDetails = [$"Manifest fehlt: {manifestPath}"]
            };
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<SystemBundleManifest>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (manifest is null || !string.Equals(manifest.Kind, SystemAbbildPaths.SystemBundleKind, StringComparison.Ordinal))
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = "Ungültiges Bundle-Manifest (kind muss system-bundle sein).",
                ErrorDetails = ["Ungültiges Manifest"]
            };
        }

        if (manifest.Mode is SystemAbbildMode.WindowsSystemImage or SystemAbbildMode.AllVolumes)
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = SystemAbbildPaths.GetRestoreHint(manifest.Mode),
                ErrorDetails = [SystemAbbildPaths.GetRestoreHint(manifest.Mode)]
            };
        }

        var profiles = await _profileService.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        var profileMap = profiles.ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        var restoredCount = 0;
        var skippedCount = 0;
        var errors = new List<string>();
        var total = manifest.Profiles.Count(entry => entry.Success);
        var current = 0;

        foreach (var entry in manifest.Profiles.Where(entry => entry.Success))
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;
            progress?.Report(new RestoreProgressReport
            {
                Current = current,
                Total = total,
                CurrentItemLabel = entry.ProgramName
            });

            if (!profileMap.TryGetValue(entry.ProgramId, out var profile))
            {
                skippedCount++;
                errors.Add($"Profil „{entry.ProgramName}“ ({entry.ProgramId}) nicht gefunden — übersprungen.");
                continue;
            }

            var snapshots = await _snapshotService.LoadSnapshotsAsync(entry.ProgramId, cancellationToken)
                .ConfigureAwait(false);
            var snapshot = snapshots.FirstOrDefault(item => item.Id == entry.SnapshotId);
            if (snapshot is null)
            {
                skippedCount++;
                errors.Add($"Snapshot {entry.SnapshotId} für „{entry.ProgramName}“ nicht gefunden — übersprungen.");
                continue;
            }

            var result = await _snapshotService.RestoreSnapshotAsync(profile, snapshot, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (result.Success)
            {
                restoredCount += result.RestoredCount;
            }
            else
            {
                skippedCount++;
                errors.Add($"„{entry.ProgramName}“: {result.Message}");
                errors.AddRange(result.ErrorDetails);
            }
        }

        var success = restoredCount > 0 && errors.Count == 0;
        var partial = restoredCount > 0 && errors.Count > 0;
        return new RestoreOperationResult
        {
            Success = success || partial,
            Message = partial
                ? $"Bundle teilweise wiederhergestellt ({restoredCount} Dateien/Ordner, {skippedCount} übersprungen)."
                : success
                    ? $"Bundle „{bundleId}“ wiederhergestellt ({restoredCount} Dateien/Ordner)."
                    : $"Bundle-Wiederherstellung fehlgeschlagen ({skippedCount} übersprungen).",
            RestoredCount = restoredCount,
            SkippedCount = skippedCount,
            TotalCount = total,
            ErrorDetails = errors
        };
    }

    private async Task<SystemImageOperationResult> CreateProgramBundleAsync(
        string targetPath,
        CancellationToken cancellationToken)
    {
        var bundleId = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var bundleDir = SystemAbbildPaths.GetSystemBundleDirectory(_paths, bundleId);
        Directory.CreateDirectory(bundleDir);

        var manifest = new SystemBundleManifest
        {
            Kind = SystemAbbildPaths.SystemBundleKind,
            BundleId = bundleId,
            CreatedAt = DateTimeOffset.Now,
            Mode = SystemAbbildMode.AllProgramsBundle
        };

        var profiles = await _profileService.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        if (profiles.Count == 0)
        {
            manifest.Errors.Add("Keine Profile vorhanden — Bundle leer.");
            await WriteManifestAsync(bundleDir, manifest, cancellationToken).ConfigureAwait(false);
            return new SystemImageOperationResult
            {
                Success = false,
                Mode = SystemAbbildMode.AllProgramsBundle,
                BundleId = bundleId,
                Message = "Keine Programme zum Sichern gefunden.",
                Errors = manifest.Errors
            };
        }

        var successCount = 0;
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _snapshotService.CreateSnapshotAsync(
                    profile,
                    $"System-Bundle {bundleId}",
                    captureTarget: null,
                    progress: null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var entry = new SystemBundleProfileEntry
                {
                    ProgramId = profile.Id,
                    ProgramName = profile.Name,
                    SnapshotId = result.Snapshot?.Id ?? string.Empty,
                    Success = result.Status != SnapshotResultStatus.Failed && result.Snapshot is not null,
                    ErrorMessage = result.Status == SnapshotResultStatus.Failed ? result.Message : null
                };

                manifest.Profiles.Add(entry);
                if (entry.Success)
                {
                    successCount++;
                }
                else
                {
                    manifest.Errors.Add($"„{profile.Name}“: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                manifest.Profiles.Add(new SystemBundleProfileEntry
                {
                    ProgramId = profile.Id,
                    ProgramName = profile.Name,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                manifest.Errors.Add($"„{profile.Name}“: {ex.Message}");
            }
        }

        await WriteManifestAsync(bundleDir, manifest, cancellationToken).ConfigureAwait(false);

        var logFilePath = Path.Combine(_paths.LogsRoot, $"system-abbild-{bundleId}.log");
        var logLines = new List<string>
        {
            $"HorosSaver System-Abbild — Modus 2 (Alle Programme)",
            $"Bundle: {bundleDir}",
            $"Ziel-Basis: {targetPath}",
            $"Profile gesamt: {profiles.Count}",
            $"Erfolgreich: {successCount}",
            $"Fehler: {manifest.Errors.Count}"
        };
        logLines.AddRange(manifest.Errors.Select(error => $"  - {error}"));
        await File.WriteAllLinesAsync(logFilePath, logLines, cancellationToken).ConfigureAwait(false);

        var allSucceeded = manifest.Errors.Count == 0 && successCount == profiles.Count;
        var partial = successCount > 0 && manifest.Errors.Count > 0;
        return new SystemImageOperationResult
        {
            Success = allSucceeded || partial,
            Mode = SystemAbbildMode.AllProgramsBundle,
            BundleId = bundleId,
            LogFilePath = logFilePath,
            Errors = manifest.Errors,
            Message = allSucceeded
                ? $"Programm-Bundle erstellt: {bundleDir} ({successCount} Profile)."
                : partial
                    ? $"Programm-Bundle teilweise erstellt ({successCount}/{profiles.Count}). Details: {logFilePath}"
                    : $"Programm-Bundle fehlgeschlagen. Details: {logFilePath}"
        };
    }

    private static async Task WriteManifestAsync(
        string bundleDir,
        SystemBundleManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(bundleDir, "manifest.json");
        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
