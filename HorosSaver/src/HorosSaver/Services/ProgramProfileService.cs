using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class ProgramProfileService : IProgramProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStoragePathResolver _paths;

    public ProgramProfileService(IStoragePathResolver paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ProgramProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var store = await LoadProfileStoreAsync(cancellationToken).ConfigureAwait(false);
        return store.Profiles;
    }

    public async Task<ProfileStoreData> LoadProfileStoreAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();

        if (!File.Exists(_paths.ProfilesFilePath))
        {
            await SaveProfileStoreAsync([], [], cancellationToken).ConfigureAwait(false);
            return new ProfileStoreData();
        }

        await using var stream = File.OpenRead(_paths.ProfilesFilePath);
        var document = await JsonSerializer.DeserializeAsync<ProfileStoreDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (document?.Profiles is null)
        {
            await SaveProfileStoreAsync([], [], cancellationToken).ConfigureAwait(false);
            return new ProfileStoreData();
        }

        var profiles = document.Profiles
            .OrderBy(profile => profile.SortOrder)
            .Select(profile =>
            {
                if (profile.IsBound)
                {
                    KnownWingetIds.EnrichProfile(profile);
                }

                return profile;
            })
            .ToList();

        var groups = document.Groups?
            .OrderBy(group => group.SortOrder)
            .ToList() ?? [];

        return new ProfileStoreData
        {
            Profiles = profiles,
            Groups = groups
        };
    }

    public async Task SaveProfilesAsync(IEnumerable<ProgramProfile> profiles, CancellationToken cancellationToken = default)
    {
        var store = await LoadProfileStoreAsync(cancellationToken).ConfigureAwait(false);
        await SaveProfileStoreAsync(profiles, store.Groups, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveProfileStoreAsync(
        IEnumerable<ProgramProfile> profiles,
        IEnumerable<ProgramGroup> groups,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();

        var document = new ProfileStoreDocument
        {
            SchemaVersion = 2,
            Profiles = profiles.OrderBy(profile => profile.SortOrder).ToList(),
            Groups = groups.OrderBy(group => group.SortOrder).ToList()
        };

        await using var stream = File.Create(_paths.ProfilesFilePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<ProgramGroup> AutoDetectGroups(IReadOnlyList<ProgramProfile> profiles)
        => ProgramGroupDetector.AutoDetectGroups(profiles);

    public void ApplyAutoGroups(IReadOnlyList<ProgramProfile> profiles, IReadOnlyList<ProgramGroup> groups)
    {
        foreach (var profile in profiles)
        {
            ProgramGroupDetector.ClearGroupMembership(profile);
        }

        ProgramGroupDetector.ApplyAutoGroups(profiles, groups);
    }

    public async Task UpdateSortOrderAsync(IEnumerable<ProgramProfile> orderedProfiles, CancellationToken cancellationToken = default)
    {
        var index = 0;
        foreach (var profile in orderedProfiles)
        {
            profile.SortOrder = index++;
        }

        await SaveProfilesAsync(orderedProfiles, cancellationToken).ConfigureAwait(false);
    }
}

internal static class DefaultProfileFactory
{
    public static List<ProgramProfile> CreateDefaultProfiles()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cursorUser = Path.Combine(appData, "Cursor", "User");

        return
        [
            CreateCursorProfile(cursorUser, userProfile),
            CreateProfile("vscode", "VS Code", "Editor", "Entwicklertools", "VS", "#007ACC", 1,
                [
                    "Extensions: 32",
                    "Settings: sync",
                    "Keybindings: custom",
                    "Workspace: HorosSaver"
                ],
                new DateTimeOffset(2026, 7, 24, 10, 11, 0, TimeSpan.Zero),
                [
                    PathEntry("settings.json", Path.Combine(appData, "Code", "User", "settings.json"), "User/settings.json"),
                    PathEntry("keybindings.json", Path.Combine(appData, "Code", "User", "keybindings.json"), "User/keybindings.json"),
                    PathEntry("snippets", Path.Combine(appData, "Code", "User", "snippets"), "User/snippets", isDirectory: true),
                    PathEntry("extensions", Path.Combine(userProfile, ".vscode", "extensions"), "extensions", isDirectory: true)
                ]),
            CreateProfile("chrome", "Google Chrome", "Browser", "Web", "GC", "#4285F4", 2,
                ["Profile: Work", "Bookmarks: 1.2k", "Extensions: 24"],
                new DateTimeOffset(2026, 7, 24, 9, 45, 0, TimeSpan.Zero),
                [
                    PathEntry("Local State", Path.Combine(appData, "Google", "Chrome", "Local State"), "Chrome/Local State"),
                    PathEntry("Bookmarks", Path.Combine(appData, "Google", "Chrome", "Default", "Bookmarks"), "Chrome/Default/Bookmarks"),
                    PathEntry("Preferences", Path.Combine(appData, "Google", "Chrome", "Default", "Preferences"), "Chrome/Default/Preferences")
                ]),
            CreateProfile("docker", "Docker Desktop", "Container", "DevOps", "DK", "#2496ED", 3,
                ["Containers: 3", "Images: 12", "Volumes: 8"],
                new DateTimeOffset(2026, 7, 23, 16, 20, 0, TimeSpan.Zero),
                [
                    PathEntry("settings.json", Path.Combine(appData, "Docker", "settings.json"), "Docker/settings.json"),
                    PathEntry("daemon.json", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Docker", "config", "daemon.json"), "Docker/daemon.json")
                ]),
            CreateProfile("steam", "Steam", "Gaming", "Plattform", "ST", "#1B2838", 4,
                ["Library: 84 Spiele", "Playtime: 256 Std."],
                new DateTimeOffset(2026, 7, 23, 12, 8, 0, TimeSpan.Zero),
                [
                    PathEntry("config", Path.Combine(appData, "Steam"), "Steam", isDirectory: true)
                ]),
            CreateProfile("jetbrains", "JetBrains Toolbox", "Tools", "IDE Manager", "JB", "#000000", 5,
                ["Tools: 7", "Settings: sync"],
                new DateTimeOffset(2026, 7, 22, 18, 33, 0, TimeSpan.Zero),
                [
                    PathEntry("Toolbox", Path.Combine(appData, "JetBrains", "Toolbox"), "JetBrains/Toolbox", isDirectory: true)
                ]),
            CreateProfile("nodejs", "Node.js LTS", "Runtime", "Profil", "N", "#339933", 6,
                ["Global Packages: 12", "npmrc: custom", "nvm: optional"],
                null,
                [
                    PathEntry(".npmrc", Path.Combine(userProfile, ".npmrc"), ".npmrc"),
                    PathEntry("npm", Path.Combine(appData, "npm"), "npm", isDirectory: true)
                ]),
            CreateProfile("powershell", "PowerShell 7", "Shell", "Profil", "PS", "#5391FE", 7,
                ["Profile.ps1: aktiv", "Modules: 8", "PSReadLine: History"],
                null,
                [
                    PathEntry("Microsoft.PowerShell_profile.ps1", Path.Combine(userProfile, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1"), "PowerShell/Microsoft.PowerShell_profile.ps1"),
                    PathEntry("Modules", Path.Combine(userProfile, "Documents", "PowerShell", "Modules"), "PowerShell/Modules", isDirectory: true)
                ])
        ];
    }

    private static ProgramProfile CreateCursorProfile(string cursorUser, string userProfile)
    {
        var profile = new ProgramProfile
        {
            Id = "cursor",
            Name = "Cursor",
            Category = "IDE",
            Subtitle = "Vollständiges Profil",
            IconGlyph = "CR",
            IconBackground = "#00D2B4",
            IsActive = true,
            SortOrder = 0,
            DetailLines =
            [
                "Extensions: 47",
                "Settings: sync",
                "Keybindings: custom",
                "Workspace: HorosSaver"
            ],
            LastSnapshotAt = new DateTimeOffset(2026, 7, 24, 14, 32, 0, TimeSpan.Zero),
            CursorSnapshotLevel = CursorSnapshotLevel.Standard
        };

        CursorSnapshotPaths.ApplyLevelToProfile(profile, CursorSnapshotLevel.Standard);
        return profile;
    }

    private static ProgramProfile CreateProfile(
        string id,
        string name,
        string category,
        string subtitle,
        string glyph,
        string color,
        int sortOrder,
        List<string> detailLines,
        DateTimeOffset? lastSnapshotAt,
        List<ProfilePathEntry> paths)
    {
        return new ProgramProfile
        {
            Id = id,
            Name = name,
            Category = category,
            Subtitle = subtitle,
            IconGlyph = glyph,
            IconBackground = color,
            SortOrder = sortOrder,
            DetailLines = detailLines,
            LastSnapshotAt = lastSnapshotAt,
            Paths = paths
        };
    }

    private static ProfilePathEntry PathEntry(
        string label,
        string sourcePath,
        string relativeTarget,
        bool isDirectory = false)
    {
        return new ProfilePathEntry
        {
            Label = label,
            SourcePath = sourcePath,
            RelativeTarget = relativeTarget,
            IsDirectory = isDirectory
        };
    }
}
