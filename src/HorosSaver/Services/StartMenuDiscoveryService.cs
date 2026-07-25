using System.Diagnostics;
using HorosSaver.Models;

namespace HorosSaver.Services;

internal sealed class StartMenuDiscoveryService
{
    private static readonly string[] NoiseNameFragments =
    [
        "uninstall",
        "deinstall",
        "readme",
        "help",
        "documentation",
        "website",
        "check for updates",
        "update",
        "repair",
        "maintenance"
    ];

    public IReadOnlyList<DiscoveredProgram> Discover(CancellationToken cancellationToken = default)
    {
        var roots = new[]
        {
            (Path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"), Scope: "startmenu-allusers"),
            (Path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), Scope: "startmenu-user")
        };

        var results = new List<DiscoveredProgram>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, scope) in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var shortcutPath in Directory.EnumerateFiles(path, "*.lnk", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var program = TryParseShortcut(shortcutPath, scope);
                if (program is null)
                {
                    continue;
                }

                var dedupeKey = program.TargetPath ?? shortcutPath;
                if (!seenTargets.Add(dedupeKey))
                {
                    continue;
                }

                results.Add(program);
            }
        }

        return results
            .Where(program => !IsNoise(program.DisplayName))
            .OrderBy(program => program.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DiscoveredProgram? TryParseShortcut(string shortcutPath, string scope)
    {
        if (!WindowsShortcutResolver.TryResolve(shortcutPath, out var shortcut))
        {
            return null;
        }

        var displayName = Path.GetFileNameWithoutExtension(shortcutPath).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        if (!File.Exists(shortcut.TargetPath) && !Directory.Exists(shortcut.TargetPath))
        {
            return null;
        }

        if (Path.GetExtension(shortcut.TargetPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) is false
            && !Directory.Exists(shortcut.TargetPath))
        {
            return null;
        }

        var installLocation = Directory.Exists(shortcut.TargetPath)
            ? shortcut.TargetPath
            : Path.GetDirectoryName(shortcut.TargetPath);

        var fileVersion = File.Exists(shortcut.TargetPath)
            ? TryReadFileVersion(shortcut.TargetPath)
            : null;

        return new DiscoveredProgram
        {
            DisplayName = displayName,
            DisplayVersion = fileVersion?.FileVersion,
            Publisher = fileVersion?.CompanyName,
            InstallLocation = NormalizePath(installLocation),
            Scope = scope,
            Sources = ProgramDiscoverySource.StartMenu,
            TargetPath = shortcut.TargetPath,
            ShortcutPath = shortcutPath
        };
    }

    private static FileVersionInfo? TryReadFileVersion(string targetPath)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(targetPath);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsNoise(string displayName)
    {
        if (displayName.Length < 2)
        {
            return true;
        }

        var normalized = displayName.ToLowerInvariant();
        return NoiseNameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().Trim('"');
        return trimmed.EndsWith('\\') ? trimmed.TrimEnd('\\') : trimmed;
    }
}
