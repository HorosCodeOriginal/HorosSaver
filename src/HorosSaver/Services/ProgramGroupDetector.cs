using System.Globalization;
using System.Text;
using HorosSaver.Models;

namespace HorosSaver.Services;

public static class ProgramGroupDetector
{
    private const int MinimumStemLength = 2;
    private const int MinimumMembers = 2;

    public static IReadOnlyList<ProgramGroup> AutoDetectGroups(IReadOnlyList<ProgramProfile> profiles)
    {
        var candidates = new Dictionary<string, List<ProgramProfile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            var stem = ExtractGroupStem(profile.Name);
            if (stem is null)
            {
                continue;
            }

            if (!candidates.TryGetValue(stem, out var members))
            {
                members = [];
                candidates[stem] = members;
            }

            members.Add(profile);
        }

        var groups = new List<ProgramGroup>();
        var sortOrder = 0;

        foreach (var (stem, members) in candidates.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (members.Count < MinimumMembers)
            {
                continue;
            }

            if (!ShouldGroup(members))
            {
                continue;
            }

            groups.Add(new ProgramGroup
            {
                Id = CreateGroupId(stem),
                Name = stem,
                SortOrder = sortOrder++
            });
        }

        return groups;
    }

    public static void ApplyAutoGroups(
        IReadOnlyList<ProgramProfile> profiles,
        IReadOnlyList<ProgramGroup> groups)
    {
        var groupsByName = groups.ToDictionary(
            group => group.Name,
            group => group,
            StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            var stem = ExtractGroupStem(profile.Name);
            if (stem is null
                || !groupsByName.TryGetValue(stem, out var group))
            {
                continue;
            }

            profile.GroupId = group.Id;
            profile.GroupName = group.Name;
        }
    }

    public static void ClearGroupMembership(ProgramProfile profile)
    {
        profile.GroupId = null;
        profile.GroupName = null;
    }

    public static string? ExtractGroupStem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var stem = trimmed;

        var parenIndex = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex > 0)
        {
            stem = trimmed[..parenIndex].Trim();
        }

        var spaceIndex = stem.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex > 0)
        {
            stem = stem[..spaceIndex].Trim();
        }

        return stem.Length >= MinimumStemLength ? stem : null;
    }

    private static bool ShouldGroup(IReadOnlyList<ProgramProfile> members)
    {
        var publishers = members
            .Select(profile => NormalizePublisher(profile.Publisher))
            .Where(publisher => publisher is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (publishers.Count <= 1)
        {
            return true;
        }

        return members.All(profile => ExtractGroupStem(profile.Name) is not null);
    }

    private static string? NormalizePublisher(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return null;
        }

        return publisher.Trim();
    }

    private static string CreateGroupId(string stem)
    {
        var normalized = stem.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (character is ' ' or '-' or '_' && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "group";
        }

        return $"group-{slug}";
    }
}
