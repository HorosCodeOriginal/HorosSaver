using HorosSaver.Models;

namespace HorosSaver.Services;

public static class RestorePathRemapper
{
    private sealed record RootMapping(string Category, string RootPath);

    public static string MapTargetPath(string sourcePath, RestoreOptions options, string? sourceProfileRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return sourcePath;
        }

        var normalizedSource = Path.GetFullPath(sourcePath);

        return options.Mode switch
        {
            RestoreTargetMode.OriginalPaths => normalizedSource,
            RestoreTargetMode.CustomRoot => MapToCustomRoot(normalizedSource, options.CustomRootPath),
            RestoreTargetMode.AlternateUserProfile => MapToAlternateProfile(
                normalizedSource,
                options.AlternateUserProfilePath,
                sourceProfileRoot),
            _ => normalizedSource
        };
    }

    public static IReadOnlyList<RestorePathPreview> BuildPreview(
        IEnumerable<CapturedItem> items,
        IReadOnlyCollection<string>? selectedRelativePaths,
        RestoreOptions options)
    {
        var sourceProfileRoot = DetectSourceProfileRoot(items);
        var previews = new List<RestorePathPreview>();

        foreach (var item in items.Where(captured => captured.Exists))
        {
            if (!IsSelected(item, selectedRelativePaths))
            {
                continue;
            }

            var targetPath = MapTargetPath(item.SourcePath, options, sourceProfileRoot);
            previews.Add(new RestorePathPreview
            {
                Label = item.Label,
                SourcePath = item.SourcePath,
                TargetPath = targetPath,
                RelativePath = item.SnapshotRelativePath.Replace('\\', '/'),
                TargetExists = TargetPathExists(item, targetPath),
                IsRemapped = !string.Equals(
                    Path.GetFullPath(item.SourcePath),
                    Path.GetFullPath(targetPath),
                    StringComparison.OrdinalIgnoreCase)
            });
        }

        return previews;
    }

    public static string? DetectSourceProfileRoot(IEnumerable<CapturedItem> items)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourcePath in items.Select(item => item.SourcePath))
        {
            var profileRoot = TryExtractUserProfileRoot(sourcePath);
            if (profileRoot is null)
            {
                continue;
            }

            counts.TryGetValue(profileRoot, out var count);
            counts[profileRoot] = count + 1;
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Key)
            .FirstOrDefault();
    }

    public static bool HasTargetConflicts(IEnumerable<RestorePathPreview> previews)
        => previews.Any(preview => preview.TargetExists);

    private static bool IsSelected(CapturedItem item, IReadOnlyCollection<string>? selectedRelativePaths)
    {
        if (selectedRelativePaths is null || selectedRelativePaths.Count == 0)
        {
            return true;
        }

        var normalized = item.SnapshotRelativePath.Replace('\\', '/');
        return selectedRelativePaths.Any(path =>
            string.Equals(path.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TargetPathExists(CapturedItem item, string targetPath)
    {
        if (item.IsDirectory)
        {
            if (item.Files.Count > 0)
            {
                return item.Files
                    .Where(file => file.Exists)
                    .Any(file =>
                    {
                        var fileTarget = Path.Combine(
                            targetPath,
                            file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        return File.Exists(fileTarget);
                    });
            }

            return Directory.Exists(targetPath)
                   && Directory.EnumerateFileSystemEntries(targetPath).Any();
        }

        return File.Exists(targetPath);
    }

    private static string MapToCustomRoot(string sourcePath, string? customRootPath)
    {
        if (string.IsNullOrWhiteSpace(customRootPath))
        {
            return sourcePath;
        }

        var root = Path.GetFullPath(customRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var mapping = FindLongestRootMapping(sourcePath);
        if (mapping is not null)
        {
            var relative = sourcePath[mapping.RootPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(relative)
                ? Path.Combine(root, mapping.Category)
                : Path.Combine(root, mapping.Category, relative);
        }

        if (Path.IsPathRooted(sourcePath) && sourcePath.Length >= 3 && sourcePath[1] == ':')
        {
            var drive = char.ToUpperInvariant(sourcePath[0]).ToString();
            var relative = sourcePath[2..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(root, "Drive", drive, relative);
        }

        return Path.Combine(root, "Other", sourcePath.Replace(':', '_'));
    }

    private static string MapToAlternateProfile(
        string sourcePath,
        string? alternateProfilePath,
        string? sourceProfileRoot)
    {
        if (string.IsNullOrWhiteSpace(alternateProfilePath))
        {
            return sourcePath;
        }

        var alternateRoot = Path.GetFullPath(
            alternateProfilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var profileRoot = sourceProfileRoot ?? TryExtractUserProfileRoot(sourcePath);
        if (profileRoot is null)
        {
            return sourcePath;
        }

        if (!sourcePath.StartsWith(profileRoot, StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        var relative = sourcePath[profileRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relative)
            ? alternateRoot
            : Path.Combine(alternateRoot, relative);
    }

    private static RootMapping? FindLongestRootMapping(string sourcePath)
    {
        RootMapping? best = null;

        foreach (var mapping in GetKnownRootMappings())
        {
            if (!sourcePath.StartsWith(mapping.RootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || mapping.RootPath.Length > best.RootPath.Length)
            {
                best = mapping;
            }
        }

        return best;
    }

    private static IEnumerable<RootMapping> GetKnownRootMappings()
    {
        yield return CreateMapping("UserProfile", Environment.SpecialFolder.UserProfile);
        yield return CreateMapping("AppData/Local", Environment.SpecialFolder.LocalApplicationData);
        yield return CreateMapping("AppData/Roaming", Environment.SpecialFolder.ApplicationData);
        yield return CreateMapping("ProgramData", Environment.SpecialFolder.CommonApplicationData);
        yield return CreateMapping("ProgramFiles", Environment.SpecialFolder.ProgramFiles);
        yield return CreateMapping("ProgramFilesX86", Environment.SpecialFolder.ProgramFilesX86);
        yield return CreateMapping("Desktop", Environment.SpecialFolder.Desktop);
        yield return CreateMapping("Documents", Environment.SpecialFolder.MyDocuments);
    }

    private static RootMapping CreateMapping(string category, Environment.SpecialFolder folder)
    {
        var root = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(root))
        {
            return new RootMapping(category, string.Empty);
        }

        var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return new RootMapping(category, normalized + Path.DirectorySeparatorChar);
    }

    private static string? TryExtractUserProfileRoot(string sourcePath)
    {
        if (!Path.IsPathRooted(sourcePath))
        {
            return null;
        }

        var parts = sourcePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        if (!parts[1].Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.Combine(parts[0], parts[1], parts[2]) + Path.DirectorySeparatorChar;
    }
}
