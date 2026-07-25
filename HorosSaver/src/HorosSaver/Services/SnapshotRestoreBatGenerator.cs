using System.Text;
using System.Text.Json;
using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class SnapshotRestoreBatGenerator
{
    private static readonly UTF8Encoding BatEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteIfApplicable(string snapshotDir, SnapshotManifest manifest)
    {
        var batPath = Path.Combine(snapshotDir, "Wiederherstellen.bat");
        var content = BuildContent(snapshotDir, manifest);
        File.WriteAllText(batPath, content, BatEncoding);
    }

    public static bool TryRegenerate(string snapshotDir)
    {
        var manifestPath = Path.Combine(snapshotDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        var manifest = JsonSerializer.Deserialize<SnapshotManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions);
        if (manifest is null)
        {
            return false;
        }

        WriteIfApplicable(snapshotDir, manifest);
        return true;
    }

    public static int RegenerateAllKnown(IStoragePathResolver paths, ISnapshotLocationsIndexService locationsIndex)
    {
        var regenerated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(paths.SnapshotsRoot))
        {
            foreach (var manifestPath in Directory.EnumerateFiles(paths.SnapshotsRoot, "manifest.json", SearchOption.AllDirectories))
            {
                var snapshotDir = Path.GetDirectoryName(manifestPath);
                if (!string.IsNullOrWhiteSpace(snapshotDir) && TryRegenerate(snapshotDir))
                {
                    regenerated.Add(snapshotDir);
                }
            }
        }

        foreach (var entry in LoadIndexedSnapshotDirectories(locationsIndex))
        {
            if (TryRegenerate(entry))
            {
                regenerated.Add(entry);
            }
        }

        return regenerated.Count;
    }

    private static IEnumerable<string> LoadIndexedSnapshotDirectories(ISnapshotLocationsIndexService locationsIndex)
    {
        var document = locationsIndex.LoadAsync().GetAwaiter().GetResult();
        foreach (var entry in document.Locations)
        {
            if (!string.IsNullOrWhiteSpace(entry.AbsolutePath) && Directory.Exists(entry.AbsolutePath))
            {
                yield return entry.AbsolutePath;
            }
        }
    }

    public static string BuildContent(string snapshotDir, SnapshotManifest manifest)
    {
        var displayName = string.IsNullOrWhiteSpace(manifest.DisplayName)
            ? SnapshotDisplayName.Build(manifest.ProgramName, manifest.CreatedAt)
            : manifest.DisplayName.Trim();

        var embeddedContext = ResolveEmbeddedLaunchContext();

        var builder = new StringBuilder();
        builder.AppendLine("@echo off");
        builder.AppendLine("setlocal EnableExtensions EnableDelayedExpansion");
        builder.AppendLine("REM HorosSaver Snapshot-Wiederherstellung - HorosCode");
        builder.AppendLine($"REM Snapshot: {EscapeComment(displayName)}");
        builder.AppendLine($"REM Erstellt: {manifest.CreatedAt:yyyy-MM-dd HH:mm}");
        if (manifest.BatRestoreOptimized)
        {
            builder.AppendLine("REM Externer Snapshot: Vollstaendig + unkomprimiert fuer BAT-Robocopy.");
        }

        builder.AppendLine();
        AppendHorosSaverExeResolution(builder, embeddedContext);
        builder.AppendLine();

        if (manifest.Kind == SnapshotKind.Incremental || manifest.CompressionEnabled)
        {
            AppendIncrementalRestore(builder, manifest);
            return builder.ToString();
        }

        var restoreTargets = CollectRestoreTargetPaths(snapshotDir, manifest);
        AppendAdminElevationCheck(builder, RequiresAdminElevation(restoreTargets));

        builder.AppendLine("set \"SNAPSHOT_DIR=%~dp0\"");
        builder.AppendLine("echo Wiederherstellung starten...");
        builder.AppendLine("echo Snapshot-Ordner: %SNAPSHOT_DIR%");
        builder.AppendLine();
        AppendRestorePreamble(builder, manifest, restoreTargets);
        builder.AppendLine("set \"RESTORE_ERR_COUNT=0\"");
        builder.AppendLine("set \"RESTORE_FILES_COPIED=0\"");
        builder.AppendLine("set \"RESTORE_FILES_SKIPPED=0\"");
        builder.AppendLine("set \"PROGRAM_INSTALL_ATTEMPTED=0\"");
        builder.AppendLine("set \"PROGRAM_INSTALL_SUCCEEDED=0\"");
        AppendProgramInstallVariables(builder, manifest);
        builder.AppendLine();

        var hasActions = false;
        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists))
        {
            var sourceRelative = item.SnapshotRelativePath.Replace('/', '\\');
            var snapshotSource = Path.Combine(snapshotDir, sourceRelative);

            if (item.IsDirectory)
            {
                if (Directory.Exists(snapshotSource))
                {
                    AppendDirectoryRestore(builder, snapshotSource, item.SourcePath);
                    hasActions = true;
                }

                continue;
            }

            if (File.Exists(snapshotSource))
            {
                AppendFileRestore(builder, snapshotSource, item.SourcePath);
                hasActions = true;
            }
        }

        if (!hasActions)
        {
            builder.AppendLine("echo Keine wiederherstellbaren Dateien im Manifest gefunden.");
            builder.AppendLine("echo Starte HorosSaver fuer erweiterte Wiederherstellung...");
            AppendLaunchHorosSaverGui(builder, manifest.ProgramId, manifest.SnapshotId);
            builder.AppendLine("pause");
            builder.AppendLine("exit /b 1");
            return builder.ToString();
        }

        builder.AppendLine();
        AppendRestoreEpilogue(builder, manifest);
        AppendRestoreSubroutines(builder, manifest);
        return builder.ToString();
    }

    private static EmbeddedLaunchContext ResolveEmbeddedLaunchContext()
    {
        var layout = AppStoragePaths.Resolve();
        return new EmbeddedLaunchContext(
            HorosSaverExeLocator.ResolveCurrentExecutable(),
            layout.DataRoot,
            layout.IsPortable);
    }

    private static void AppendHorosSaverExeResolution(StringBuilder builder, EmbeddedLaunchContext context)
    {
        builder.AppendLine("set \"HOROSSAVER_EXE=\"");

        if (!string.IsNullOrWhiteSpace(context.ExecutablePath))
        {
            builder.AppendLine($"set \"HOROSSAVER_EMBEDDED={EscapeBat(context.ExecutablePath)}\"");
            builder.AppendLine("if exist \"%HOROSSAVER_EMBEDDED%\" set \"HOROSSAVER_EXE=%HOROSSAVER_EMBEDDED%\"");
        }

        builder.AppendLine(
            "if not defined HOROSSAVER_EXE if exist \"%~dp0..\\..\\..\\HorosSaver.exe\" set \"HOROSSAVER_EXE=%~dp0..\\..\\..\\HorosSaver.exe\"");
        builder.AppendLine(
            "if not defined HOROSSAVER_EXE if exist \"%LOCALAPPDATA%\\HorosCode\\HorosSaver\\HorosSaver.exe\" set \"HOROSSAVER_EXE=%LOCALAPPDATA%\\HorosCode\\HorosSaver\\HorosSaver.exe\"");
        builder.AppendLine(
            "if not defined HOROSSAVER_EXE if exist \"%LOCALAPPDATA%\\Programs\\HorosSaver\\HorosSaver.exe\" set \"HOROSSAVER_EXE=%LOCALAPPDATA%\\Programs\\HorosSaver\\HorosSaver.exe\"");
        builder.AppendLine(
            "if not defined HOROSSAVER_EXE if exist \"%LOCALAPPDATA%\\HorosSaver\\HorosSaver.exe\" set \"HOROSSAVER_EXE=%LOCALAPPDATA%\\HorosSaver\\HorosSaver.exe\"");

        if (!string.IsNullOrWhiteSpace(context.DataRoot))
        {
            builder.AppendLine($"set \"HOROSSAVER_DATA_ROOT_EMBEDDED={EscapeBat(context.DataRoot)}\"");
        }

        if (context.IsPortable)
        {
            builder.AppendLine("set \"HOROSSAVER_PORTABLE_EMBEDDED=1\"");
        }
    }

    private static void AppendPortableEnvironment(StringBuilder builder)
    {
        builder.AppendLine("if defined HOROSSAVER_DATA_ROOT_EMBEDDED set \"HOROSSAVER_DATA_ROOT=%HOROSSAVER_DATA_ROOT_EMBEDDED%\"");
        builder.AppendLine("if defined HOROSSAVER_PORTABLE_EMBEDDED set \"HOROSSAVER_PORTABLE=%HOROSSAVER_PORTABLE_EMBEDDED%\"");
    }

    private static void AppendIncrementalRestore(StringBuilder builder, SnapshotManifest manifest)
    {
        builder.AppendLine("echo Dieser Snapshot ist inkrementell oder komprimiert.");
        builder.AppendLine("echo HorosSaver fuehrt Dekompression und Referenz-Aufloesung durch.");
        builder.AppendLine("echo.");
        AppendPortableEnvironment(builder);
        builder.AppendLine("if defined HOROSSAVER_EXE (");
        builder.AppendLine("  echo HorosSaver: %HOROSSAVER_EXE%");
        builder.AppendLine(
            $"  \"%HOROSSAVER_EXE%\" --restore \"{EscapeBat(manifest.ProgramId)}\" \"{EscapeBat(manifest.SnapshotId)}\" --reinstall");
        builder.AppendLine("  set \"RESTORE_EXIT=!ERRORLEVEL!\"");
        builder.AppendLine("  if \"!RESTORE_EXIT!\"==\"0\" (");
        builder.AppendLine("    echo Wiederherstellung abgeschlossen.");
        builder.AppendLine("    pause");
        builder.AppendLine("    exit /b 0");
        builder.AppendLine("  )");
        builder.AppendLine("  echo CLI-Wiederherstellung fehlgeschlagen ^(Code !RESTORE_EXIT!^). Starte GUI...");
        AppendLaunchHorosSaverGui(builder, manifest.ProgramId, manifest.SnapshotId, indent: "  ");
        builder.AppendLine(") else (");
        if (!string.IsNullOrWhiteSpace(manifest.ProgramId))
        {
            builder.AppendLine("  echo Bitte HorosSaver manuell starten und Wiederherstellen waehlen.");
            builder.AppendLine($"  echo Programm-ID: {EscapeComment(manifest.ProgramId)}");
            builder.AppendLine($"  echo Snapshot-ID: {EscapeComment(manifest.SnapshotId)}");
        }

        builder.AppendLine("  if defined HOROSSAVER_EMBEDDED echo Erwarteter Pfad bei Erstellung: %HOROSSAVER_EMBEDDED%");
        builder.AppendLine("  echo HorosSaver.exe nicht gefunden. Bitte manuell starten.");
        builder.AppendLine(")");
        builder.AppendLine("pause");
        builder.AppendLine("exit /b 1");
    }

    private static void AppendLaunchHorosSaverGui(
        StringBuilder builder,
        string programId,
        string snapshotId,
        string indent = "")
    {
        builder.AppendLine($"{indent}if defined HOROSSAVER_EXE (");
        builder.AppendLine($"{indent}  start \"\" \"%HOROSSAVER_EXE%\"");
        builder.AppendLine($"{indent}) else (");
        builder.AppendLine($"{indent}  echo HorosSaver.exe nicht gefunden.");
        builder.AppendLine($"{indent}  echo Programm-ID: {EscapeComment(programId)} / Snapshot-ID: {EscapeComment(snapshotId)}");
        builder.AppendLine($"{indent})");
    }

    private static List<string> CollectRestoreTargetPaths(string snapshotDir, SnapshotManifest manifest)
    {
        var targets = new List<string>();

        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists))
        {
            var sourceRelative = item.SnapshotRelativePath.Replace('/', '\\');
            var snapshotSource = Path.Combine(snapshotDir, sourceRelative);

            if (item.IsDirectory)
            {
                if (Directory.Exists(snapshotSource))
                {
                    targets.Add(item.SourcePath);
                }

                continue;
            }

            if (File.Exists(snapshotSource))
            {
                targets.Add(item.SourcePath);
            }
        }

        return targets;
    }

    private static bool RequiresAdminElevation(IEnumerable<string> targetPaths)
    {
        foreach (var targetPath in targetPaths)
        {
            if (IsProgramFilesPath(targetPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProgramFilesPath(string path)
    {
        var normalized = path.Replace('/', '\\');
        return normalized.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> CollectProcessImageWarnings(
        SnapshotManifest manifest,
        IEnumerable<string> targetPaths)
    {
        var warnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (!targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var imageName = Path.GetFileName(targetPath);
            if (!string.IsNullOrWhiteSpace(imageName))
            {
                warnings.Add(imageName);
            }
        }

        if (IsSevenZipRestore(manifest, targetPaths))
        {
            warnings.Add("7zFM.exe");
            warnings.Add("7zG.exe");
        }

        return warnings.OrderBy(imageName => imageName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsSevenZipRestore(SnapshotManifest manifest, IEnumerable<string> targetPaths)
    {
        if (manifest.ProgramName.Contains("7-zip", StringComparison.OrdinalIgnoreCase)
            || manifest.ProgramName.Contains("7zip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var targetPath in targetPaths)
        {
            if (targetPath.Contains("7-Zip", StringComparison.OrdinalIgnoreCase)
                || targetPath.Contains("7-zip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendAdminElevationCheck(StringBuilder builder, bool requiresAdmin)
    {
        if (!requiresAdmin)
        {
            return;
        }

        builder.AppendLine("if /I \"%~1\"==\"__elevated\" set \"HOROSSAVER_ALREADY_ELEVATED=1\"");
        builder.AppendLine("set \"NEEDS_ELEVATION=1\"");
        builder.AppendLine("if \"%NEEDS_ELEVATION%\"==\"1\" (");
        builder.AppendLine("  net session >nul 2>&1");
        builder.AppendLine("  if errorlevel 1 (");
        builder.AppendLine("    if defined HOROSSAVER_ALREADY_ELEVATED (");
        builder.AppendLine("      echo FEHLER: Administratorrechte konnten nicht aktiviert werden.");
        builder.AppendLine("      echo Bitte Wiederherstellen.bat per Rechtsklick ^> Als Administrator ausfuehren.");
        builder.AppendLine("      pause");
        builder.AppendLine("      exit /b 1");
        builder.AppendLine("    )");
        builder.AppendLine("    echo Diese Wiederherstellung benoetigt Administratorrechte ^(Ziel unter Program Files^).");
        builder.AppendLine("    echo Bitte UAC-Dialog bestaetigen, falls Windows danach fragt.");
        builder.AppendLine("    echo Dieses Fenster schliesst sich - die Wiederherstellung laeuft im Administrator-Fenster weiter.");
        builder.AppendLine("    powershell -NoProfile -Command \"Start-Process -FilePath '%~f0' -ArgumentList '__elevated' -Verb RunAs\"");
        builder.AppendLine("    exit /b 0");
        builder.AppendLine("  )");
        builder.AppendLine(")");
        builder.AppendLine();
    }

    private static void AppendRestorePreamble(
        StringBuilder builder,
        SnapshotManifest manifest,
        IReadOnlyList<string> restoreTargets)
    {
        builder.AppendLine("echo WICHTIG: Schliessen Sie das Zielprogramm vor der Wiederherstellung.");
        builder.AppendLine("echo Offene Programme koennen Dateien sperren und die Wiederherstellung verhindern.");
        if (!string.IsNullOrWhiteSpace(manifest.ProgramName))
        {
            builder.AppendLine($"echo Zielprogramm: {EscapeComment(manifest.ProgramName.Trim())}");
        }

        builder.AppendLine("echo.");

        foreach (var imageName in CollectProcessImageWarnings(manifest, restoreTargets))
        {
            builder.AppendLine($"tasklist /FI \"IMAGENAME eq {EscapeBat(imageName)}\" 2>nul | find /I \"{EscapeBat(imageName)}\" >nul");
            builder.AppendLine("if not errorlevel 1 (");
            builder.AppendLine($"  echo WARNUNG: {EscapeComment(imageName)} laeuft noch. Bitte das Zielprogramm schliessen.");
            builder.AppendLine(")");
        }

        builder.AppendLine("echo Starte Datei-Wiederherstellung...");
        builder.AppendLine("echo.");
    }

    private static void AppendProgramInstallVariables(StringBuilder builder, SnapshotManifest manifest)
    {
        var programInstall = manifest.ProgramInstall;
        if (!HasProgramInstallPostRestore(manifest))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(programInstall?.WingetId))
        {
            builder.AppendLine($"set \"PI_WINGET_ID={EscapeBat(programInstall.WingetId.Trim())}\"");
        }

        var installLocation = ResolveInstallLocation(manifest);
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            builder.AppendLine($"set \"PI_INSTALL_LOC={EscapeBat(installLocation.Trim())}\"");
        }

        var programName = programInstall?.ProgramName ?? manifest.ProgramName;
        if (!string.IsNullOrWhiteSpace(programName))
        {
            builder.AppendLine($"set \"PI_PROGRAM_NAME={EscapeBat(programName.Trim())}\"");
            builder.AppendLine($"set \"PI_START_MENU_NAME={EscapeBat(ResolveStartMenuFolderName(programName))}\"");
        }

        if (!string.IsNullOrWhiteSpace(programInstall?.DisplayVersion))
        {
            builder.AppendLine($"set \"PI_VERSION={EscapeBat(programInstall.DisplayVersion.Trim())}\"");
        }

        if (!string.IsNullOrWhiteSpace(programInstall?.Publisher))
        {
            builder.AppendLine($"set \"PI_PUBLISHER={EscapeBat(programInstall.Publisher.Trim())}\"");
        }

        var uninstallKey = ResolveUninstallRegistryKeyName(manifest);
        builder.AppendLine($"set \"PI_UNINSTALL_KEY={EscapeBat(uninstallKey)}\"");

        var displayIconRelative = ResolveDisplayIconRelativePath(manifest);
        if (!string.IsNullOrWhiteSpace(displayIconRelative))
        {
            builder.AppendLine($"set \"PI_DISPLAY_ICON_REL={EscapeBat(displayIconRelative)}\"");
        }

        if (!string.IsNullOrWhiteSpace(manifest.ProgramId))
        {
            builder.AppendLine($"set \"HOROSSAVER_PROGRAM_ID={EscapeBat(manifest.ProgramId.Trim())}\"");
        }

        if (!string.IsNullOrWhiteSpace(manifest.SnapshotId))
        {
            builder.AppendLine($"set \"HOROSSAVER_SNAPSHOT_ID={EscapeBat(manifest.SnapshotId.Trim())}\"");
        }
    }

    private static bool HasProgramInstallPostRestore(SnapshotManifest manifest)
    {
        if (manifest.ProgramInstall is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(manifest.ProgramInstall.WingetId)
            || !string.IsNullOrWhiteSpace(ResolveInstallLocation(manifest));
    }

    private static string? ResolveInstallLocation(SnapshotManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.ProgramInstall?.InstallLocation))
        {
            return manifest.ProgramInstall.InstallLocation.Trim();
        }

        foreach (var item in manifest.CapturedItems.Where(captured => captured.Exists && captured.IsDirectory))
        {
            if (IsProgramFilesPath(item.SourcePath))
            {
                return item.SourcePath;
            }
        }

        return null;
    }

    private static void AppendRestoreEpilogue(StringBuilder builder, SnapshotManifest manifest)
    {
        var hasProgramInstall = HasProgramInstallPostRestore(manifest);

        if (hasProgramInstall)
        {
            builder.AppendLine("call :TryProgramInstall");
        }

        builder.AppendLine("echo.");
        builder.AppendLine("echo === Zusammenfassung ===");
        builder.AppendLine("echo Dateien kopiert: !RESTORE_FILES_COPIED!");
        builder.AppendLine("echo Dateien uebersprungen: !RESTORE_FILES_SKIPPED!");
        if (hasProgramInstall)
        {
            builder.AppendLine("if !PROGRAM_INSTALL_ATTEMPTED! EQU 1 (");
            builder.AppendLine("  if !PROGRAM_INSTALL_SUCCEEDED! EQU 1 (");
            builder.AppendLine("    echo Programm-Installation: erfolgreich ^(Programme und Features/Startmenue^)");
            builder.AppendLine("  ) else (");
            builder.AppendLine("    echo Programm-Installation: fehlgeschlagen oder unvollstaendig");
            builder.AppendLine("  )");
            builder.AppendLine(") else (");
            builder.AppendLine("  echo Programm-Installation: nicht versucht");
            builder.AppendLine(")");
        }
        else
        {
            builder.AppendLine("echo Programm-Installation: nur Dateien wiederhergestellt ^(kein winget-Eintrag im Snapshot^)");
        }

        builder.AppendLine("echo.");
        builder.AppendLine("if !RESTORE_ERR_COUNT! GTR 0 (");
        builder.AppendLine("  echo Wiederherstellung mit Fehlern abgeschlossen: !RESTORE_ERR_COUNT! Vorgaenge fehlgeschlagen.");
        builder.AppendLine("  echo Pruefen Sie gesperrte Dateien und starten Sie ggf. nach Programmende erneut.");
        builder.AppendLine("  pause");
        builder.AppendLine("  exit /b 1");
        builder.AppendLine(")");
        builder.AppendLine("if !RESTORE_FILES_COPIED! EQU 0 if !RESTORE_FILES_SKIPPED! GTR 0 (");
        builder.AppendLine("  echo Hinweis: Alle Dateien waren bereits am Ziel vorhanden ^(identisch^).");
        if (hasProgramInstall)
        {
            builder.AppendLine("  echo Registry/Startmenue wurden ggf. ueber winget nachinstalliert.");
        }

        builder.AppendLine(")");
        builder.AppendLine("echo Wiederherstellung abgeschlossen.");
        builder.AppendLine("pause");
        builder.AppendLine("exit /b 0");
    }

    private static void AppendRestoreSubroutines(StringBuilder builder, SnapshotManifest manifest)
    {
        builder.AppendLine("goto :RestoreDone");
        builder.AppendLine();
        builder.AppendLine(":RestoreFile");
        builder.AppendLine("set \"RF_SOURCE_DIR=%~1\"");
        builder.AppendLine("set \"RF_TARGET_DIR=%~2\"");
        builder.AppendLine("set \"RF_NAME=%~3\"");
        builder.AppendLine("echo.");
        builder.AppendLine("echo [Wiederherstellung] Datei: %RF_NAME%");
        builder.AppendLine("echo [Wiederherstellung] Quelle: %RF_SOURCE_DIR%");
        builder.AppendLine("echo [Wiederherstellung] Ziel:   %RF_TARGET_DIR%");
        builder.AppendLine("echo Bitte warten, Robocopy laeuft...");
        builder.AppendLine("if not exist \"%RF_TARGET_DIR%\" mkdir \"%RF_TARGET_DIR%\"");
        builder.AppendLine("set \"HS_ROBO_LOG=%TEMP%\\horossaver_robocopy_%RANDOM%.log\"");
        builder.AppendLine("robocopy \"%RF_SOURCE_DIR%\" \"%RF_TARGET_DIR%\" \"%RF_NAME%\" /COPY:DAT /IS /IT /R:5 /W:2 /NDL /NFL /NP /BYTES > \"!HS_ROBO_LOG!\" 2>&1");
        builder.AppendLine("set \"RF_RC=!ERRORLEVEL!\"");
        builder.AppendLine("type \"!HS_ROBO_LOG!\"");
        builder.AppendLine("call :AccumulateRobocopyStats \"!HS_ROBO_LOG!\"");
        builder.AppendLine("if !RF_RC! GEQ 8 (");
        builder.AppendLine("  set /a RESTORE_ERR_COUNT+=1");
        builder.AppendLine("  echo   [FEHLER] %RF_TARGET_DIR%\\%RF_NAME% ^(Robocopy Code !RF_RC!^)");
        builder.AppendLine(") else (");
        builder.AppendLine("  echo   [OK] %RF_TARGET_DIR%\\%RF_NAME% ^(Robocopy Code !RF_RC!^)");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        builder.AppendLine(":RestoreDir");
        builder.AppendLine("set \"RD_SOURCE=%~1\"");
        builder.AppendLine("set \"RD_TARGET=%~2\"");
        builder.AppendLine("echo.");
        builder.AppendLine("echo [Wiederherstellung] Ordner-Kopie");
        builder.AppendLine("echo [Wiederherstellung] Quelle: %RD_SOURCE%");
        builder.AppendLine("echo [Wiederherstellung] Ziel:   %RD_TARGET%");
        builder.AppendLine("echo Bitte warten, Robocopy laeuft ^(kann einige Minuten dauern^)...");
        builder.AppendLine("if not exist \"%RD_TARGET%\" mkdir \"%RD_TARGET%\"");
        builder.AppendLine("set \"HS_ROBO_LOG=%TEMP%\\horossaver_robocopy_%RANDOM%.log\"");
        builder.AppendLine("robocopy \"%RD_SOURCE%\" \"%RD_TARGET%\" /E /COPY:DAT /IS /IT /R:5 /W:2 /NDL /NFL /NP /BYTES > \"!HS_ROBO_LOG!\" 2>&1");
        builder.AppendLine("set \"RD_RC=!ERRORLEVEL!\"");
        builder.AppendLine("type \"!HS_ROBO_LOG!\"");
        builder.AppendLine("call :AccumulateRobocopyStats \"!HS_ROBO_LOG!\"");
        builder.AppendLine("if !RD_RC! GEQ 8 (");
        builder.AppendLine("  set /a RESTORE_ERR_COUNT+=1");
        builder.AppendLine("  echo   [FEHLER] %RD_TARGET% ^(Robocopy Code !RD_RC!^)");
        builder.AppendLine(") else (");
        builder.AppendLine("  echo   [OK] %RD_TARGET% ^(Robocopy Code !RD_RC!^)");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        builder.AppendLine(":AccumulateRobocopyStats");
        builder.AppendLine("set \"STATS_FILE=%~1\"");
        builder.AppendLine("if not exist \"%STATS_FILE%\" exit /b 0");
        builder.AppendLine("for /f \"tokens=2 delims=:\" %%P in ('findstr /C:\"Files :\" \"%STATS_FILE%\"') do (");
        builder.AppendLine("  for /f \"tokens=1,2,3\" %%a in (%%P) do (");
        builder.AppendLine("    set /a RESTORE_FILES_COPIED+=%%b");
        builder.AppendLine("    set /a RESTORE_FILES_SKIPPED+=%%c");
        builder.AppendLine("  )");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        AppendProgramInstallSubroutines(builder, manifest);
        builder.AppendLine(":RestoreDone");
    }

    private static void AppendProgramInstallSubroutines(StringBuilder builder, SnapshotManifest manifest)
    {
        if (!HasProgramInstallPostRestore(manifest))
        {
            return;
        }

        builder.AppendLine(":TryProgramInstall");
        builder.AppendLine("if not defined PI_WINGET_ID if not defined PI_INSTALL_LOC exit /b 0");
        builder.AppendLine("echo.");
        builder.AppendLine("echo === Programm-Installation ^(Registry, Startmenue^) ===");
        builder.AppendLine("set \"PI_EXE_FOUND=0\"");
        builder.AppendLine("if defined PI_INSTALL_LOC if exist \"%PI_INSTALL_LOC%\" (");
        builder.AppendLine("  dir /b \"%PI_INSTALL_LOC%\\*.exe\" >nul 2>&1");
        builder.AppendLine("  if not errorlevel 1 set \"PI_EXE_FOUND=1\"");
        builder.AppendLine(")");
        if (!string.IsNullOrWhiteSpace(manifest.ProgramInstall?.WingetId))
        {
            builder.AppendLine("call :WingetInstallProgram");
        }

        builder.AppendLine("call :CreateStartMenuShortcuts");
        builder.AppendLine("call :RegisterUninstall");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        builder.AppendLine(":WingetInstallProgram");
        builder.AppendLine("if not defined PI_WINGET_ID exit /b 0");
        builder.AppendLine("set \"WINGET_EXE=\"");
        builder.AppendLine("where winget >nul 2>&1");
        builder.AppendLine("if not errorlevel 1 for /f \"delims=\" %%W in ('where winget 2^>nul') do (");
        builder.AppendLine("  if not defined WINGET_EXE set \"WINGET_EXE=%%W\"");
        builder.AppendLine(")");
        builder.AppendLine("if not defined WINGET_EXE if exist \"%LOCALAPPDATA%\\Microsoft\\WindowsApps\\winget.exe\" set \"WINGET_EXE=%LOCALAPPDATA%\\Microsoft\\WindowsApps\\winget.exe\"");
        builder.AppendLine("if not defined WINGET_EXE if exist \"%LOCALAPPDATA%\\Microsoft\\WindowsApps\\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\\winget.exe\" set \"WINGET_EXE=%LOCALAPPDATA%\\Microsoft\\WindowsApps\\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\\winget.exe\"");
        builder.AppendLine("if not defined WINGET_EXE (");
        builder.AppendLine("  echo winget nicht gefunden. Bitte Programm manuell installieren ^(ID: %PI_WINGET_ID%^).");
        builder.AppendLine("  exit /b 0");
        builder.AppendLine(")");
        builder.AppendLine("echo winget: %WINGET_EXE%");
        builder.AppendLine("set \"PROGRAM_INSTALL_ATTEMPTED=1\"");
        builder.AppendLine("if !PI_EXE_FOUND! EQU 1 (");
        builder.AppendLine("  echo Programmdateien vorhanden - winget --force fuer Programme und Features...");
        builder.AppendLine("  \"%WINGET_EXE%\" install --id %PI_WINGET_ID% -e --accept-package-agreements --accept-source-agreements --force");
        builder.AppendLine(") else (");
        builder.AppendLine("  echo Programmdateien fehlen - winget install...");
        builder.AppendLine("  \"%WINGET_EXE%\" install --id %PI_WINGET_ID% -e --accept-package-agreements --accept-source-agreements");
        builder.AppendLine(")");
        builder.AppendLine("set \"WINGET_RC=!ERRORLEVEL!\"");
        builder.AppendLine("echo winget beendet mit Code !WINGET_RC!");
        builder.AppendLine("if !WINGET_RC! EQU 0 set \"PROGRAM_INSTALL_SUCCEEDED=1\"");
        builder.AppendLine("if !WINGET_RC! EQU -1978335189 set \"PROGRAM_INSTALL_SUCCEEDED=1\"");
        builder.AppendLine("if !WINGET_RC! EQU 2316632107 set \"PROGRAM_INSTALL_SUCCEEDED=1\"");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        AppendCreateStartMenuShortcutsSubroutine(builder, manifest);
        AppendRegisterUninstallSubroutine(builder);
    }

    private static void AppendCreateStartMenuShortcutsSubroutine(StringBuilder builder, SnapshotManifest manifest)
    {
        var shortcuts = ResolveStartMenuShortcuts(manifest);
        var hasKnownShortcuts = shortcuts.Count > 0;

        builder.AppendLine(":CreateStartMenuShortcuts");
        builder.AppendLine("if not defined PI_INSTALL_LOC exit /b 0");
        builder.AppendLine("if not exist \"%PI_INSTALL_LOC%\" (");
        builder.AppendLine("  echo Startmenue: Installationsordner nicht gefunden - keine Verknuepfungen.");
        builder.AppendLine("  exit /b 0");
        builder.AppendLine(")");
        builder.AppendLine("if not defined PI_START_MENU_NAME (");
        builder.AppendLine("  for %%I in (\"%PI_INSTALL_LOC%\") do set \"PI_START_MENU_NAME=%%~nxI\"");
        builder.AppendLine(")");
        builder.AppendLine("set \"SM_FOLDER=%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\%PI_START_MENU_NAME%\"");
        builder.AppendLine("if not exist \"%SM_FOLDER%\" mkdir \"%SM_FOLDER%\"");
        builder.AppendLine("set \"SM_CREATED=0\"");
        builder.AppendLine("echo Startmenue-Ordner: %SM_FOLDER%");

        if (hasKnownShortcuts)
        {
            foreach (var (linkName, relativePath) in shortcuts)
            {
                builder.AppendLine($"if exist \"%PI_INSTALL_LOC%\\{EscapeBat(relativePath)}\" (");
                builder.AppendLine($"  call :CreateStartMenuShortcut \"{EscapeBat(linkName)}\" \"%PI_INSTALL_LOC%\\{EscapeBat(relativePath)}\"");
                builder.AppendLine(")");
            }
        }
        else
        {
            builder.AppendLine("call :CreateGenericStartMenuShortcuts");
        }

        builder.AppendLine("if !SM_CREATED! GTR 0 (");
        builder.AppendLine("  echo Startmenue-Verknuepfungen angelegt");
        builder.AppendLine(") else (");
        builder.AppendLine("  echo Startmenue: keine passenden Zieldateien gefunden.");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        builder.AppendLine(":CreateStartMenuShortcut");
        builder.AppendLine("set \"SMS_LINK_NAME=%~1\"");
        builder.AppendLine("set \"SMS_TARGET=%~2\"");
        builder.AppendLine("if not exist \"%SMS_TARGET%\" exit /b 0");
        builder.AppendLine("set \"SMS_LINK=%SM_FOLDER%\\!SMS_LINK_NAME!.lnk\"");
        builder.AppendLine("powershell -NoProfile -Command \"$s=New-Object -ComObject WScript.Shell;$l=$s.CreateShortcut('!SMS_LINK!');$l.TargetPath='!SMS_TARGET!';$l.WorkingDirectory='%PI_INSTALL_LOC%';$l.Save()\"");
        builder.AppendLine("if not errorlevel 1 set /a SM_CREATED+=1");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();

        if (!hasKnownShortcuts)
        {
            builder.AppendLine(":CreateGenericStartMenuShortcuts");
            builder.AppendLine("set \"GEN_EXE_COUNT=0\"");
            builder.AppendLine("for %%F in (\"%PI_INSTALL_LOC%\\*.exe\") do (");
            builder.AppendLine("  if !GEN_EXE_COUNT! LSS 3 (");
            builder.AppendLine("    echo %%~nxF | findstr /I /R \"uninstall setup update\" >nul");
            builder.AppendLine("    if errorlevel 1 (");
            builder.AppendLine("      call :CreateStartMenuShortcut \"%%~nF\" \"%%~fF\"");
            builder.AppendLine("      set /a GEN_EXE_COUNT+=1");
            builder.AppendLine("    )");
            builder.AppendLine("  )");
            builder.AppendLine(")");
            builder.AppendLine("exit /b 0");
            builder.AppendLine();
        }
    }

    private static void AppendRegisterUninstallSubroutine(StringBuilder builder)
    {
        builder.AppendLine(":RegisterUninstall");
        builder.AppendLine("if not defined PI_INSTALL_LOC exit /b 0");
        builder.AppendLine("if not exist \"%PI_INSTALL_LOC%\" exit /b 0");
        builder.AppendLine("if not defined PI_PROGRAM_NAME exit /b 0");
        builder.AppendLine("if not defined PI_UNINSTALL_KEY (");
        builder.AppendLine("  for %%I in (\"%PI_INSTALL_LOC%\") do set \"PI_UNINSTALL_KEY=%%~nxI\"");
        builder.AppendLine(")");
        builder.AppendLine("echo.");
        builder.AppendLine("echo === Programme und Features ^(Uninstall-Registry^) ===");
        builder.AppendLine("set \"PROGRAM_INSTALL_ATTEMPTED=1\"");
        builder.AppendLine("set \"PI_REG_BASE=HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\!PI_UNINSTALL_KEY!\"");
        builder.AppendLine("set \"PI_UNINSTALL_EXE=%PI_INSTALL_LOC%\\Uninstall.exe\"");
        builder.AppendLine("if exist \"!PI_UNINSTALL_EXE!\" (");
        builder.AppendLine("  set \"PI_UNINSTALL_STRING=\\\"!PI_UNINSTALL_EXE!\\\"\"");
        builder.AppendLine(") else if defined PI_WINGET_ID (");
        builder.AppendLine("  set \"PI_UNINSTALL_STRING=winget uninstall --id %PI_WINGET_ID% -e\"");
        builder.AppendLine(") else (");
        builder.AppendLine("  echo Kein Deinstaller gefunden - ueberspringe Registry-Eintrag.");
        builder.AppendLine("  exit /b 0");
        builder.AppendLine(")");
        builder.AppendLine("if defined PI_DISPLAY_ICON_REL (");
        builder.AppendLine("  set \"PI_DISPLAY_ICON=%PI_INSTALL_LOC%\\!PI_DISPLAY_ICON_REL!\"");
        builder.AppendLine(") else (");
        builder.AppendLine("  for %%F in (\"%PI_INSTALL_LOC%\\*.exe\") do (");
        builder.AppendLine("    echo %%~nxF | findstr /I /R \"uninstall setup update\" >nul");
        builder.AppendLine("    if errorlevel 1 (");
        builder.AppendLine("      set \"PI_DISPLAY_ICON=%%~fF\"");
        builder.AppendLine("      goto :RegUninstallIconDone");
        builder.AppendLine("    )");
        builder.AppendLine("  )");
        builder.AppendLine(")");
        builder.AppendLine(":RegUninstallIconDone");
        builder.AppendLine("if not defined PI_DISPLAY_ICON set \"PI_DISPLAY_ICON=%PI_INSTALL_LOC%\"");
        builder.AppendLine("if not defined PI_VERSION set \"PI_VERSION=0.0.0\"");
        builder.AppendLine("if not defined PI_PUBLISHER set \"PI_PUBLISHER=Unknown\"");
        builder.AppendLine("set \"PI_INSTALL_LOC_REG=%PI_INSTALL_LOC%\\\"");
        builder.AppendLine("set \"PI_ESTIMATED_SIZE=0\"");
        builder.AppendLine("for /f \"usebackq delims=\" %%S in (`powershell -NoProfile -Command \"$s=(Get-ChildItem -LiteralPath '%PI_INSTALL_LOC%' -Recurse -File -EA SilentlyContinue ^| Measure-Object -Property Length -Sum).Sum; if($s){[int]($s/1KB)}else{0}\"`) do set \"PI_ESTIMATED_SIZE=%%S\"");
        builder.AppendLine("echo Registry: !PI_REG_BASE!");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v DisplayName /t REG_SZ /d \"!PI_PROGRAM_NAME!\" /f");
        builder.AppendLine("if errorlevel 1 goto :RegUninstallFailed");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v DisplayVersion /t REG_SZ /d \"!PI_VERSION!\" /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v Publisher /t REG_SZ /d \"!PI_PUBLISHER!\" /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v InstallLocation /t REG_SZ /d \"!PI_INSTALL_LOC_REG!\" /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v UninstallString /t REG_SZ /d \"!PI_UNINSTALL_STRING!\" /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v DisplayIcon /t REG_SZ /d \"!PI_DISPLAY_ICON!\" /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v EstimatedSize /t REG_DWORD /d !PI_ESTIMATED_SIZE! /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v NoModify /t REG_DWORD /d 1 /f");
        builder.AppendLine("reg add \"!PI_REG_BASE!\" /v NoRepair /t REG_DWORD /d 1 /f");
        builder.AppendLine("echo Uninstall-Registry eingetragen fuer Programme und Features.");
        builder.AppendLine("set \"PROGRAM_INSTALL_SUCCEEDED=1\"");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
        builder.AppendLine(":RegUninstallFailed");
        builder.AppendLine("echo FEHLER: Uninstall-Registry konnte nicht geschrieben werden ^(Adminrechte?^).");
        builder.AppendLine("exit /b 0");
        builder.AppendLine();
    }

    private static IReadOnlyList<(string LinkName, string RelativePath)> ResolveStartMenuShortcuts(SnapshotManifest manifest)
    {
        var programName = manifest.ProgramInstall?.ProgramName ?? manifest.ProgramName ?? string.Empty;
        var installLocation = manifest.ProgramInstall?.InstallLocation;
        var targetPaths = manifest.CapturedItems
            .Where(captured => captured.Exists)
            .Select(captured => captured.SourcePath);

        if (IsSevenZipRestore(manifest, targetPaths) || IsSevenZipName(programName))
        {
            return
            [
                ("7-Zip File Manager", "7zFM.exe"),
                ("7-Zip", "7zG.exe"),
                ("7-Zip Help", "7-zip.chm")
            ];
        }

        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return [];
        }

        var installDir = installLocation.Trim();
        if (!Directory.Exists(installDir))
        {
            return [];
        }

        return Directory.EnumerateFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Where(fileName => !IsExcludedStartMenuExecutable(fileName!))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(fileName => (Path.GetFileNameWithoutExtension(fileName!), fileName!))
            .ToArray();
    }

    private static bool IsSevenZipName(string programName)
    {
        return programName.Contains("7-zip", StringComparison.OrdinalIgnoreCase)
            || programName.Contains("7zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveUninstallRegistryKeyName(SnapshotManifest manifest)
    {
        var programName = manifest.ProgramInstall?.ProgramName ?? manifest.ProgramName ?? string.Empty;
        var targetPaths = manifest.CapturedItems
            .Where(captured => captured.Exists)
            .Select(captured => captured.SourcePath);

        if (IsSevenZipRestore(manifest, targetPaths) || IsSevenZipName(programName))
        {
            return "7-Zip";
        }

        var sanitized = SanitizeRegistryKeyName(programName);
        return string.IsNullOrWhiteSpace(sanitized) ? "HorosSaver.Restored" : sanitized;
    }

    private static string? ResolveDisplayIconRelativePath(SnapshotManifest manifest)
    {
        var programName = manifest.ProgramInstall?.ProgramName ?? manifest.ProgramName ?? string.Empty;
        var targetPaths = manifest.CapturedItems
            .Where(captured => captured.Exists)
            .Select(captured => captured.SourcePath);

        if (IsSevenZipRestore(manifest, targetPaths) || IsSevenZipName(programName))
        {
            return "7zFM.exe";
        }

        return null;
    }

    private static string SanitizeRegistryKeyName(string programName)
    {
        var trimmed = programName.Trim();
        var versionIndex = trimmed.IndexOf(" (", StringComparison.Ordinal);
        if (versionIndex > 0)
        {
            trimmed = trimmed[..versionIndex].Trim();
        }

        var dashIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            trimmed = trimmed[..dashIndex].Trim();
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalidChar, '_');
        }

        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    private static bool IsExcludedStartMenuExecutable(string fileName)
    {
        return fileName.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("setup", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("update", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveStartMenuFolderName(string programName)
    {
        var trimmed = programName.Trim();
        if (IsSevenZipName(trimmed))
        {
            return "7-Zip";
        }

        var versionIndex = trimmed.IndexOf(" (", StringComparison.Ordinal);
        if (versionIndex > 0)
        {
            trimmed = trimmed[..versionIndex].Trim();
        }

        var dashIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            trimmed = trimmed[..dashIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "Programm" : trimmed;
    }

    private static void AppendDirectoryRestore(StringBuilder builder, string sourceDir, string targetDir)
    {
        builder.AppendLine($"call :RestoreDir {QuoteBat(sourceDir)} {QuoteBat(targetDir)}");
    }

    private static void AppendFileRestore(StringBuilder builder, string sourceFile, string targetFile)
    {
        var sourceDir = Path.GetDirectoryName(sourceFile);
        var targetDir = Path.GetDirectoryName(targetFile);
        var fileName = Path.GetFileName(targetFile);
        if (string.IsNullOrWhiteSpace(sourceDir)
            || string.IsNullOrWhiteSpace(targetDir)
            || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        builder.AppendLine(
            $"call :RestoreFile {QuoteBat(sourceDir)} {QuoteBat(targetDir)} {QuoteBat(fileName)}");
    }

    private static string QuoteBat(string path) => "\"" + path.Replace("\"", string.Empty) + "\"";

    private static string EscapeBat(string value) => value.Replace("\"", "\"\"");

    private static string EscapeComment(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ');
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(ch switch
            {
                '\u2014' or '\u2013' => '-',
                <= '\u007F' => ch,
                'ä' => 'a',
                'ö' => 'o',
                'ü' => 'u',
                'Ä' => 'A',
                'Ö' => 'O',
                'Ü' => 'U',
                'ß' => 's',
                _ => '?'
            });
        }

        return builder.ToString();
    }

    private sealed record EmbeddedLaunchContext(string? ExecutablePath, string DataRoot, bool IsPortable);
}
