namespace HorosSaver.Services;

internal static class RuntimeEnvironmentLabels
{
    public static string ShellLabel => ResolveShellLabel();

    public static string OsLabel => ResolveOsLabel();

    private static string ResolveShellLabel()
    {
        var psHome = Environment.GetEnvironmentVariable("PSHOME");
        if (!string.IsNullOrWhiteSpace(psHome))
        {
            var pwshExe = Path.Combine(psHome, "pwsh.exe");
            if (File.Exists(pwshExe))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(pwshExe);
                if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
                {
                    var parts = versionInfo.ProductVersion.Split('.', '+');
                    if (parts.Length >= 2
                        && int.TryParse(parts[0], out var major)
                        && int.TryParse(parts[1], out var minor))
                    {
                        return $"PowerShell {major}.{minor}";
                    }
                }
            }
        }

        var runtimeVersion = Environment.Version;
        return $"PowerShell {runtimeVersion.Major}.{runtimeVersion.Minor}";
    }

    private static string ResolveOsLabel()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        return Environment.OSVersion.VersionString;
    }
}
