namespace HorosSaver.Services;

public sealed class AppStorageLayout
{
    public required string AppDirectory { get; init; }
    public required string DataRoot { get; init; }
    public required string LogsRoot { get; init; }
    public required bool IsPortable { get; init; }
}

public static class AppStoragePaths
{
    public const string PortableEnvironmentVariable = "HOROSSAVER_PORTABLE";
    public const string DataRootEnvironmentVariable = "HOROSSAVER_DATA_ROOT";

    private static readonly string[] PortableMarkerFiles = ["portable.txt", "HorosSaver.portable"];

    public static AppStorageLayout Resolve()
    {
        var appDirectory = ResolveAppDirectory();

        var explicitDataRoot = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitDataRoot))
        {
            return CreatePortableLayout(appDirectory, Path.GetFullPath(explicitDataRoot.Trim()));
        }

        if (IsPortableMode(appDirectory))
        {
            return CreatePortableLayout(appDirectory, Path.Combine(appDirectory, "data"));
        }

        var localDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HorosCode",
            "HorosSaver");

        return new AppStorageLayout
        {
            AppDirectory = appDirectory,
            DataRoot = localDataRoot,
            LogsRoot = Path.Combine(localDataRoot, "logs"),
            IsPortable = false
        };
    }

    public static string GetLegacyLocalAppDataRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HorosCode",
            "HorosSaver");

    private static AppStorageLayout CreatePortableLayout(string appDirectory, string dataRoot)
        => new()
        {
            AppDirectory = appDirectory,
            DataRoot = dataRoot,
            LogsRoot = Path.Combine(appDirectory, "logs"),
            IsPortable = true
        };

    private static bool IsPortableMode(string appDirectory)
    {
        if (IsTruthy(Environment.GetEnvironmentVariable(PortableEnvironmentVariable)))
        {
            return true;
        }

        foreach (var marker in PortableMarkerFiles)
        {
            if (File.Exists(Path.Combine(appDirectory, marker)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim() is "1" or "true" or "yes" or "on"
            || string.Equals(value.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveAppDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return Path.GetFullPath(baseDirectory);
    }
}
