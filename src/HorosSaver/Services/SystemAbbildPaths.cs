using System.Globalization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed record SystemAbbildTargetDrive(
    string TargetPath,
    string Label,
    bool IsNtfs,
    bool IsFixed);

public static class SystemAbbildPaths
{
    public const string SystemBundleFolderName = "_system-bundle";
    public const string SystemBundleKind = "system-bundle";
    public const string WindowsImageBackupFolderName = "WindowsImageBackup";

    public static SystemAbbildMode NormalizeMode(SystemAbbildMode mode)
        => mode is SystemAbbildMode.WindowsSystemImage
            or SystemAbbildMode.AllProgramsBundle
            or SystemAbbildMode.AllVolumes
            ? mode
            : SystemAbbildMode.AllProgramsBundle;

    public static bool RequiresAdminElevation(SystemAbbildMode mode)
        => NormalizeMode(mode) is SystemAbbildMode.WindowsSystemImage or SystemAbbildMode.AllVolumes;

    public static bool RequiresTargetPath(SystemAbbildMode mode)
        => RequiresAdminElevation(mode);

    public static string GetLevelLabel(SystemAbbildMode mode) => NormalizeMode(mode) switch
    {
        SystemAbbildMode.WindowsSystemImage => "1 — Windows-Systemabbild",
        SystemAbbildMode.AllVolumes => "3 — Alle Festplattenvolumes",
        _ => "2 — Alle Programme (Standard)"
    };

    public static string GetLevelDescription(SystemAbbildMode mode) => NormalizeMode(mode) switch
    {
        SystemAbbildMode.WindowsSystemImage =>
            "Windows-Systemabbild via wbadmin -allCritical. Ziel-Laufwerk in den Einstellungen wählen. Administratorrechte (UAC) nötig.",
        SystemAbbildMode.AllVolumes =>
            "Sichert alle lokalen festen NTFS-Volumes (außer Ziel) via wbadmin. Ziel-Laufwerk erforderlich. Administratorrechte (UAC) nötig.",
        _ =>
            "Erstellt ein Bundle aller Programme: je Profil ein Snapshot unter data\\snapshots\\_system-bundle\\{id}\\. " +
            "Ziel optional (Standard: data\\snapshots)."
    };

    public static string GetAdminHint(SystemAbbildMode mode)
        => RequiresAdminElevation(mode)
            ? "Modus 1 und 3 starten wbadmin mit UAC-Elevation. HorosSaver selbst läuft ohne Admin."
            : string.Empty;

    public static string GetRestoreHint(SystemAbbildMode mode) => NormalizeMode(mode) switch
    {
        SystemAbbildMode.WindowsSystemImage or SystemAbbildMode.AllVolumes =>
            "Wiederherstellung nur über Windows-Wiederherstellungsumgebung (WinRE) bzw. wbadmin recover — " +
            "kein Datei-Restore in HorosSaver.",
        _ =>
            "Programm-Bundle: HorosSaver stellt alle Snapshots aus dem Manifest nacheinander wieder her."
    };

    public static string GetSystemBundlesRoot(IStoragePathResolver paths)
        => Path.Combine(paths.SnapshotsRoot, SystemBundleFolderName);

    public static string GetSystemBundleDirectory(IStoragePathResolver paths, string bundleId)
        => Path.Combine(GetSystemBundlesRoot(paths), bundleId);

    public static IReadOnlyList<SystemAbbildTargetDrive> EnumerateTargetDrives()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var drives = new List<SystemAbbildTargetDrive>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady)
            {
                continue;
            }

            var isNtfs = string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
            var targetPath = drive.Name.TrimEnd('\\');
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            var formatLabel = string.IsNullOrWhiteSpace(drive.DriveFormat) ? "unbekannt" : drive.DriveFormat;
            var volumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? string.Empty : $" \"{drive.VolumeLabel}\"";
            var label = isNtfs
                ? $"{targetPath}{volumeLabel} (frei {freeGb.ToString("F0", CultureInfo.InvariantCulture)} GB, NTFS)"
                : $"{targetPath}{volumeLabel} (frei {freeGb.ToString("F0", CultureInfo.InvariantCulture)} GB, {formatLabel})";

            drives.Add(new SystemAbbildTargetDrive(targetPath, label, isNtfs, drive.DriveType == DriveType.Fixed));
        }

        return drives
            .OrderByDescending(drive => drive.IsNtfs && drive.IsFixed)
            .ThenByDescending(drive => drive.IsNtfs)
            .ThenBy(drive => drive.TargetPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetWindowsBackupTargetHint(string? targetPath)
    {
        var computerName = Environment.MachineName;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return "WindowsImageBackup wird auf dem gewählten Laufwerk angelegt " +
                   $"(Ordner: X:\\{WindowsImageBackupFolderName}\\{computerName}\\).";
        }

        var driveRoot = GetDriveRoot(targetPath);
        return "WindowsImageBackup wird auf dem gewählten Laufwerk angelegt " +
               $"(Ordner: {driveRoot}\\{WindowsImageBackupFolderName}\\{computerName}\\).";
    }

    public static string GetDriveRoot(string targetPath)
    {
        var trimmed = targetPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(trimmed));
            return string.IsNullOrWhiteSpace(root) ? trimmed.TrimEnd('\\') : root.TrimEnd('\\');
        }
        catch
        {
            return trimmed.TrimEnd('\\');
        }
    }

    public static bool IsDriveRootTarget(string? configuredTarget, string driveTargetPath)
    {
        if (string.IsNullOrWhiteSpace(configuredTarget))
        {
            return false;
        }

        return string.Equals(
            GetDriveRoot(configuredTarget),
            GetDriveRoot(driveTargetPath),
            StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                configuredTarget.Trim().TrimEnd('\\'),
                GetDriveRoot(configuredTarget),
                StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveTargetPath(IStoragePathResolver paths, string? configuredTarget, SystemAbbildMode mode)
    {
        var normalizedMode = NormalizeMode(mode);
        if (!RequiresTargetPath(normalizedMode))
        {
            return string.IsNullOrWhiteSpace(configuredTarget)
                ? paths.SnapshotsRoot
                : configuredTarget.Trim();
        }

        if (string.IsNullOrWhiteSpace(configuredTarget))
        {
            throw new InvalidOperationException("Für diesen Modus ist ein Ziel-Laufwerk erforderlich (Einstellungen → System-Abbild).");
        }

        return configuredTarget.Trim();
    }
}
