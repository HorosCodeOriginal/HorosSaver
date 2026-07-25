using System.Security.AccessControl;
using System.Security.Principal;

namespace HorosSaver.Services;

internal readonly record struct AclOperationResult(bool Success, string? Sddl, string? Warning)
{
    public static AclOperationResult Captured(string sddl) => new(true, sddl, null);

    public static AclOperationResult Failed(string warning) => new(false, null, warning);

    public static AclOperationResult Skipped(string reason) => new(false, null, reason);
}

internal static class FileAclService
{
    public const string SidecarSuffix = ".acl.sddl";
    public const string DirectorySidecarSuffix = ".dir.acl.sddl";

    public static bool IsSupported => OperatingSystem.IsWindows();

    public static bool IsReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static AclOperationResult TryCaptureAcl(string sourcePath, bool isDirectory)
    {
        if (!IsSupported)
        {
            return AclOperationResult.Skipped("ACLs werden nur unter Windows unterstützt.");
        }

        if (IsReparsePoint(sourcePath))
        {
            return AclOperationResult.Skipped($"Reparse-Punkt übersprungen: {sourcePath}");
        }

        try
        {
            var sddl = isDirectory
                ? new DirectoryInfo(sourcePath).GetAccessControl(AccessControlSections.All)
                    .GetSecurityDescriptorSddlForm(AccessControlSections.All)
                : new FileInfo(sourcePath).GetAccessControl(AccessControlSections.All)
                    .GetSecurityDescriptorSddlForm(AccessControlSections.All);

            if (string.IsNullOrWhiteSpace(sddl))
            {
                return AclOperationResult.Failed($"Leere ACL für {sourcePath}");
            }

            return AclOperationResult.Captured(sddl);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AclOperationResult.Failed($"ACL nicht lesbar ({sourcePath}): {ex.Message}");
        }
        catch (IOException ex)
        {
            return AclOperationResult.Failed($"ACL nicht lesbar ({sourcePath}): {ex.Message}");
        }
        catch (PlatformNotSupportedException ex)
        {
            return AclOperationResult.Failed($"ACL nicht unterstützt: {ex.Message}");
        }
    }

    public static AclOperationResult TryApplyAcl(string targetPath, string sddl, bool isDirectory)
    {
        if (!IsSupported)
        {
            return AclOperationResult.Skipped("ACLs werden nur unter Windows unterstützt.");
        }

        if (IsReparsePoint(targetPath))
        {
            return AclOperationResult.Skipped($"Reparse-Punkt übersprungen: {targetPath}");
        }

        var direct = TryApplyAclDirect(targetPath, sddl, isDirectory);
        if (direct.Success)
        {
            return direct;
        }

        return TryRobocopyAclBoost(targetPath, sddl, isDirectory, direct.Warning ?? "Unbekannter Fehler");
    }

    internal static AclOperationResult TryApplyAclDirect(string targetPath, string sddl, bool isDirectory)
    {
        if (isDirectory)
        {
            if (!Directory.Exists(targetPath))
            {
                return AclOperationResult.Failed($"Zielordner fehlt: {targetPath}");
            }
        }
        else if (!File.Exists(targetPath))
        {
            return AclOperationResult.Failed($"Zieldatei fehlt: {targetPath}");
        }

        // DACL-only first — owner/group SIDs from another machine often fail on fresh systems.
        var daclResult = TryApplySecuritySections(targetPath, sddl, isDirectory, AccessControlSections.Access);
        if (daclResult.Success)
        {
            return daclResult;
        }

        var fullResult = TryApplySecuritySections(targetPath, sddl, isDirectory, AccessControlSections.All);
        if (fullResult.Success)
        {
            return fullResult;
        }

        return AclOperationResult.Failed(daclResult.Warning ?? fullResult.Warning ?? "Unbekannter ACL-Fehler");
    }

    private static AclOperationResult TryApplySecuritySections(
        string targetPath,
        string sddl,
        bool isDirectory,
        AccessControlSections sections)
    {
        try
        {
            if (isDirectory)
            {
                var security = new DirectorySecurity();
                security.SetSecurityDescriptorSddlForm(sddl, sections);
                new DirectoryInfo(targetPath).SetAccessControl(security);
            }
            else
            {
                var security = new FileSecurity();
                security.SetSecurityDescriptorSddlForm(sddl, sections);
                new FileInfo(targetPath).SetAccessControl(security);
            }

            return AclOperationResult.Captured(sddl);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (IOException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (IdentityNotMappedException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (System.Security.SecurityException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (PlatformNotSupportedException ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            return AclOperationResult.Failed(ex.Message);
        }
    }

    private static AclOperationResult TryRobocopyAclBoost(
        string targetPath,
        string sddl,
        bool isDirectory,
        string primaryError)
    {
        if (!RobocopyAclBoost.IsAvailable)
        {
            return AclOperationResult.Failed(
                $"ACL nicht schreibbar ({targetPath}): {primaryError} (robocopy nicht verfügbar)");
        }

        var tempSource = RobocopyAclBoost.TryMaterializeTempWithAcl(targetPath, sddl, isDirectory);
        if (tempSource is null)
        {
            return AclOperationResult.Failed(
                $"ACL nicht schreibbar ({targetPath}): {primaryError} (robocopy-Boost fehlgeschlagen)");
        }

        try
        {
            if (RobocopyAclBoost.TryCopySecurityFrom(tempSource, targetPath, isDirectory))
            {
                return AclOperationResult.Captured(sddl);
            }
        }
        finally
        {
            RobocopyAclBoost.CleanupTemp(tempSource, isDirectory);
        }

        return AclOperationResult.Failed(
            $"ACL nicht schreibbar ({targetPath}): {primaryError} (robocopy /COPY:SOU fehlgeschlagen)");
    }
}

internal static class AclSidecarStore
{
    public static string GetFileSidecarPath(string snapshotDir, string snapshotRelative, bool storedCompressed)
    {
        var relative = snapshotRelative.Replace('/', Path.DirectorySeparatorChar);
        if (storedCompressed)
        {
            relative += ".gz";
        }

        return Path.Combine(snapshotDir, relative + FileAclService.SidecarSuffix);
    }

    public static string GetDirectorySidecarPath(string snapshotDir, string snapshotRelative)
    {
        var relative = snapshotRelative.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(snapshotDir, relative + FileAclService.DirectorySidecarSuffix);
    }

    public static void WriteSidecar(string sidecarPath, string sddl)
    {
        var directory = Path.GetDirectoryName(sidecarPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(sidecarPath, sddl);
    }

    public static string? TryReadSidecar(string sidecarPath)
    {
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        try
        {
            var sddl = File.ReadAllText(sidecarPath).Trim();
            return string.IsNullOrWhiteSpace(sddl) ? null : sddl;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void CaptureToSidecar(
        string sourcePath,
        string snapshotDir,
        string snapshotRelative,
        bool isDirectory,
        bool storedCompressed,
        ICollection<string> warnings)
    {
        var result = FileAclService.TryCaptureAcl(sourcePath, isDirectory);
        if (result.Success && result.Sddl is not null)
        {
            var sidecarPath = isDirectory
                ? GetDirectorySidecarPath(snapshotDir, snapshotRelative)
                : GetFileSidecarPath(snapshotDir, snapshotRelative, storedCompressed);
            WriteSidecar(sidecarPath, result.Sddl);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            warnings.Add(result.Warning);
        }
    }

    public static void ApplyFromSidecar(
        string targetPath,
        string snapshotDir,
        string snapshotRelative,
        bool isDirectory,
        bool storedCompressed,
        ICollection<string> warnings)
    {
        var sidecarPath = isDirectory
            ? GetDirectorySidecarPath(snapshotDir, snapshotRelative)
            : GetFileSidecarPath(snapshotDir, snapshotRelative, storedCompressed);

        var sddl = TryReadSidecar(sidecarPath);
        if (sddl is null)
        {
            return;
        }

        try
        {
            var result = FileAclService.TryApplyAcl(targetPath, sddl, isDirectory);
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Warning))
            {
                warnings.Add(FormatAclWarning(targetPath, result.Warning));
            }
        }
        catch (Exception ex)
        {
            warnings.Add(FormatAclWarning(targetPath, ex.Message));
        }
    }

    private static string FormatAclWarning(string targetPath, string detail)
        => $"ACL/Owner für {targetPath} übersprungen: {detail}";
}

internal static class RobocopyAclBoost
{
    public static bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return File.Exists(Path.Combine(system32, "robocopy.exe"));
        }
    }

    public static bool TryCopySecurityFrom(string sourcePath, string targetPath, bool isDirectory)
    {
        if (!IsAvailable)
        {
            return false;
        }

        var source = isDirectory ? sourcePath : Path.GetDirectoryName(sourcePath) ?? sourcePath;
        var target = isDirectory ? targetPath : Path.GetDirectoryName(targetPath) ?? targetPath;
        var fileName = isDirectory ? "*" : Path.GetFileName(targetPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "robocopy.exe"),
                Arguments = $"\"{source}\" \"{target}\" {fileName} /COPY:SOU /IS /IT /NFL /NDL /NJH /NJS /NC /NS /NP",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode is >= 0 and <= 7;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static string? TryMaterializeTempWithAcl(string targetPath, string sddl, bool isDirectory)
    {
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "horossaver-acl-" + Guid.NewGuid().ToString("N")[..8]);
            if (isDirectory)
            {
                Directory.CreateDirectory(tempRoot);
                var apply = FileAclService.TryApplyAclDirect(tempRoot, sddl, isDirectory: true);
                return apply.Success ? tempRoot : null;
            }

            Directory.CreateDirectory(tempRoot);
            var tempFile = Path.Combine(tempRoot, Path.GetFileName(targetPath));
            File.WriteAllBytes(tempFile, File.Exists(targetPath) ? File.ReadAllBytes(targetPath) : []);
            var fileApply = FileAclService.TryApplyAclDirect(tempFile, sddl, isDirectory: false);
            return fileApply.Success ? tempFile : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void CleanupTemp(string tempPath, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
            else if (File.Exists(tempPath))
            {
                var parent = Path.GetDirectoryName(tempPath);
                File.Delete(tempPath);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    Directory.Delete(parent, recursive: true);
                }
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
