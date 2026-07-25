using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class SnapshotDisplayName
{
    public static string Build(string? programDisplayName, DateTimeOffset createdAt)
    {
        var timestamp = createdAt.ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(programDisplayName))
        {
            return timestamp;
        }

        return $"{programDisplayName.Trim()} — {timestamp}";
    }

    public static string BuildFileNamePart(string programDisplayName, DateTimeOffset createdAt)
    {
        var safeName = SanitizeForFileName(programDisplayName);
        var timestamp = createdAt.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"{safeName}_{timestamp}";
    }

    public static string ResolveProgramDisplayName(
        SnapshotManifest manifest,
        IReadOnlyDictionary<string, string>? profileNamesById = null)
    {
        if (!string.IsNullOrWhiteSpace(manifest.ProgramName))
        {
            return manifest.ProgramName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(manifest.ProgramInstall?.ProgramName))
        {
            return manifest.ProgramInstall.ProgramName.Trim();
        }

        if (profileNamesById is not null
            && profileNamesById.TryGetValue(manifest.ProgramId, out var profileName)
            && !string.IsNullOrWhiteSpace(profileName))
        {
            return profileName.Trim();
        }

        return manifest.ProgramId;
    }

    public static string SanitizeForFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Snapshot";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim('_', ' ', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "Snapshot" : sanitized;
    }
}
