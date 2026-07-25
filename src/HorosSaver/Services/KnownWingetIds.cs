namespace HorosSaver.Services;

/// <summary>
/// Known winget package IDs for frequently bound HorosSaver profiles.
/// Used when binding/restoring programs where registry metadata lacks a winget ID.
/// </summary>
public static class KnownWingetIds
{
    private sealed record WingetRule(string[] NameFragments, string WingetId);

    private static readonly WingetRule[] Rules =
    [
        Rule(["copyq"], "hluk.CopyQ"),
        Rule(["visual studio code", "vscode"], "Microsoft.VisualStudioCode"),
        Rule(["google chrome", "chrome"], "Google.Chrome"),
        Rule(["cursor"], "Anysphere.Cursor"),
        Rule(["everything"], "voidtools.Everything"),
        Rule(["mem reduct"], "Henry++.MemReduct"),
        Rule(["7-zip", "7zip"], "7zip.7zip"),
        Rule(["git"], "Git.Git"),
        Rule(["powershell 7", "powershell"], "Microsoft.PowerShell"),
        Rule(["docker desktop", "docker"], "Docker.DockerDesktop"),
        Rule(["node.js", "nodejs"], "OpenJS.NodeJS.LTS"),
        Rule(["steam"], "Valve.Steam"),
        Rule(["opera"], "Opera.Opera"),
        Rule(["outlook for windows", "new outlook"], "9NRX63209R7B"),
        Rule(["outlook"], "Microsoft.Office"),
        Rule(["whatsapp"], "9NKSQGP7F2NH"),
        Rule(["signal"], "OpenWhisperSystems.Signal"),
        Rule(["rustdesk"], "RustDesk.RustDesk"),
        Rule(["jetbrains", "toolbox"], "JetBrains.Toolbox"),
        Rule(["virtualbox"], "Oracle.VirtualBox")
    ];

    public static string? Resolve(string displayName, string? publisher = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var normalized = displayName.ToLowerInvariant();
        foreach (var rule in Rules)
        {
            if (rule.NameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)))
            {
                return rule.WingetId;
            }
        }

        _ = publisher;
        return null;
    }

    public static void EnrichProfile(Models.ProgramProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.WingetId))
        {
            return;
        }

        var resolved = Resolve(profile.Name, profile.Publisher);
        if (resolved is not null)
        {
            profile.WingetId = resolved;
        }
    }

    private static WingetRule Rule(string[] fragments, string wingetId) => new(fragments, wingetId);
}
