using System.Text.RegularExpressions;
using HorosSaver.Models;

namespace HorosSaver.Services;

public static class KnownAppPathDefaults
{
    private sealed record AppRule(
        string[] NameFragments,
        Func<string, string, string, IReadOnlyList<ProfilePathEntry>> BuildPaths);

    private static readonly AppRule[] Rules =
    [
        Rule(["visual studio code", "vscode"], BuildVsCodePaths),
        Rule(["google chrome", "chrome"], BuildChromePaths),
        Rule(["cursor"], BuildCursorPaths),
        Rule(["opera"], BuildOperaPaths),
        Rule(["everything"], BuildEverythingPaths),
        Rule(["mem reduct"], BuildMemReductPaths),
        Rule(["copyq"], BuildCopyQPaths),
        Rule(["outlook"], BuildOutlookPaths),
        Rule(["whatsapp"], BuildWhatsAppPaths),
        Rule(["signal"], BuildSignalPaths),
        Rule(["rustdesk"], BuildRustDeskPaths),
        Rule(["powershell 7", "powershell"], BuildPowerShellPaths),
        Rule(["docker desktop", "docker"], BuildDockerPaths),
        Rule(["steam"], BuildSteamPaths),
        Rule(["jetbrains", "toolbox"], BuildJetBrainsPaths),
        Rule(["node.js", "nodejs"], BuildNodePaths),
        Rule(["virtualbox"], BuildVirtualBoxPaths)
    ];

    public static IReadOnlyList<ProfilePathEntry> Resolve(string displayName, string? installLocation)
    {
        var normalized = displayName.ToLowerInvariant();
        foreach (var rule in Rules)
        {
            if (rule.NameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)))
            {
                return rule.BuildPaths(displayName, installLocation ?? string.Empty, normalized);
            }
        }

        return BuildGenericPaths(displayName, installLocation);
    }

    public static string GetPathHint(string displayName)
    {
        var normalized = displayName.ToLowerInvariant();
        if (normalized.Contains("copyq", StringComparison.Ordinal))
        {
            return "CopyQ: AppData-Pfade sind vorkonfiguriert. Exportierte Settings (.cpq) z. B. unter " +
                   "%USERPROFILE%\\Documents\\ per „Datei hinzufügen…\" ergänzen " +
                   "(Beispiel: copyq settings 25072026.cpq).";
        }

        if (normalized.Contains("cursor", StringComparison.Ordinal))
        {
            return "Cursor: Snapshot-Level in den Einstellungen wählen (Minimal / Standard / Voll). " +
                   "IDE-Installationen unter Program Files werden nie gesichert.";
        }

        if (normalized.Contains("outlook", StringComparison.Ordinal))
        {
            return "Outlook Classic: OST/PST, Profil-XML (Roaming), Signaturen und Vorlagen sind vorkonfiguriert. " +
                   "Konten-Passwörter/Token liegen nicht in diesen Ordnern — nach Restore ggf. erneut anmelden. " +
                   "New Outlook (Store): Paket LocalState wird automatisch ergänzt, wenn vorhanden.";
        }

        if (normalized.Contains("whatsapp", StringComparison.Ordinal))
        {
            return "WhatsApp: Store-Paket (LocalState) und Desktop-Ordner (%APPDATA%\\WhatsApp, %LOCALAPPDATA%\\WhatsApp) " +
                   "werden automatisch erkannt. WhatsApp vor dem Snapshot schließen — Datenbankdateien sind oft gesperrt.";
        }

        if (normalized.Contains("steam", StringComparison.Ordinal)
            || normalized.Contains("game", StringComparison.Ordinal))
        {
            return "Typisch: %APPDATA% oder Installationsordner unter Program Files.";
        }

        if (normalized.Contains("virtualbox", StringComparison.Ordinal))
        {
            return "VirtualBox: Konfiguration unter %USERPROFILE%\\.VirtualBox (VirtualBox.xml, …). " +
                   "VM-Ordner standardmäßig %USERPROFILE%\\VirtualBox VMs\\ — VM-Disks (.vdi) können sehr groß sein; " +
                   "ggf. einzelne VMs per „Ordner hinzufügen…\" statt des gesamten VM-Ordners sichern.";
        }

        return "Typisch: %APPDATA%, %LOCALAPPDATA% oder %ProgramData% — Pfade anpassen oder ergänzen. " +
               "Einzelne Dateien und Ordner per „Datei hinzufügen…\" / „Ordner hinzufügen…\".";
    }

    private static AppRule Rule(
        string[] fragments,
        Func<string, string, string, IReadOnlyList<ProfilePathEntry>> buildPaths)
        => new(fragments, buildPaths);

    private static IReadOnlyList<ProfilePathEntry> BuildGenericPaths(string displayName, string? installLocation)
    {
        var entries = new List<ProfilePathEntry>();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var slug = Slugify(displayName);

        entries.Add(DirEntry("AppData (Roaming)", Path.Combine(appData, displayName), slug));
        entries.Add(DirEntry("LocalAppData", Path.Combine(localAppData, displayName), $"{slug}-local"));
        entries.Add(DirEntry("ProgramData", Path.Combine(programData, displayName), $"{slug}-programdata"));

        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            entries.Insert(0, DirEntry("Installationsordner", installLocation, "install"));
        }

        return entries;
    }

    private static IReadOnlyList<ProfilePathEntry> BuildVsCodePaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            FileEntry("settings.json", Path.Combine(appData, "Code", "User", "settings.json"), "User/settings.json"),
            FileEntry("keybindings.json", Path.Combine(appData, "Code", "User", "keybindings.json"), "User/keybindings.json"),
            DirEntry("snippets", Path.Combine(appData, "Code", "User", "snippets"), "User/snippets"),
            DirEntry("extensions", Path.Combine(userProfile, ".vscode", "extensions"), "extensions")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildChromePaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var chromeRoot = Path.Combine(appData, "Google", "Chrome");
        return
        [
            FileEntry("Local State", Path.Combine(chromeRoot, "Local State"), "Chrome/Local State"),
            FileEntry("Bookmarks", Path.Combine(chromeRoot, "Default", "Bookmarks"), "Chrome/Default/Bookmarks"),
            FileEntry("Preferences", Path.Combine(chromeRoot, "Default", "Preferences"), "Chrome/Default/Preferences"),
            DirEntry("Profile", Path.Combine(chromeRoot, "Default"), "Chrome/Default")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildCursorPaths(string _, string __, string ___)
        => CursorSnapshotPaths.Resolve(CursorSnapshotLevel.Standard);

    private static IReadOnlyList<ProfilePathEntry> BuildOperaPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var operaRoot = Path.Combine(appData, "Opera Software", "Opera Stable");
        return
        [
            DirEntry("Opera Profil", operaRoot, "Opera"),
            FileEntry("Preferences", Path.Combine(operaRoot, "Preferences"), "Opera/Preferences"),
            FileEntry("Bookmarks", Path.Combine(operaRoot, "Bookmarks"), "Opera/Bookmarks")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildEverythingPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            FileEntry("Everything.ini", Path.Combine(appData, "Everything", "Everything.ini"), "Everything/Everything.ini"),
            DirEntry("Everything", Path.Combine(appData, "Everything"), "Everything")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildMemReductPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            FileEntry("memreduct.ini", Path.Combine(appData, "Mem Reduct", "memreduct.ini"), "MemReduct/memreduct.ini"),
            DirEntry("Mem Reduct", Path.Combine(appData, "Mem Reduct"), "MemReduct")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildCopyQPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            FileEntry("copyq.ini", Path.Combine(appData, "copyq", "copyq.ini"), "copyq/copyq.ini"),
            DirEntry("copyq", Path.Combine(appData, "copyq"), "copyq")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildOutlookPaths(string _, string __, string ___)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var entries = new List<ProfilePathEntry>
        {
            DirEntry("Outlook Daten (LocalAppData)", Path.Combine(localAppData, "Microsoft", "Outlook"), "Outlook-Local"),
            DirEntry("Outlook Profil (Roaming)", Path.Combine(appData, "Microsoft", "Outlook"), "Outlook-Roaming"),
            DirEntry("Signatures", Path.Combine(appData, "Microsoft", "Signatures"), "Signatures"),
            DirEntry("Templates", Path.Combine(appData, "Microsoft", "Templates"), "Templates"),
            DirEntry("Outlook-Dateien (Documents)", Path.Combine(documents, "Outlook Files"), "Outlook-Files")
        };

        var olkPath = Path.Combine(localAppData, "Microsoft", "Olk");
        if (Directory.Exists(olkPath))
        {
            entries.Add(DirEntry("New Outlook (Olk)", olkPath, "Outlook-New-Olk"));
        }

        foreach (var packagePath in FindPackageDirectories(localAppData, "OutlookForWindows"))
        {
            entries.Add(DirEntry("New Outlook (Store-Paket)", packagePath, "Outlook-New-Store"));
            var localState = Path.Combine(packagePath, "LocalState");
            if (Directory.Exists(localState))
            {
                entries.Add(DirEntry("New Outlook LocalState", localState, "Outlook-New-LocalState"));
            }
        }

        return entries;
    }

    private static IReadOnlyList<ProfilePathEntry> BuildWhatsAppPaths(string _, string __, string ___)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var entries = new List<ProfilePathEntry>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfNew(ProfilePathEntry entry)
        {
            if (seenTargets.Add(entry.RelativeTarget))
            {
                entries.Add(entry);
            }
        }

        foreach (var packagePath in FindPackageDirectories(localAppData, "WhatsApp"))
        {
            AddIfNew(DirEntry("WhatsApp Store-Paket", packagePath, "WhatsApp-Store"));
            var localState = Path.Combine(packagePath, "LocalState");
            if (Directory.Exists(localState))
            {
                AddIfNew(DirEntry("WhatsApp LocalState", localState, "WhatsApp-LocalState"));
            }
        }

        AddIfNew(DirEntry("WhatsApp (Roaming)", Path.Combine(appData, "WhatsApp"), "WhatsApp-Roaming"));
        AddIfNew(DirEntry("WhatsApp (LocalAppData)", Path.Combine(localAppData, "WhatsApp"), "WhatsApp-Local"));

        return entries;
    }

    private static IReadOnlyList<string> FindPackageDirectories(string localAppData, string nameFragment)
    {
        var packagesRoot = Path.Combine(localAppData, "Packages");
        if (!Directory.Exists(packagesRoot))
        {
            return [];
        }

        return Directory.GetDirectories(packagesRoot)
            .Where(path => Path.GetFileName(path).Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ProfilePathEntry> BuildSignalPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            DirEntry("Signal", Path.Combine(appData, "Signal"), "Signal"),
            FileEntry("config.json", Path.Combine(appData, "Signal", "config.json"), "Signal/config.json")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildRustDeskPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            DirEntry("RustDesk", Path.Combine(appData, "RustDesk"), "RustDesk"),
            FileEntry("RustDesk.toml", Path.Combine(appData, "RustDesk", "config", "RustDesk.toml"), "RustDesk/config/RustDesk.toml")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildPowerShellPaths(string _, string __, string ___)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var psRoot = Path.Combine(userProfile, "Documents", "PowerShell");
        return
        [
            FileEntry("Microsoft.PowerShell_profile.ps1", Path.Combine(psRoot, "Microsoft.PowerShell_profile.ps1"), "PowerShell/Microsoft.PowerShell_profile.ps1"),
            DirEntry("Modules", Path.Combine(psRoot, "Modules"), "PowerShell/Modules")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildDockerPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return
        [
            FileEntry("settings.json", Path.Combine(appData, "Docker", "settings.json"), "Docker/settings.json"),
            FileEntry("daemon.json", Path.Combine(programData, "Docker", "config", "daemon.json"), "Docker/daemon.json")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildSteamPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return [DirEntry("Steam", Path.Combine(appData, "Steam"), "Steam")];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildJetBrainsPaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return [DirEntry("JetBrains Toolbox", Path.Combine(appData, "JetBrains", "Toolbox"), "JetBrains/Toolbox")];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildNodePaths(string _, string __, string ___)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            FileEntry(".npmrc", Path.Combine(userProfile, ".npmrc"), ".npmrc"),
            DirEntry("npm", Path.Combine(appData, "npm"), "npm")
        ];
    }

    private static IReadOnlyList<ProfilePathEntry> BuildVirtualBoxPaths(string _, string __, string ___)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var entries = new List<ProfilePathEntry>
        {
            DirEntry(
                "VirtualBox Konfiguration (.VirtualBox)",
                Path.Combine(userProfile, ".VirtualBox"),
                "VirtualBox/.VirtualBox"),
            DirEntry(
                "VirtualBox VMs (VM-Disks können sehr groß sein)",
                Path.Combine(userProfile, "VirtualBox VMs"),
                "VirtualBox/VirtualBox VMs")
        };

        var oracleRoaming = Path.Combine(appData, "Oracle", "VirtualBox");
        if (Directory.Exists(oracleRoaming))
        {
            entries.Add(DirEntry("Oracle VirtualBox (Roaming)", oracleRoaming, "VirtualBox/Oracle-Roaming"));
        }

        var oracleLocal = Path.Combine(localAppData, "Oracle", "VirtualBox");
        if (Directory.Exists(oracleLocal))
        {
            entries.Add(DirEntry("Oracle VirtualBox (LocalAppData)", oracleLocal, "VirtualBox/Oracle-Local"));
        }

        var oracleProgramData = Path.Combine(programData, "Oracle", "VirtualBox");
        if (Directory.Exists(oracleProgramData))
        {
            entries.Add(DirEntry("Oracle VirtualBox (ProgramData)", oracleProgramData, "VirtualBox/Oracle-ProgramData"));
        }

        return entries;
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

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "app" : slug;
    }
}
