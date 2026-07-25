namespace HorosSaver.Services;

internal static class HorosSaverExeLocator
{
    public static string? ResolveCurrentExecutable()
    {
        if (IsValidHorosSaverExe(Environment.ProcessPath))
        {
            return Path.GetFullPath(Environment.ProcessPath);
        }

        var baseDirectoryExe = Path.Combine(AppContext.BaseDirectory, "HorosSaver.exe");
        if (File.Exists(baseDirectoryExe))
        {
            return Path.GetFullPath(baseDirectoryExe);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "HorosSaver.exe");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static bool IsValidHorosSaverExe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(path), "HorosSaver.exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(path);
    }
}
