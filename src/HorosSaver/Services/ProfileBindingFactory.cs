using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HorosSaver.Models;

namespace HorosSaver.Services;

public static class ProfileBindingFactory
{
    private static readonly string[] Palette =
    [
        "#00D2B4", "#007ACC", "#4285F4", "#2496ED", "#1B2838",
        "#339933", "#5391FE", "#E95420", "#5865F2", "#FF6B35"
    ];

    public static ProgramProfile CreateBoundProfile(
        DiscoveredProgram discovered,
        IReadOnlyList<ProfilePathEntry> paths,
        int sortOrder,
        IReadOnlyCollection<string> existingIds)
    {
        var id = CreateUniqueId(discovered.DisplayName, existingIds);
        var glyph = CreateGlyph(discovered.DisplayName);
        var color = PickColor(discovered.DisplayName);

        var wingetId = KnownWingetIds.Resolve(discovered.DisplayName, discovered.Publisher);

        var detailLines = new List<string>
        {
            $"Quelle: {discovered.SourceLabel}",
            $"Publisher: {discovered.Publisher ?? "—"}",
            $"Version: {discovered.DisplayVersion ?? "—"}"
        };

        if (!string.IsNullOrWhiteSpace(discovered.InstallLocation))
        {
            detailLines.Add($"Install: {discovered.InstallLocation}");
        }

        if (!string.IsNullOrWhiteSpace(wingetId))
        {
            detailLines.Add($"winget: {wingetId}");
        }

        detailLines.Add($"Pfade: {paths.Count} konfiguriert");

        return new ProgramProfile
        {
            Id = id,
            Name = discovered.DisplayName,
            Category = "Eingebunden",
            Subtitle = discovered.Publisher ?? "Systemprogramm",
            IconGlyph = glyph,
            IconBackground = color,
            IsBound = true,
            Publisher = discovered.Publisher,
            InstalledVersion = discovered.DisplayVersion,
            InstallLocation = discovered.InstallLocation
                ?? (string.IsNullOrWhiteSpace(discovered.TargetPath)
                    ? null
                    : Path.GetDirectoryName(discovered.TargetPath)),
            WingetId = wingetId,
            SortOrder = sortOrder,
            Paths = paths.Select(ClonePath).ToList(),
            DetailLines = detailLines
        };
    }

    public static ProgramProfile CreateCustomPathsProfile(
        string name,
        IReadOnlyList<ProfilePathEntry> paths,
        int sortOrder,
        IReadOnlyCollection<string> existingIds)
    {
        var trimmedName = name.Trim();
        var id = CreateUniqueId(trimmedName, existingIds);
        var glyph = CreateGlyph(trimmedName);
        var color = PickColor(trimmedName);

        return new ProgramProfile
        {
            Id = id,
            Name = trimmedName,
            Category = "Dateien & Ordner",
            Subtitle = "Benutzerdefiniert",
            IconGlyph = glyph,
            IconBackground = color,
            IsBound = true,
            SortOrder = sortOrder,
            Paths = paths.Select(ClonePath).ToList(),
            DetailLines =
            [
                "Quelle: Dateien & Ordner",
                $"Pfade: {paths.Count} konfiguriert"
            ]
        };
    }

    public static void ApplyPathsToProfile(ProgramProfile profile, IReadOnlyList<ProfilePathEntry> paths)
    {
        profile.Paths = paths.Select(ClonePath).ToList();
        UpdatePathCountDetailLine(profile, paths.Count);
    }

    public static bool IsAlreadyBound(DiscoveredProgram discovered, IEnumerable<ProgramProfile> profiles)
    {
        var normalizedName = NormalizeName(discovered.DisplayName);
        return profiles.Any(profile =>
            string.Equals(NormalizeName(profile.Name), normalizedName, StringComparison.Ordinal)
            || (profile.IsBound
                && !string.IsNullOrWhiteSpace(profile.InstallLocation)
                && !string.IsNullOrWhiteSpace(discovered.InstallLocation)
                && string.Equals(
                    profile.InstallLocation.TrimEnd('\\'),
                    discovered.InstallLocation.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase)));
    }

    private static void UpdatePathCountDetailLine(ProgramProfile profile, int pathCount)
    {
        const string prefix = "Pfade:";
        var updated = false;

        for (var index = 0; index < profile.DetailLines.Count; index++)
        {
            if (profile.DetailLines[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                profile.DetailLines[index] = $"Pfade: {pathCount} konfiguriert";
                updated = true;
                break;
            }
        }

        if (!updated)
        {
            profile.DetailLines.Add($"Pfade: {pathCount} konfiguriert");
        }
    }

    private static ProfilePathEntry ClonePath(ProfilePathEntry entry)
        => new()
        {
            Label = entry.Label,
            SourcePath = entry.SourcePath,
            RelativeTarget = entry.RelativeTarget,
            IsDirectory = entry.IsDirectory
        };

    private static string CreateUniqueId(string displayName, IReadOnlyCollection<string> existingIds)
    {
        var baseSlug = Slugify(displayName);
        var candidate = $"bound-{baseSlug}";
        if (!existingIds.Contains(candidate))
        {
            return candidate;
        }

        for (var index = 2; index < 100; index++)
        {
            candidate = $"bound-{baseSlug}-{index}";
            if (!existingIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"bound-{baseSlug}-{Guid.NewGuid():N}"[..24];
    }

    private static string CreateGlyph(string displayName)
    {
        var letters = new string(displayName
            .Where(char.IsLetterOrDigit)
            .Take(2)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(letters) ? "AP" : letters;
    }

    private static string PickColor(string displayName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(displayName.ToLowerInvariant()));
        var index = hash[0] % Palette.Length;
        return Palette[index];
    }

    private static string NormalizeName(string value)
        => value.Trim().ToLower(CultureInfo.InvariantCulture);

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "app" : slug;
    }
}
