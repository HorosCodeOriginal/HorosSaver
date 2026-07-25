using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class SnapshotService : ISnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStoragePathResolver _paths;
    private readonly IAppSettingsService _settings;
    private readonly ISnapshotLocationsIndexService _locationsIndex;
    private readonly SnapshotResolver _resolver;
    private readonly SnapshotCaptureEngine _captureEngine;
    private readonly IProgramInstallService _programInstallService;
    private readonly IProgramProfileService _profileService;

    public SnapshotService(
        IStoragePathResolver paths,
        IAppSettingsService settings,
        IProgramInstallService programInstallService,
        IProgramProfileService profileService,
        ISnapshotLocationsIndexService locationsIndex)
    {
        _paths = paths;
        _settings = settings;
        _programInstallService = programInstallService;
        _profileService = profileService;
        _locationsIndex = locationsIndex;
        _resolver = new SnapshotResolver(paths, locationsIndex);
        _captureEngine = new SnapshotCaptureEngine(paths, _resolver);
    }

    public async Task<IReadOnlyList<SnapshotInfo>> LoadSnapshotsAsync(
        string programId,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        var programDir = _paths.GetProgramSnapshotsDirectory(programId);
        var snapshotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(programDir))
        {
            foreach (var snapshotDir in Directory.GetDirectories(programDir))
            {
                snapshotIds.Add(Path.GetFileName(snapshotDir));
            }
        }

        var indexedEntries = await _locationsIndex.GetProgramEntriesAsync(programId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in indexedEntries)
        {
            snapshotIds.Add(entry.SnapshotId);
        }

        var snapshots = new List<SnapshotInfo>();
        var profiles = await _profileService.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        var profileNamesById = profiles.ToDictionary(
            profile => profile.Id,
            profile => profile.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (var snapshotId in snapshotIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshotDir = SnapshotPathPlanner.ResolveSnapshotDirectory(
                _paths,
                _locationsIndex,
                programId,
                snapshotId);
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

            var (referenced, stored) = SnapshotCaptureEngine.CountStorageStats(manifest);

            var programDisplayName = SnapshotDisplayName.ResolveProgramDisplayName(manifest, profileNamesById);
            var displayName = string.IsNullOrWhiteSpace(manifest.DisplayName)
                ? SnapshotDisplayName.Build(programDisplayName, manifest.CreatedAt)
                : manifest.DisplayName.Trim();

            snapshots.Add(new SnapshotInfo
            {
                Id = snapshotId,
                ProgramId = manifest.ProgramId,
                Name = displayName,
                Description = BuildDescription(manifest, referenced, stored),
                CreatedAt = manifest.CreatedAt,
                SizeBytes = GetDirectorySize(snapshotDir),
                Kind = manifest.Kind,
                ParentSnapshotId = manifest.ParentSnapshotId,
                CompressionEnabled = manifest.CompressionEnabled,
                ReferencedFileCount = referenced,
                StoredFileCount = stored,
                StoragePath = snapshotDir,
                IsExternal = manifest.IsExternal
            });
        }

        var ordered = snapshots.OrderByDescending(snapshot => snapshot.CreatedAt).ToList();
        if (ordered.Count > 0)
        {
            ordered[0].IsCurrent = true;
        }

        return ordered;
    }

    public async Task<SnapshotOperationResult> CreateSnapshotAsync(
        ProgramProfile profile,
        string? description = null,
        SnapshotCaptureTargetChoice? captureTarget = null,
        IProgress<SnapshotProgressReport>? progress = null,
        SnapshotCaptureControls? captureControls = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);

        var effectiveToken = captureControls?.CancellationToken ?? cancellationToken;
        string? snapshotDir = null;
        string? snapshotId = null;
        var manifestWritten = false;

        try
        {
        var totalWorkItems = CountCaptureWorkItems(profile);
        var progressTracker = new SnapshotCaptureProgressTracker(totalWorkItems, progress);
        progressTracker.ReportPhase("Snapshot wird vorbereitet…");

        var existingSnapshots = await LoadSnapshotsAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        var parentSnapshot = _settings.Current.IncrementalSnapshotsEnabled
            ? existingSnapshots.FirstOrDefault()
            : null;

        var timestamp = DateTimeOffset.Now;
        snapshotId = timestamp.ToString("yyyyMMdd_HHmmss_fff");

        var storagePlan = SnapshotPathPlanner.ResolveCapturePlan(
            _paths,
            _settings.Current,
            profile,
            snapshotId,
            captureTarget);

        if (parentSnapshot is not null
            && storagePlan.IsExternal
            && parentSnapshot.IsExternal
            && !string.Equals(
                Path.GetDirectoryName(parentSnapshot.StoragePath ?? string.Empty),
                Path.GetDirectoryName(storagePlan.SnapshotDir),
                StringComparison.OrdinalIgnoreCase))
        {
            parentSnapshot = existingSnapshots.FirstOrDefault(snapshot => snapshot.Kind == SnapshotKind.Full);
        }

        if (storagePlan.IsExternal)
        {
            parentSnapshot = null;
        }

        snapshotDir = storagePlan.SnapshotDir;
        Directory.CreateDirectory(snapshotDir);

        var isIncremental = parentSnapshot is not null;
        var compressionEnabled = storagePlan.IsExternal
            ? false
            : _settings.Current.CompressSnapshotsEnabled;
        var copyAclsEnabled = _settings.Current.CopyAclsEnabled;
        var aclWarnings = new List<string>();
        var lockedSkippedFiles = new List<string>();
        var displayName = SnapshotDisplayName.Build(profile.Name, timestamp);
        var manifest = new SnapshotManifest
        {
            SchemaVersion = 2,
            SnapshotId = snapshotId,
            ProgramId = profile.Id,
            ProgramName = profile.Name,
            CreatedAt = timestamp,
            Kind = isIncremental ? SnapshotKind.Incremental : SnapshotKind.Full,
            ParentSnapshotId = parentSnapshot?.Id,
            CompressionEnabled = compressionEnabled,
            AclCopyEnabled = copyAclsEnabled,
            DisplayName = displayName,
            StorageRoot = storagePlan.StorageRoot,
            IsExternal = storagePlan.IsExternal,
            BatRestoreOptimized = storagePlan.IsExternal,
            ProgramInstall = BuildProgramInstallMetadata(profile)
        };

        foreach (var pathEntry in profile.Paths)
        {
            captureControls?.WaitIfAllowed(effectiveToken);
            effectiveToken.ThrowIfCancellationRequested();

            var captured = await _captureEngine.CapturePathAsync(
                pathEntry,
                snapshotDir,
                profile.Id,
                parentSnapshot?.Id,
                isIncremental && _settings.Current.IncrementalSnapshotsEnabled,
                manifest.CompressionEnabled,
                copyAclsEnabled,
                aclWarnings,
                lockedSkippedFiles,
                progressTracker,
                captureControls,
                effectiveToken).ConfigureAwait(false);

            manifest.CapturedItems.Add(captured);

            if (!captured.Exists)
            {
                manifest.SkippedItems.Add($"{pathEntry.Label}: {pathEntry.SourcePath}");
            }
        }

        manifest.AclWarnings.AddRange(aclWarnings);

        foreach (var lockedPath in lockedSkippedFiles)
        {
            manifest.SkippedItems.Add(SnapshotFileAccessHelper.FormatLockedSkippedItem(lockedPath));
            AppFileLogger.Warning($"Snapshot: gesperrte Datei übersprungen: {lockedPath}");
        }

        progressTracker.ReportPhase("Manifest wird geschrieben…");
        captureControls?.WaitIfAllowed(effectiveToken);
        effectiveToken.ThrowIfCancellationRequested();

        var manifestPath = Path.Combine(snapshotDir, "manifest.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, effectiveToken).ConfigureAwait(false);
        }

        manifestWritten = true;

        await _locationsIndex.SetSnapshotPathAsync(profile.Id, snapshotId, snapshotDir, effectiveToken)
            .ConfigureAwait(false);
        SnapshotRestoreBatGenerator.WriteIfApplicable(snapshotDir, manifest);

        profile.LastSnapshotAt = timestamp;

        var capturedCount = manifest.CapturedItems.Count(item => item.Exists);
        var (referenced, stored) = SnapshotCaptureEngine.CountStorageStats(manifest);
        var hasCapturedContent = stored > 0 || referenced > 0;
        var status = capturedCount switch
        {
            0 => SnapshotResultStatus.Failed,
            _ when !hasCapturedContent => SnapshotResultStatus.Partial,
            var count when count < profile.Paths.Count => SnapshotResultStatus.Partial,
            _ => SnapshotResultStatus.Success
        };

        var snapshot = new SnapshotInfo
        {
            Id = snapshotId,
            ProgramId = profile.Id,
            Name = displayName,
            Description = description ?? BuildDescription(manifest, referenced, stored),
            CreatedAt = timestamp,
            SizeBytes = GetDirectorySize(snapshotDir),
            IsCurrent = true,
            Kind = manifest.Kind,
            ParentSnapshotId = manifest.ParentSnapshotId,
            CompressionEnabled = manifest.CompressionEnabled,
            ReferencedFileCount = referenced,
            StoredFileCount = stored,
            StoragePath = snapshotDir,
            IsExternal = manifest.IsExternal
        };

        var kindLabel = manifest.Kind == SnapshotKind.Incremental ? "Inkrementell" : "Vollständig";
        var locationLabel = manifest.IsExternal ? "extern" : "Standard";
        var message = status switch
        {
            SnapshotResultStatus.Success => $"{kindLabel} gespeichert ({locationLabel}, {stored} neu, {referenced} referenziert).",
            SnapshotResultStatus.Partial => $"{kindLabel} teilweise gespeichert ({locationLabel}, {capturedCount}/{profile.Paths.Count} Pfade vorhanden).",
            _ => "Keine konfigurierten Pfade gefunden — Manifest angelegt."
        };

        if (lockedSkippedFiles.Count > 0)
        {
            message += $" {lockedSkippedFiles.Count} Datei(en) übersprungen (gesperrt).";
        }

        if (aclWarnings.Count > 0)
        {
            message += $" ACL: {aclWarnings.Count} Hinweis(e).";
        }

        return new SnapshotOperationResult
        {
            Status = status,
            Message = message,
            Snapshot = snapshot,
            AclWarnings = aclWarnings,
            SkippedLockedCount = lockedSkippedFiles.Count,
            SkippedLockedPaths = lockedSkippedFiles.ToArray()
        };
        }
        catch (OperationCanceledException)
        {
            await CleanupCancelledSnapshotAsync(profile.Id, snapshotId, snapshotDir, manifestWritten, CancellationToken.None)
                .ConfigureAwait(false);

            return new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Cancelled,
                Message = "Snapshot abgebrochen."
            };
        }
    }

    private async Task CleanupCancelledSnapshotAsync(
        string programId,
        string? snapshotId,
        string? snapshotDir,
        bool manifestWritten,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(snapshotDir) && Directory.Exists(snapshotDir))
        {
            try
            {
                Directory.Delete(snapshotDir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (manifestWritten && !string.IsNullOrWhiteSpace(snapshotId))
        {
            await _locationsIndex.RemoveSnapshotAsync(programId, snapshotId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<SnapshotOperationResult> UpdateSnapshotAsync(
        ProgramProfile profile,
        string snapshotId,
        string displayName,
        string? newStorageRoot,
        CancellationToken cancellationToken = default)
    {
        var currentDir = SnapshotPathPlanner.ResolveSnapshotDirectory(
            _paths,
            _locationsIndex,
            profile.Id,
            snapshotId);
        var manifest = await LoadSnapshotManifestAsync(profile.Id, snapshotId, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Failed,
                Message = "Snapshot-Manifest nicht gefunden."
            };
        }

        var trimmedName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return new SnapshotOperationResult
            {
                Status = SnapshotResultStatus.Failed,
                Message = "Bitte einen Snapshot-Namen eingeben."
            };
        }

        manifest.DisplayName = trimmedName;
        var targetDir = currentDir;

        if (!string.IsNullOrWhiteSpace(newStorageRoot))
        {
            var movePlan = SnapshotPathPlanner.ResolveMovePlan(
                _paths,
                profile,
                snapshotId,
                newStorageRoot,
                manifest.IsExternal);

            if (!string.Equals(movePlan.SnapshotDir, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(movePlan.SnapshotDir) ?? movePlan.SnapshotDir);
                if (Directory.Exists(currentDir))
                {
                    Directory.Move(currentDir, movePlan.SnapshotDir);
                }

                targetDir = movePlan.SnapshotDir;
                manifest.StorageRoot = movePlan.StorageRoot;
                manifest.IsExternal = movePlan.IsExternal;
                await _locationsIndex.SetSnapshotPathAsync(profile.Id, snapshotId, targetDir, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var manifestPath = Path.Combine(targetDir, "manifest.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        SnapshotRestoreBatGenerator.WriteIfApplicable(targetDir, manifest);

        var (referenced, stored) = SnapshotCaptureEngine.CountStorageStats(manifest);
        var snapshot = new SnapshotInfo
        {
            Id = snapshotId,
            ProgramId = profile.Id,
            Name = trimmedName,
            Description = BuildDescription(manifest, referenced, stored),
            CreatedAt = manifest.CreatedAt,
            SizeBytes = GetDirectorySize(targetDir),
            Kind = manifest.Kind,
            ParentSnapshotId = manifest.ParentSnapshotId,
            CompressionEnabled = manifest.CompressionEnabled,
            ReferencedFileCount = referenced,
            StoredFileCount = stored,
            StoragePath = targetDir,
            IsExternal = manifest.IsExternal
        };

        return new SnapshotOperationResult
        {
            Status = SnapshotResultStatus.Success,
            Message = "Snapshot aktualisiert.",
            Snapshot = snapshot
        };
    }

    public async Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        CancellationToken cancellationToken = default)
        => await RestoreSnapshotAsync(profile, snapshot, selectedRelativePaths: null, progress: null, cancellationToken)
            .ConfigureAwait(false);

    public async Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        IReadOnlyCollection<string>? selectedRelativePaths,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
        => await RestoreSnapshotAsync(profile, snapshot, selectedRelativePaths, options: null, progress, cancellationToken)
            .ConfigureAwait(false);

    public async Task<RestoreOperationResult> RestoreSnapshotAsync(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        IReadOnlyCollection<string>? selectedRelativePaths,
        RestoreOptions? options,
        IProgress<RestoreProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var restoreOptions = options ?? RestoreOptions.Original;
        var manifest = await LoadSnapshotManifestAsync(profile.Id, snapshot.Id, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = "Snapshot-Manifest nicht gefunden. Wiederherstellung nicht möglich."
            };
        }

        var itemsToRestore = manifest.CapturedItems
            .Where(captured => captured.Exists)
            .Where(captured => IsSelectedForRestore(captured, selectedRelativePaths))
            .ToList();

        if (itemsToRestore.Count == 0)
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = "Keine Pfade für die Wiederherstellung ausgewählt.",
                TotalCount = 0
            };
        }

        var validationError = ValidateRestoreOptions(restoreOptions, manifest, selectedRelativePaths);
        if (validationError is not null)
        {
            return new RestoreOperationResult
            {
                Success = false,
                Message = validationError,
                TotalCount = itemsToRestore.Count
            };
        }

        var installLog = new List<string>();
        var installAttempted = false;
        var installSucceeded = false;
        var total = itemsToRestore.Count;

        if (restoreOptions.ReinstallProgram
            && restoreOptions.Mode == RestoreTargetMode.OriginalPaths
            && (profile.IsBound || manifest.ProgramInstall is not null))
        {
            progress?.Report(new RestoreProgressReport
            {
                Current = 0,
                Total = total,
                CurrentItemLabel = "Programm installieren…"
            });

            var installResult = await _programInstallService.TryInstallAsync(
                profile,
                manifest.ProgramInstall,
                new Progress<string>(installLog.Add),
                forceReinstall: true,
                cancellationToken).ConfigureAwait(false);

            installAttempted = installResult.Attempted;
            installSucceeded = installResult.Success;

            if (!string.IsNullOrWhiteSpace(installResult.Message)
                && (installLog.Count == 0 || installLog[^1] != installResult.Message))
            {
                installLog.Add(installResult.Message);
            }
        }

        var sourceProfileRoot = RestorePathRemapper.DetectSourceProfileRoot(manifest.CapturedItems);
        var restored = 0;
        var skipped = 0;
        var errors = new List<string>();
        var aclWarnings = new List<string>();
        var current = 0;
        var snapshotDir = GetSnapshotDirectoryPath(profile.Id, snapshot.Id);
        var allowOverwrite = !restoreOptions.RequiresExplicitOverwrite || restoreOptions.OverwriteConfirmed;
        var applyAcls = manifest.AclCopyEnabled;

        foreach (var item in itemsToRestore)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;

            progress?.Report(new RestoreProgressReport
            {
                Current = current,
                Total = total,
                CurrentItemLabel = item.Label
            });

            try
            {
                var targetRoot = RestorePathRemapper.MapTargetPath(item.SourcePath, restoreOptions, sourceProfileRoot);

                if (item.IsDirectory)
                {
                    var directoryResult = RestoreDirectoryItem(
                        profile.Id,
                        snapshot.Id,
                        snapshotDir,
                        item,
                        targetRoot,
                        allowOverwrite,
                        applyAcls,
                        aclWarnings);
                    if (directoryResult.Restored > 0)
                    {
                        restored++;
                    }
                    else
                    {
                        skipped++;
                        errors.Add($"{item.Label}: Keine Dateien im Snapshot gefunden.");
                    }

                    continue;
                }

                if (!_resolver.TryResolveSourceForRestore(profile.Id, snapshot.Id, item, out var sourcePath, out var isCompressed)
                    || sourcePath is null)
                {
                    skipped++;
                    errors.Add($"{item.Label}: Datei im Snapshot nicht gefunden.");
                    continue;
                }

                if (!allowOverwrite && File.Exists(targetRoot))
                {
                    skipped++;
                    errors.Add($"{item.Label}: Ziel existiert bereits — Überschreiben nicht bestätigt ({targetRoot}).");
                    continue;
                }

                RestoreFile(sourcePath, targetRoot, isCompressed, allowOverwrite);

                if (applyAcls)
                {
                    TryApplyAclsFromSidecar(
                        targetRoot,
                        snapshotDir,
                        item.SnapshotRelativePath,
                        isDirectory: false,
                        isCompressed,
                        aclWarnings);
                }

                restored++;
            }
            catch (IOException ex)
            {
                skipped++;
                errors.Add($"{item.Label}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                skipped++;
                errors.Add($"{item.Label}: {ex.Message}");
            }
        }

        var success = restored > 0;
        var modeLabel = restoreOptions.Mode switch
        {
            RestoreTargetMode.CustomRoot => "Zielsystem (Staging)",
            RestoreTargetMode.AlternateUserProfile => "Alternatives Benutzerprofil",
            _ => "Originalpfade"
        };

        var message = success switch
        {
            true when skipped == 0 && aclWarnings.Count == 0 => $"Wiederherstellung abgeschlossen ({modeLabel}): {restored} Element(e) zurückkopiert.",
            true when skipped == 0 => $"Wiederherstellung abgeschlossen ({modeLabel}): {restored} Element(e) — ACL: {aclWarnings.Count} Hinweis(e).",
            true => $"Wiederherstellung teilweise abgeschlossen ({modeLabel}): {restored} zurückkopiert, {skipped} übersprungen.",
            _ => "Keine Dateien wiederhergestellt — Snapshot leer oder Zielpfade nicht beschreibbar."
        };

        if (installAttempted)
        {
            var installNote = installSucceeded
                ? "Programm wurde installiert."
                : "Programm-Installation fehlgeschlagen oder nicht verifiziert — nur Einstellungen wiederhergestellt.";
            message = $"{installNote} {message}";
        }

        if (installLog.Count > 0)
        {
            errors.InsertRange(0, installLog.Select(line => $"[Install] {line}"));
        }

        return new RestoreOperationResult
        {
            Success = success,
            RestoredCount = restored,
            SkippedCount = skipped,
            TotalCount = total,
            Message = message,
            ErrorDetails = errors,
            AclWarnings = aclWarnings,
            ProgramInstallAttempted = installAttempted,
            ProgramInstallSucceeded = installSucceeded,
            InstallLog = installLog
        };
    }

    private static ProgramInstallMetadata BuildProgramInstallMetadata(ProgramProfile profile)
    {
        KnownWingetIds.EnrichProfile(profile);
        return new ProgramInstallMetadata
        {
            ProgramName = profile.Name,
            WingetId = profile.WingetId,
            InstallLocation = profile.InstallLocation,
            DisplayVersion = profile.InstalledVersion,
            Publisher = profile.Publisher
        };
    }

    private static string? ValidateRestoreOptions(
        RestoreOptions options,
        SnapshotManifest manifest,
        IReadOnlyCollection<string>? selectedRelativePaths)
    {
        if (options.Mode == RestoreTargetMode.CustomRoot)
        {
            if (string.IsNullOrWhiteSpace(options.CustomRootPath))
            {
                return "Bitte ein Zielverzeichnis (Staging-Ordner) angeben.";
            }

            try
            {
                Directory.CreateDirectory(options.CustomRootPath);
            }
            catch (Exception ex)
            {
                return $"Zielverzeichnis nicht verwendbar: {ex.Message}";
            }
        }

        if (options.Mode == RestoreTargetMode.AlternateUserProfile)
        {
            if (string.IsNullOrWhiteSpace(options.AlternateUserProfilePath))
            {
                return "Bitte ein alternatives Benutzerprofil-Verzeichnis angeben.";
            }

            try
            {
                Directory.CreateDirectory(options.AlternateUserProfilePath);
            }
            catch (Exception ex)
            {
                return $"Alternatives Profil nicht verwendbar: {ex.Message}";
            }
        }

        if (options.RequiresExplicitOverwrite)
        {
            var previews = RestorePathRemapper.BuildPreview(manifest.CapturedItems, selectedRelativePaths, options);
            if (RestorePathRemapper.HasTargetConflicts(previews) && !options.OverwriteConfirmed)
            {
                return "Am Ziel existieren bereits Dateien — bitte Überschreiben bestätigen oder anderen Ordner wählen.";
            }
        }

        return null;
    }

    public Task<SnapshotManifest?> LoadSnapshotManifestAsync(
        string programId,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_resolver.LoadManifest(programId, snapshotId));
    }

    public Task RefreshCurrentFlagsAsync(string programId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> DeleteSnapshotAsync(string programId, string snapshotId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotDir = GetSnapshotDirectoryPath(programId, snapshotId);
        if (!Directory.Exists(snapshotDir))
        {
            return Task.FromResult(false);
        }

        Directory.Delete(snapshotDir, recursive: true);
        return DeleteSnapshotIndexEntryAsync(programId, snapshotId, cancellationToken);
    }

    private async Task<bool> DeleteSnapshotIndexEntryAsync(
        string programId,
        string snapshotId,
        CancellationToken cancellationToken)
    {
        await _locationsIndex.RemoveSnapshotAsync(programId, snapshotId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public string GetSnapshotDirectoryPath(string programId, string snapshotId)
        => SnapshotPathPlanner.ResolveSnapshotDirectory(_paths, _locationsIndex, programId, snapshotId);

    private (int Restored, int Skipped) RestoreDirectoryItem(
        string programId,
        string snapshotId,
        string snapshotDir,
        CapturedItem item,
        string targetRoot,
        bool allowOverwrite,
        bool applyAcls,
        ICollection<string> aclWarnings)
    {
        if (item.Files.Count > 0)
        {
            var restored = 0;
            var skipped = 0;

            foreach (var file in item.Files.Where(record => record.Exists))
            {
                try
                {
                    var sourcePath = _resolver.ResolveRecordPath(
                        programId,
                        snapshotId,
                        snapshotDir,
                        item.SnapshotRelativePath,
                        file);

                    if (sourcePath is null)
                    {
                        skipped++;
                        continue;
                    }

                    var targetPath = Path.Combine(
                        targetRoot,
                        file.RelativePath.Replace('/', Path.DirectorySeparatorChar));

                    if (!allowOverwrite && File.Exists(targetPath))
                    {
                        skipped++;
                        continue;
                    }

                    var isCompressed = file.StorageKind == SnapshotStorageKind.Compressed;
                    RestoreFile(sourcePath, targetPath, isCompressed, allowOverwrite);

                    if (applyAcls)
                    {
                        var snapshotRelative = SnapshotContentHasher.NormalizeRelativePath(
                            Path.Combine(item.SnapshotRelativePath, file.RelativePath));
                        TryApplyAclsFromSidecar(
                            targetPath,
                            snapshotDir,
                            snapshotRelative,
                            isDirectory: false,
                            isCompressed,
                            aclWarnings);
                    }

                    restored++;
                }
                catch (IOException)
                {
                    skipped++;
                }
                catch (UnauthorizedAccessException)
                {
                    skipped++;
                }
            }

            if (applyAcls)
            {
                ApplyDirectoryAclsFromSidecars(snapshotDir, item.SnapshotRelativePath, targetRoot, aclWarnings);
            }

            return (restored, skipped);
        }

        var legacySource = Path.Combine(
            snapshotDir,
            item.SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(legacySource))
        {
            return (0, 1);
        }

        CopyDirectory(legacySource, targetRoot, allowOverwrite);

        if (applyAcls)
        {
            ApplyDirectoryAclsFromSidecars(snapshotDir, item.SnapshotRelativePath, targetRoot, aclWarnings);
        }

        return (1, 0);
    }

    private static void TryApplyAclsFromSidecar(
        string targetPath,
        string snapshotDir,
        string snapshotRelative,
        bool isDirectory,
        bool storedCompressed,
        ICollection<string> aclWarnings)
    {
        try
        {
            AclSidecarStore.ApplyFromSidecar(
                targetPath,
                snapshotDir,
                snapshotRelative,
                isDirectory,
                storedCompressed,
                aclWarnings);
        }
        catch (Exception ex)
        {
            aclWarnings.Add($"ACL/Owner für {targetPath} übersprungen: {ex.Message}");
        }
    }

    private static void ApplyDirectoryAclsFromSidecars(
        string snapshotDir,
        string snapshotRelativeRoot,
        string targetRoot,
        ICollection<string> aclWarnings)
    {
        var normalizedRoot = SnapshotContentHasher.NormalizeRelativePath(snapshotRelativeRoot);
        TryApplyAclsFromSidecar(
            targetRoot,
            snapshotDir,
            normalizedRoot,
            isDirectory: true,
            storedCompressed: false,
            aclWarnings);

        foreach (var sidecarPath in Directory.EnumerateFiles(
                     snapshotDir,
                     "*" + FileAclService.DirectorySidecarSuffix,
                     SearchOption.AllDirectories))
        {
            var snapshotRelative = TryGetSnapshotRelativeFromDirectorySidecar(snapshotDir, sidecarPath);
            if (snapshotRelative is null
                || !snapshotRelative.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(snapshotRelative, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativeWithinItem = Path.GetRelativePath(
                normalizedRoot.Replace('/', Path.DirectorySeparatorChar),
                snapshotRelative.Replace('/', Path.DirectorySeparatorChar));

            var targetDirectory = string.IsNullOrWhiteSpace(relativeWithinItem) || relativeWithinItem == "."
                ? targetRoot
                : Path.Combine(targetRoot, relativeWithinItem);

            TryApplyAclsFromSidecar(
                targetDirectory,
                snapshotDir,
                snapshotRelative,
                isDirectory: true,
                storedCompressed: false,
                aclWarnings);
        }
    }

    private static string? TryGetSnapshotRelativeFromDirectorySidecar(string snapshotDir, string sidecarPath)
    {
        if (!sidecarPath.EndsWith(FileAclService.DirectorySidecarSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = Path.GetRelativePath(snapshotDir, sidecarPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return null;
        }

        relative = relative[..^FileAclService.DirectorySidecarSuffix.Length];
        return SnapshotContentHasher.NormalizeRelativePath(relative);
    }

    private static void RestoreFile(string sourcePath, string targetPath, bool isCompressed, bool allowOverwrite)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (!allowOverwrite && File.Exists(targetPath))
        {
            throw new IOException($"Ziel existiert bereits: {targetPath}");
        }

        if (isCompressed)
        {
            SnapshotContentHasher.DecompressToFile(sourcePath, targetPath);
            return;
        }

        File.Copy(sourcePath, targetPath, overwrite: allowOverwrite);
    }

    private static void CopyDirectory(string sourceDir, string targetDir, bool allowOverwrite)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            if (!allowOverwrite && File.Exists(targetFile))
            {
                continue;
            }

            File.Copy(file, targetFile, overwrite: allowOverwrite);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(directory));
            CopyDirectory(directory, targetSubDir, allowOverwrite);
        }
    }

    private static bool IsSelectedForRestore(CapturedItem item, IReadOnlyCollection<string>? selectedRelativePaths)
    {
        if (selectedRelativePaths is null || selectedRelativePaths.Count == 0)
        {
            return true;
        }

        var normalized = item.SnapshotRelativePath.Replace('\\', '/');
        return selectedRelativePaths.Any(path =>
            string.Equals(path.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountCaptureWorkItems(ProgramProfile profile)
    {
        var count = 0;

        foreach (var pathEntry in profile.Paths)
        {
            var exists = pathEntry.IsDirectory
                ? Directory.Exists(pathEntry.SourcePath)
                : File.Exists(pathEntry.SourcePath);
            if (!exists)
            {
                continue;
            }

            if (pathEntry.IsDirectory)
            {
                count += Directory.EnumerateFiles(pathEntry.SourcePath, "*", SearchOption.AllDirectories).Count();
            }
            else
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildDescription(SnapshotManifest manifest, int referenced, int stored)
    {
        var kindLabel = manifest.Kind == SnapshotKind.Incremental ? "Inkrementell" : "Vollständig";
        var compressionLabel = manifest.CompressionEnabled ? "GZip" : "unkomprimiert";
        var captured = manifest.CapturedItems.Count(item => item.Exists);
        return $"{kindLabel} · {compressionLabel} · {stored} gespeichert · {referenced} referenziert · {manifest.CapturedItems.Count} Pfade ({captured} erfasst)";
    }

    private static long GetDirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file =>
            {
                try
                {
                    return new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    return 0L;
                }
            })
            .Sum();
    }
}
