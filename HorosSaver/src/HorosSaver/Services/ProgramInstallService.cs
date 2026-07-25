using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IProgramInstallService
{
    bool IsProgramInstalled(ProgramProfile profile, ProgramInstallMetadata? snapshotMetadata = null);

    Task<ProgramInstallResult> TryInstallAsync(
        ProgramProfile profile,
        ProgramInstallMetadata? snapshotMetadata = null,
        IProgress<string>? logProgress = null,
        bool forceReinstall = false,
        CancellationToken cancellationToken = default);
}

public sealed class ProgramInstallResult
{
    public bool Attempted { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> LogLines { get; init; } = [];
    public string? WingetId { get; init; }
}

public sealed class ProgramInstallService : IProgramInstallService
{
    public bool IsProgramInstalled(ProgramProfile profile, ProgramInstallMetadata? snapshotMetadata = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        var installLocation = snapshotMetadata?.InstallLocation ?? profile.InstallLocation;
        if (IsInstallLocationPresent(installLocation, profile.Name))
        {
            return true;
        }

        var wingetId = ResolveWingetId(profile, snapshotMetadata);
        if (!string.IsNullOrWhiteSpace(wingetId) && IsWingetPackageInstalled(wingetId))
        {
            return true;
        }

        return false;
    }

    public async Task<ProgramInstallResult> TryInstallAsync(
        ProgramProfile profile,
        ProgramInstallMetadata? snapshotMetadata = null,
        IProgress<string>? logProgress = null,
        bool forceReinstall = false,
        CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        void Log(string line)
        {
            log.Add(line);
            logProgress?.Report(line);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProgramInstallResult
            {
                Attempted = false,
                Success = true,
                Message = "Programm-Installation nur unter Windows unterstützt — übersprungen.",
                LogLines = log
            };
        }

        var alreadyInstalled = IsProgramInstalled(profile, snapshotMetadata);
        if (alreadyInstalled && !forceReinstall)
        {
            Log($"Programm „{profile.Name}“ ist bereits installiert — Reinstall übersprungen.");
            return new ProgramInstallResult
            {
                Attempted = false,
                Success = true,
                Message = $"„{profile.Name}“ ist bereits installiert — Reinstall übersprungen.",
                LogLines = log
            };
        }

        if (alreadyInstalled && forceReinstall)
        {
            Log($"Erzwinge Neuinstallation von „{profile.Name}“ (winget --force) …");
        }

        var wingetId = ResolveWingetId(profile, snapshotMetadata);
        if (string.IsNullOrWhiteSpace(wingetId))
        {
            Log($"Keine winget-ID für „{profile.Name}“ bekannt — nur Einstellungen werden wiederhergestellt.");
            return new ProgramInstallResult
            {
                Attempted = false,
                Success = false,
                Message = $"Keine winget-ID für „{profile.Name}“ — Programm muss manuell installiert werden.",
                LogLines = log
            };
        }

        var wingetExe = ResolveWingetExecutable();
        if (wingetExe is null)
        {
            Log("winget nicht gefunden — bitte App Installer / Microsoft Store installieren.");
            return new ProgramInstallResult
            {
                Attempted = true,
                Success = false,
                WingetId = wingetId,
                Message = "winget nicht verfügbar — Programm konnte nicht automatisch installiert werden.",
                LogLines = log
            };
        }

        var useForce = forceReinstall && alreadyInstalled;
        Log(useForce
            ? $"Starte winget install --id {wingetId} --force …"
            : $"Starte winget install --id {wingetId} …");
        var snapshotVersion = snapshotMetadata?.DisplayVersion ?? profile.InstalledVersion;
        if (!string.IsNullOrWhiteSpace(snapshotVersion))
        {
            Log($"Snapshot-Version: {snapshotVersion} (winget installiert die aktuell verfügbare Version).");
        }

        var result = await RunWingetInstallAsync(wingetExe, wingetId, useForce, Log, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Log($"winget beendet mit Exit-Code {result.ExitCode}.");
            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Log(result.StandardError);
            }

            return new ProgramInstallResult
            {
                Attempted = true,
                Success = false,
                WingetId = wingetId,
                Message = $"Programm-Installation fehlgeschlagen (winget Exit {result.ExitCode}). Einstellungen werden trotzdem wiederhergestellt.",
                LogLines = log
            };
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Log(line.Trim());
            }
        }

        var installedAfter = IsProgramInstalled(profile, snapshotMetadata);
        if (!installedAfter)
        {
            Log("winget meldete Erfolg, aber Programm ist in Registry/Pfad noch nicht sichtbar — ggf. Neustart nötig.");
        }

        return new ProgramInstallResult
        {
            Attempted = true,
            Success = installedAfter,
            WingetId = wingetId,
            Message = installedAfter
                ? useForce
                    ? $"„{profile.Name}“ wurde über winget neu installiert."
                    : $"„{profile.Name}“ wurde über winget installiert."
                : $"winget install abgeschlossen, Installation konnte nicht verifiziert werden.",
            LogLines = log
        };
    }

    private static string? ResolveWingetId(ProgramProfile profile, ProgramInstallMetadata? snapshotMetadata)
    {
        if (!string.IsNullOrWhiteSpace(snapshotMetadata?.WingetId))
        {
            return snapshotMetadata.WingetId;
        }

        if (!string.IsNullOrWhiteSpace(profile.WingetId))
        {
            return profile.WingetId;
        }

        KnownWingetIds.EnrichProfile(profile);
        return profile.WingetId;
    }

    private static bool IsInstallLocationPresent(string? installLocation, string programName)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
        {
            return false;
        }

        var exeCandidates = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.AllDirectories).Take(20))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var nameToken = NormalizeName(programName).Replace(" ", string.Empty);
        foreach (var exe in exeCandidates)
        {
            var fileName = Path.GetFileNameWithoutExtension(exe);
            if (fileName.Contains(nameToken, StringComparison.OrdinalIgnoreCase)
                || nameToken.Contains(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.AllDirectories).Any();
    }

    private static async Task<ProcessExecutionResult> RunWingetInstallAsync(
        string wingetExe,
        string wingetId,
        bool force,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = wingetExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add(wingetId);
        startInfo.ArgumentList.Add("--accept-package-agreements");
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--disable-interactivity");
        if (force)
        {
            startInfo.ArgumentList.Add("--force");
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                stdout.AppendLine(args.Data);
                log(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                stderr.AppendLine(args.Data);
                log($"[stderr] {args.Data}");
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("winget-Prozess konnte nicht gestartet werden.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessExecutionResult(
            process.ExitCode,
            stdout.ToString().TrimEnd(),
            stderr.ToString().TrimEnd());
    }

    public static string? ResolveWingetExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", "winget.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathCandidate = FindOnPath("winget.exe");
        return pathCandidate;
    }

    private static string? FindOnPath(string executable)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var folder in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsWingetPackageInstalled(string wingetId)
    {
        var wingetExe = ResolveWingetExecutable();
        if (wingetExe is null)
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = wingetExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("list");
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(wingetId);
            startInfo.ArgumentList.Add("--accept-source-agreements");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);
            return process.ExitCode == 0 && output.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string NormalizeName(string value)
        => value.Trim().ToLowerInvariant();
}
