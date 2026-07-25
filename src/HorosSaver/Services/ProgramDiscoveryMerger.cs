using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class ProgramDiscoveryMerger
{
    public static List<DiscoveredProgram> Merge(
        IEnumerable<DiscoveredProgram> registryPrograms,
        IEnumerable<DiscoveredProgram> startMenuPrograms)
    {
        var merged = registryPrograms
            .Select(program => EnsureSource(program, ProgramDiscoverySource.Registry))
            .ToList();

        foreach (var startMenuProgram in startMenuPrograms)
        {
            var existingIndex = merged.FindIndex(existing => AreDuplicates(existing, startMenuProgram));
            if (existingIndex >= 0)
            {
                merged[existingIndex] = Combine(merged[existingIndex], startMenuProgram);
                continue;
            }

            merged.Add(EnsureSource(startMenuProgram, ProgramDiscoverySource.StartMenu));
        }

        return merged
            .OrderBy(program => program.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DiscoveredProgram Combine(DiscoveredProgram registryProgram, DiscoveredProgram startMenuProgram)
        => new()
        {
            DisplayName = registryProgram.DisplayName,
            DisplayVersion = registryProgram.DisplayVersion ?? startMenuProgram.DisplayVersion,
            Publisher = registryProgram.Publisher ?? startMenuProgram.Publisher,
            InstallLocation = registryProgram.InstallLocation ?? startMenuProgram.InstallLocation,
            RegistryKeyName = registryProgram.RegistryKeyName,
            Scope = registryProgram.Scope,
            Sources = registryProgram.Sources | ProgramDiscoverySource.StartMenu,
            TargetPath = startMenuProgram.TargetPath ?? registryProgram.TargetPath,
            ShortcutPath = startMenuProgram.ShortcutPath ?? registryProgram.ShortcutPath
        };

    private static DiscoveredProgram EnsureSource(DiscoveredProgram program, ProgramDiscoverySource source)
        => new()
        {
            DisplayName = program.DisplayName,
            DisplayVersion = program.DisplayVersion,
            Publisher = program.Publisher,
            InstallLocation = program.InstallLocation,
            RegistryKeyName = program.RegistryKeyName,
            Scope = program.Scope,
            Sources = program.Sources | source,
            TargetPath = program.TargetPath,
            ShortcutPath = program.ShortcutPath
        };

    private static bool AreDuplicates(DiscoveredProgram left, DiscoveredProgram right)
    {
        if (NormalizeName(left.DisplayName) == NormalizeName(right.DisplayName))
        {
            return true;
        }

        var leftTarget = NormalizePath(left.TargetPath);
        var rightTarget = NormalizePath(right.TargetPath);
        if (leftTarget is not null
            && rightTarget is not null
            && string.Equals(leftTarget, rightTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leftInstall = NormalizePath(left.InstallLocation);
        var rightInstall = NormalizePath(right.InstallLocation);
        if (leftInstall is not null
            && rightInstall is not null
            && string.Equals(leftInstall, rightInstall, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (leftInstall is not null && rightTarget is not null)
        {
            var rightTargetDirectory = Path.GetDirectoryName(rightTarget);
            if (!string.IsNullOrWhiteSpace(rightTargetDirectory)
                && string.Equals(leftInstall, rightTargetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (rightInstall is not null && leftTarget is not null)
        {
            var leftTargetDirectory = Path.GetDirectoryName(leftTarget);
            if (!string.IsNullOrWhiteSpace(leftTargetDirectory)
                && string.Equals(rightInstall, leftTargetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string value)
        => value.Trim().ToLowerInvariant();

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().TrimEnd('\\', '/');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
