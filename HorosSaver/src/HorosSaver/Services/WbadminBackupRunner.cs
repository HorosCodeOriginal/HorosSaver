using System.Diagnostics;
using System.Text;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class WbadminBackupRunner
{
    public WbadminBackupRunner(IStoragePathResolver paths)
    {
        _paths = paths;
    }

    private readonly IStoragePathResolver _paths;

    public async Task<SystemImageOperationResult> RunAsync(
        SystemAbbildMode mode,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Failure(mode, "wbadmin ist nur unter Windows verfügbar.");
        }

        var normalizedMode = SystemAbbildPaths.NormalizeMode(mode);
        if (normalizedMode is not (SystemAbbildMode.WindowsSystemImage or SystemAbbildMode.AllVolumes))
        {
            return Failure(mode, "WbadminBackupRunner unterstützt nur Modus 1 und 3.");
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Failure(mode, "Ziel-Laufwerk fehlt — bitte unter Einstellungen → System-Abbild festlegen.");
        }

        _paths.EnsureDataDirectories();
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var logFilePath = Path.Combine(_paths.LogsRoot, $"system-abbild-{timestamp}.log");
        var arguments = BuildArguments(normalizedMode, targetPath);
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine($"HorosSaver System-Abbild — Modus {(int)normalizedMode}");
        logBuilder.AppendLine($"Gestartet: {DateTimeOffset.Now:O}");
        logBuilder.AppendLine($"Ziel: {targetPath}");
        logBuilder.AppendLine($"Befehl: wbadmin {arguments}");
        logBuilder.AppendLine();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wbadmin.exe",
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                logBuilder.AppendLine("Fehler: wbadmin-Prozess konnte nicht gestartet werden (UAC abgebrochen?).");
                await File.WriteAllTextAsync(logFilePath, logBuilder.ToString(), cancellationToken).ConfigureAwait(false);
                return new SystemImageOperationResult
                {
                    Success = false,
                    Mode = normalizedMode,
                    Message = "wbadmin wurde nicht gestartet — UAC abgebrochen oder Prozessstart fehlgeschlagen.",
                    LogFilePath = logFilePath,
                    Errors = ["Prozessstart fehlgeschlagen"]
                };
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            logBuilder.AppendLine($"Exit-Code: {process.ExitCode}");

            await File.WriteAllTextAsync(logFilePath, logBuilder.ToString(), cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                return new SystemImageOperationResult
                {
                    Success = true,
                    Mode = normalizedMode,
                    Message = $"Windows-Backup gestartet/abgeschlossen (Modus {(int)normalizedMode}). Log: {logFilePath}",
                    LogFilePath = logFilePath
                };
            }

            return new SystemImageOperationResult
            {
                Success = false,
                Mode = normalizedMode,
                Message = $"wbadmin beendet mit Exit-Code {process.ExitCode}. Details: {logFilePath}",
                LogFilePath = logFilePath,
                Errors = [$"wbadmin Exit-Code {process.ExitCode}"]
            };
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Ausnahme: {ex.Message}");
            await File.WriteAllTextAsync(logFilePath, logBuilder.ToString(), cancellationToken).ConfigureAwait(false);
            return new SystemImageOperationResult
            {
                Success = false,
                Mode = normalizedMode,
                Message = $"wbadmin-Fehler: {ex.Message}",
                LogFilePath = logFilePath,
                Errors = [ex.Message]
            };
        }
    }

    private static string BuildArguments(SystemAbbildMode mode, string targetPath)
    {
        var normalizedTarget = NormalizeBackupTarget(targetPath);
        return mode switch
        {
            SystemAbbildMode.WindowsSystemImage =>
                $"start backup -backupTarget:{normalizedTarget} -allCritical -quiet",
            SystemAbbildMode.AllVolumes =>
                $"start backup -backupTarget:{normalizedTarget} -include:{BuildIncludeList(normalizedTarget)} -quiet",
            _ => throw new InvalidOperationException($"Modus {mode} wird von wbadmin nicht unterstützt.")
        };
    }

    private static string BuildIncludeList(string targetPath)
    {
        var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? string.Empty;
        var volumes = new List<string>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(drive.Name, targetRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            volumes.Add(drive.Name.TrimEnd('\\'));
        }

        if (volumes.Count == 0)
        {
            throw new InvalidOperationException("Keine NTFS-Festplattenvolumes für Modus 3 gefunden (außer Ziel).");
        }

        return string.Join(',', volumes);
    }

    private static string NormalizeBackupTarget(string targetPath)
    {
        var fullPath = Path.GetFullPath(targetPath);
        if (Directory.Exists(fullPath))
        {
            return fullPath.TrimEnd('\\');
        }

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) && root.Length <= 3)
        {
            return root.TrimEnd('\\');
        }

        return fullPath.TrimEnd('\\');
    }

    private static SystemImageOperationResult Failure(SystemAbbildMode mode, string message)
        => new()
        {
            Success = false,
            Mode = SystemAbbildPaths.NormalizeMode(mode),
            Message = message,
            Errors = [message]
        };
}
