using HorosSaver.Models;

namespace HorosSaver.Services;

public static class CursorSnapshotPaths
{
    public static bool IsCursorProfile(ProgramProfile profile)
    {
        if (profile.Id.Equals("cursor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return profile.Name.Contains("cursor", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCursorProgramName(string displayName)
        => displayName.Contains("cursor", StringComparison.OrdinalIgnoreCase);

    public static CursorSnapshotLevel NormalizeLevel(CursorSnapshotLevel level)
        => level is CursorSnapshotLevel.Minimal or CursorSnapshotLevel.Standard or CursorSnapshotLevel.Full
            ? level
            : CursorSnapshotLevel.Standard;

    public static IReadOnlyList<ProfilePathEntry> Resolve(CursorSnapshotLevel level)
    {
        return NormalizeLevel(level) switch
        {
            CursorSnapshotLevel.Minimal => BuildMinimalPaths(),
            CursorSnapshotLevel.Full => BuildFullPaths(),
            _ => BuildStandardPaths()
        };
    }

    public static void ApplyLevelToProfile(ProgramProfile profile, CursorSnapshotLevel level)
    {
        var normalized = NormalizeLevel(level);
        profile.CursorSnapshotLevel = normalized;
        profile.Paths = Resolve(normalized).Select(ClonePath).ToList();
        UpdateDetailLines(profile, normalized);
    }

    public static string GetLevelLabel(CursorSnapshotLevel level) => NormalizeLevel(level) switch
    {
        CursorSnapshotLevel.Minimal => "1 — Minimal (~1 GB)",
        CursorSnapshotLevel.Full => "3 — Voll (~31 GB)",
        _ => "2 — Standard (~14 GB)"
    };

    public static string GetLevelDescription(CursorSnapshotLevel level) => NormalizeLevel(level) switch
    {
        CursorSnapshotLevel.Minimal =>
            "Settings, Snippets, Extensions, argv, rules, skills, commands, hooks, agents — ohne Chats/globalStorage.",
        CursorSnapshotLevel.Full =>
            "Komplett %APPDATA%\\Cursor und %USERPROFILE%\\.cursor (ohne IDE-Installation).",
        _ => "Standard inkl. globalStorage, workspaceStorage, projects, History — ohne Cache/Logs/snapshots."
    };

    public static string GetSecretsHint(CursorSnapshotLevel level)
    {
        return NormalizeLevel(level) switch
        {
            CursorSnapshotLevel.Minimal => string.Empty,
            _ => "Hinweis: globalStorage enthält u. a. state.vscdb (Chats, Tokens) — nur lokal sichern."
        };
    }

    private static void UpdateDetailLines(ProgramProfile profile, CursorSnapshotLevel level)
    {
        const string detailPrefix = "Snapshot-Level:";
        profile.DetailLines.RemoveAll(line =>
            line.StartsWith(detailPrefix, StringComparison.OrdinalIgnoreCase));

        profile.DetailLines.Insert(0, $"{detailPrefix} {GetLevelLabel(level)}");
    }

    private static IReadOnlyList<ProfilePathEntry> BuildMinimalPaths()
    {
        var (cursorUser, cursorDot) = ResolveRoots();
        return
        [
            FileEntry("settings.json", Path.Combine(cursorUser, "settings.json"), "User/settings.json"),
            FileEntry("keybindings.json", Path.Combine(cursorUser, "keybindings.json"), "User/keybindings.json"),
            DirEntry("snippets", Path.Combine(cursorUser, "snippets"), "User/snippets"),
            DirEntry("extensions", Path.Combine(cursorDot, "extensions"), "extensions"),
            FileEntry("argv.json", Path.Combine(cursorDot, "argv.json"), ".cursor/argv.json"),
            DirEntry("rules", Path.Combine(cursorDot, "rules"), ".cursor/rules"),
            DirEntry("skills", Path.Combine(cursorDot, "skills"), ".cursor/skills"),
            DirEntry("commands", Path.Combine(cursorDot, "commands"), ".cursor/commands"),
            DirEntry("hooks", Path.Combine(cursorDot, "hooks"), ".cursor/hooks"),
            FileEntry("hooks.json", Path.Combine(cursorDot, "hooks.json"), ".cursor/hooks.json"),
            DirEntry("agents", Path.Combine(cursorDot, "agents"), ".cursor/agents"),
            FileEntry("permissions.json", Path.Combine(cursorDot, "permissions.json"), ".cursor/permissions.json"),
            FileEntry("ide_state.json", Path.Combine(cursorDot, "ide_state.json"), ".cursor/ide_state.json")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildStandardPaths()
    {
        var (cursorUser, cursorDot) = ResolveRoots();
        var cursorRoaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor");

        var entries = new List<ProfilePathEntry>(BuildMinimalPaths());
        entries.AddRange(
        [
            DirEntry("globalStorage", Path.Combine(cursorUser, "globalStorage"), "User/globalStorage"),
            DirEntry("workspaceStorage", Path.Combine(cursorUser, "workspaceStorage"), "User/workspaceStorage"),
            DirEntry("projects", Path.Combine(cursorDot, "projects"), ".cursor/projects"),
            DirEntry("History", Path.Combine(cursorUser, "History"), "User/History"),
            FileEntry("Preferences", Path.Combine(cursorRoaming, "Preferences"), "Cursor/Preferences"),
            FileEntry("Local State", Path.Combine(cursorRoaming, "Local State"), "Cursor/Local State"),
            DirEntry("ai-tracking", Path.Combine(cursorDot, "ai-tracking"), ".cursor/ai-tracking"),
            DirEntry("skills-cursor", Path.Combine(cursorDot, "skills-cursor"), ".cursor/skills-cursor")
        ]);

        return entries;
    }

    private static IReadOnlyList<ProfilePathEntry> BuildFullPaths()
    {
        var (cursorUser, cursorDot) = ResolveRoots();
        var cursorRoaming = Path.GetDirectoryName(cursorUser)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cursor");

        return
        [
            DirEntry("Cursor (Roaming)", cursorRoaming, "Cursor"),
            DirEntry(".cursor (User)", cursorDot, ".cursor")
        ];
    }

    private static (string CursorUser, string CursorDot) ResolveRoots()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return (Path.Combine(appData, "Cursor", "User"), Path.Combine(userProfile, ".cursor"));
    }

    private static ProfilePathEntry FileEntry(string label, string sourcePath, string relativeTarget)
        => new()
        {
            Label = label,
            SourcePath = sourcePath,
            RelativeTarget = relativeTarget,
            IsDirectory = false
        };

    private static ProfilePathEntry DirEntry(string label, string sourcePath, string relativeTarget)
        => new()
        {
            Label = label,
            SourcePath = sourcePath,
            RelativeTarget = relativeTarget,
            IsDirectory = true
        };

    private static ProfilePathEntry ClonePath(ProfilePathEntry entry)
        => new()
        {
            Label = entry.Label,
            SourcePath = entry.SourcePath,
            RelativeTarget = entry.RelativeTarget,
            IsDirectory = entry.IsDirectory
        };
}
