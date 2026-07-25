using System.Globalization;
using System.Text.RegularExpressions;
using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class ZuSavenSeeder : IZuSavenSeeder
{
    private const string EmbeddedZuSaven = """
        programm mem reduct
        programm copy q
        programm everything
        programm opera
        .programme (ordner ist unter C:\)
        scripts (ordner ist unter C:\)
        programm outlook
        programm whatsapp
        programm signal
        programm cursor
        NewPlusCyberpunk (ordner ist unter C:\Users\Administrator)
        Powershell (ordner C:\Users\Administrator\Documents)
        Everything
        Mem Reduct
        PowerShell
        RustDesk
        Cursor
        """;

    private static readonly Regex FolderPathRegex = new(
        @"^(?<name>.+?)\s*\(ordner(?:\s+ist\s+unter)?\s+(?<path>.+?)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IInstalledProgramDiscoveryService _discoveryService;
    private readonly IStoragePathResolver _paths;

    public ZuSavenSeeder(
        IInstalledProgramDiscoveryService discoveryService,
        IStoragePathResolver paths)
    {
        _discoveryService = discoveryService;
        _paths = paths;
    }

    public async Task<ZuSavenSeedResult> ApplyAsync(
        IList<ProgramProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        var (content, sourceFile) = ResolveZuSavenContent();
        var parsedEntries = ParseZuSavenContent(content);
        var dedupedEntries = DeduplicateEntries(parsedEntries);

        var installedPrograms = (await _discoveryService
            .DiscoverInstalledProgramsAsync(cancellationToken)
            .ConfigureAwait(false)).ToList();

        var mappings = new List<ZuSavenMappingEntry>();
        var manualItems = new List<string>();
        var profilesAdded = 0;
        var profilesUpdated = 0;
        var pathsMerged = 0;

        foreach (var entry in dedupedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (entry.Kind)
            {
                case ZuSavenEntryKind.Program:
                {
                    var result = UpsertProgramEntry(
                        profiles,
                        installedPrograms,
                        entry);

                    mappings.Add(result.Mapping);
                    profilesAdded += result.ProfilesAdded;
                    profilesUpdated += result.ProfilesUpdated;
                    pathsMerged += result.PathsMerged;

                    if (result.ManualNote is not null)
                    {
                        manualItems.Add(result.ManualNote);
                    }

                    break;
                }

                case ZuSavenEntryKind.CustomFolder:
                {
                    var result = UpsertCustomFolderEntry(profiles, entry);
                    mappings.Add(result.Mapping);
                    profilesAdded += result.ProfilesAdded;
                    profilesUpdated += result.ProfilesUpdated;
                    pathsMerged += result.PathsMerged;

                    if (result.ManualNote is not null)
                    {
                        manualItems.Add(result.ManualNote);
                    }

                    break;
                }
            }
        }

        pathsMerged += MergeCopyQExports(profiles, mappings);
        var removedDuplicates = RemoveRedundantCustomProfiles(profiles);

        return new ZuSavenSeedResult
        {
            Changed = profilesAdded > 0 || profilesUpdated > 0 || pathsMerged > 0 || removedDuplicates > 0,
            ProfilesAdded = profilesAdded,
            ProfilesUpdated = profilesUpdated,
            PathsMerged = pathsMerged,
            Mappings = mappings,
            ManualItems = manualItems,
            SourceFile = sourceFile
        };
    }

    private EntryUpsertResult UpsertProgramEntry(
        IList<ProgramProfile> profiles,
        IReadOnlyList<DiscoveredProgram> installedPrograms,
        ZuSavenParsedEntry entry)
    {
        var existing = FindMatchingProgramProfile(profiles, entry.SearchTerms);
        var discovered = FindInstalledProgram(installedPrograms, entry.SearchTerms);

        if (existing is not null)
        {
            var merged = MergeDefaultPaths(existing, discovered?.DisplayName ?? existing.Name, discovered?.InstallLocation);
            return new EntryUpsertResult
            {
                Mapping = CreateMapping(entry.RawLine, "bereits vorhanden", existing, merged > 0 ? $"{merged} Pfad(e) ergänzt" : "keine Änderung"),
                ProfilesUpdated = merged > 0 ? 1 : 0,
                PathsMerged = merged
            };
        }

        if (discovered is not null)
        {
            var paths = ResolveDefaultPaths(discovered.DisplayName, discovered.InstallLocation);
            var profile = ProfileBindingFactory.CreateBoundProfile(
                discovered,
                paths,
                profiles.Count,
                profiles.Select(item => item.Id).ToHashSet());

            if (CursorSnapshotPaths.IsCursorProgramName(discovered.DisplayName))
            {
                profile.CursorSnapshotLevel = CursorSnapshotLevel.Standard;
            }

            profiles.Add(profile);

            return new EntryUpsertResult
            {
                Mapping = CreateMapping(entry.RawLine, "neu (Registry)", profile, $"{paths.Count} Standardpfade"),
                ProfilesAdded = 1
            };
        }

        var seedName = entry.DisplayNameHint ?? entry.SearchTerms[0];
        var seedPaths = ResolveDefaultPaths(seedName, installLocation: null);
        var seedProfile = ProfileBindingFactory.CreateBoundProfile(
            CreateSyntheticProgram(seedName),
            seedPaths,
            profiles.Count,
            profiles.Select(item => item.Id).ToHashSet());

        if (CursorSnapshotPaths.IsCursorProgramName(seedName))
        {
            seedProfile.CursorSnapshotLevel = CursorSnapshotLevel.Standard;
        }

        profiles.Add(seedProfile);

        return new EntryUpsertResult
        {
            Mapping = CreateMapping(
                entry.RawLine,
                "neu (Seed, nicht installiert)",
                seedProfile,
                "Registry-Treffer fehlt — Standardpfade gesetzt"),
            ProfilesAdded = 1,
            ManualNote = $"„{seedName}“: nicht in der Registry gefunden — Profil mit Standardpfaden angelegt, Installation ggf. manuell prüfen."
        };
    }

    private EntryUpsertResult UpsertCustomFolderEntry(
        IList<ProgramProfile> profiles,
        ZuSavenParsedEntry entry)
    {
        var folderPath = entry.FolderPath ?? string.Empty;
        var profileName = entry.DisplayNameHint ?? Path.GetFileName(folderPath.TrimEnd('\\', '/'));

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return new EntryUpsertResult
            {
                Mapping = new ZuSavenMappingEntry
                {
                    ZuSavenLine = entry.RawLine,
                    Action = "übersprungen",
                    ProfileName = profileName,
                    Notes = "Kein Ordnerpfad erkannt"
                },
                ManualNote = $"„{entry.RawLine}“: Ordnerpfad konnte nicht aufgelöst werden."
            };
        }

        var resolvedPath = ResolveFolderPath(profileName, folderPath);
        var exists = Directory.Exists(resolvedPath);

        var existing = FindMatchingCustomProfile(profiles, profileName, resolvedPath);
        if (existing is not null)
        {
            var merged = MergePath(existing, CreateFolderPathEntry(profileName, resolvedPath));
            return new EntryUpsertResult
            {
                Mapping = CreateMapping(
                    entry.RawLine,
                    exists ? "bereits vorhanden" : "bereits vorhanden (Pfad fehlt)",
                    existing,
                    merged > 0 ? $"{merged} Pfad ergänzt" : exists ? "keine Änderung" : $"Pfad fehlt: {resolvedPath}"),
                ProfilesUpdated = merged > 0 ? 1 : 0,
                PathsMerged = merged,
                ManualNote = exists ? null : $"„{profileName}“: Ordner nicht gefunden ({resolvedPath})"
            };
        }

        var paths = exists
            ? new List<ProfilePathEntry> { CreateFolderPathEntry(profileName, resolvedPath) }
            : new List<ProfilePathEntry>();

        if (paths.Count == 0)
        {
            return new EntryUpsertResult
            {
                Mapping = new ZuSavenMappingEntry
                {
                    ZuSavenLine = entry.RawLine,
                    Action = "manuell offen",
                    ProfileName = profileName,
                    Notes = $"Ordner nicht gefunden: {resolvedPath}"
                },
                ManualNote = $"„{profileName}“: Ordner nicht gefunden ({resolvedPath}) — bitte manuell per „Dateien & Ordner“ anlegen."
            };
        }

        var profile = ProfileBindingFactory.CreateCustomPathsProfile(
            profileName,
            paths,
            profiles.Count,
            profiles.Select(item => item.Id).ToHashSet());

        profiles.Add(profile);

        return new EntryUpsertResult
        {
            Mapping = CreateMapping(entry.RawLine, "neu (Dateien & Ordner)", profile, resolvedPath),
            ProfilesAdded = 1
        };
    }

    private static int RemoveRedundantCustomProfiles(IList<ProgramProfile> profiles)
    {
        var removed = 0;

        for (var index = profiles.Count - 1; index >= 0; index--)
        {
            var profile = profiles[index];
            if (!string.Equals(profile.Category, "Dateien & Ordner", StringComparison.Ordinal))
            {
                continue;
            }

            if (profile.Paths.Count != 1 || !profile.Paths[0].IsDirectory)
            {
                continue;
            }

            var folderPath = profile.Paths[0].SourcePath;
            var coveredByOther = profiles
                .Where((candidate, candidateIndex) => candidateIndex != index)
                .Any(candidate => candidate.Paths.Any(path =>
                    PathsRepresentSameLocation(path.SourcePath, folderPath)));

            if (!coveredByOther)
            {
                continue;
            }

            profiles.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    private static int MergeCopyQExports(IList<ProgramProfile> profiles, IList<ZuSavenMappingEntry> mappings)
    {
        var copyQProfile = profiles.FirstOrDefault(profile =>
            NormalizeTerm(profile.Name).Contains("copyq", StringComparison.Ordinal));

        if (copyQProfile is null)
        {
            return 0;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!Directory.Exists(documents))
        {
            return 0;
        }

        var merged = 0;
        foreach (var file in Directory.EnumerateFiles(documents, "*.cpq"))
        {
            merged += MergePath(copyQProfile, new ProfilePathEntry
            {
                Label = Path.GetFileName(file),
                SourcePath = file,
                RelativeTarget = $"export/{Path.GetFileName(file)}",
                IsDirectory = false
            });
        }

        if (merged > 0)
        {
            mappings.Add(new ZuSavenMappingEntry
            {
                ZuSavenLine = "CopyQ .cpq (Documents)",
                Action = "Pfade ergänzt",
                ProfileName = copyQProfile.Name,
                ProfileId = copyQProfile.Id,
                Notes = $"{merged} Export-Datei(en) aus Documents"
            });
        }

        return merged;
    }

    private static int MergeDefaultPaths(ProgramProfile profile, string displayName, string? installLocation)
    {
        var defaults = ResolveDefaultPaths(displayName, installLocation, profile);
        var merged = 0;

        foreach (var path in defaults)
        {
            merged += MergePath(profile, path);
        }

        if (merged > 0)
        {
            ProfileBindingFactory.ApplyPathsToProfile(profile, profile.Paths);
        }

        return merged;
    }

    private static IReadOnlyList<ProfilePathEntry> ResolveDefaultPaths(
        string displayName,
        string? installLocation,
        ProgramProfile? profile = null)
    {
        if (CursorSnapshotPaths.IsCursorProgramName(displayName)
            || (profile is not null && CursorSnapshotPaths.IsCursorProfile(profile)))
        {
            var level = profile is null
                ? CursorSnapshotLevel.Standard
                : CursorSnapshotPaths.NormalizeLevel(profile.CursorSnapshotLevel);
            return CursorSnapshotPaths.Resolve(level);
        }

        return KnownAppPathDefaults.Resolve(displayName, installLocation);
    }

    private static int MergePath(ProgramProfile profile, ProfilePathEntry path)
    {
        if (profile.Paths.Any(existing =>
                string.Equals(
                    NormalizePath(existing.SourcePath),
                    NormalizePath(path.SourcePath),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        profile.Paths.Add(ClonePath(path));
        ProfileBindingFactory.ApplyPathsToProfile(profile, profile.Paths);
        return 1;
    }

    private static ProfilePathEntry CreateFolderPathEntry(string label, string folderPath)
        => new()
        {
            Label = label,
            SourcePath = folderPath,
            RelativeTarget = Slugify(label),
            IsDirectory = true
        };

    private static string ResolveFolderPath(string profileName, string basePath)
    {
        var trimmed = basePath.Trim().TrimEnd('\\', '/');

        if (profileName.StartsWith(".", StringComparison.Ordinal))
        {
            return Path.Combine(trimmed, profileName);
        }

        if (string.Equals(profileName, "scripts", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(trimmed, "scripts");
        }

        if (string.Equals(profileName, "Powershell", StringComparison.OrdinalIgnoreCase)
            || string.Equals(profileName, "PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(trimmed, "PowerShell");
        }

        if (string.Equals(profileName, "NewPlusCyberpunk", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(trimmed, "NewPlusCyberpunk");
        }

        return Path.Combine(trimmed, profileName);
    }

    private static ProgramProfile? FindMatchingProgramProfile(
        IEnumerable<ProgramProfile> profiles,
        IReadOnlyList<string> searchTerms)
    {
        foreach (var profile in profiles)
        {
            var normalizedName = NormalizeTerm(profile.Name);
            if (searchTerms.Any(term => normalizedName.Contains(NormalizeTerm(term), StringComparison.Ordinal)))
            {
                return profile;
            }
        }

        return null;
    }

    private static ProgramProfile? FindMatchingCustomProfile(
        IEnumerable<ProgramProfile> profiles,
        string profileName,
        string folderPath)
    {
        var normalizedName = NormalizeTerm(profileName);
        var normalizedPath = NormalizePath(folderPath);

        return profiles.FirstOrDefault(profile =>
            profile.Paths.Any(path => PathsRepresentSameLocation(path.SourcePath, folderPath))
            || string.Equals(NormalizeTerm(profile.Name), normalizedName, StringComparison.Ordinal));
    }

    private static bool PathsRepresentSameLocation(string leftPath, string rightPath)
    {
        var left = NormalizePath(leftPath);
        var right = NormalizePath(rightPath);

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static DiscoveredProgram? FindInstalledProgram(
        IEnumerable<DiscoveredProgram> installedPrograms,
        IReadOnlyList<string> searchTerms)
    {
        foreach (var program in installedPrograms)
        {
            var normalizedName = NormalizeTerm(program.DisplayName);
            if (searchTerms.Any(term => normalizedName.Contains(NormalizeTerm(term), StringComparison.Ordinal)))
            {
                return program;
            }
        }

        return null;
    }

    private static DiscoveredProgram CreateSyntheticProgram(string displayName)
        => new()
        {
            DisplayName = displayName,
            Publisher = "zu saven",
            Scope = "seed"
        };

    private (string Content, string? SourceFile) ResolveZuSavenContent()
    {
        foreach (var candidate in GetZuSavenCandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return (File.ReadAllText(candidate), candidate);
            }
            catch (IOException)
            {
                // try next candidate
            }
        }

        return (EmbeddedZuSaven, null);
    }

    private IEnumerable<string> GetZuSavenCandidatePaths()
    {
        var envPath = Environment.GetEnvironmentVariable("HOROSSAVER_ZU_SAVEN_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            yield return envPath;
        }

        yield return Path.Combine(_paths.DataRoot, "zu-saven.txt");
        yield return Path.Combine(_paths.DataRoot, "zu saven");
        yield return Path.Combine(_paths.AppDirectory, "zu-saven.txt");
        yield return Path.Combine(_paths.AppDirectory, "zu saven");

        foreach (var ancestor in EnumerateAncestors(AppContext.BaseDirectory, maxDepth: 8))
        {
            yield return Path.Combine(ancestor, "zu saven");
            yield return Path.Combine(ancestor, "zu-saven.txt");
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string startDirectory, int maxDepth)
    {
        var current = startDirectory;
        for (var depth = 0; depth < maxDepth && !string.IsNullOrWhiteSpace(current); depth++)
        {
            yield return current;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            current = parent;
        }
    }

    private static List<ZuSavenParsedEntry> ParseZuSavenContent(string content)
    {
        var entries = new List<ZuSavenParsedEntry>();

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("programm ", StringComparison.OrdinalIgnoreCase))
            {
                var name = line["programm ".Length..].Trim();
                entries.Add(new ZuSavenParsedEntry
                {
                    RawLine = line,
                    Kind = ZuSavenEntryKind.Program,
                    DisplayNameHint = name,
                    SearchTerms = BuildProgramSearchTerms(name)
                });
                continue;
            }

            var folderMatch = FolderPathRegex.Match(line);
            if (folderMatch.Success)
            {
                entries.Add(new ZuSavenParsedEntry
                {
                    RawLine = line,
                    Kind = ZuSavenEntryKind.CustomFolder,
                    DisplayNameHint = folderMatch.Groups["name"].Value.Trim(),
                    FolderPath = folderMatch.Groups["path"].Value.Trim()
                });
                continue;
            }

            entries.Add(new ZuSavenParsedEntry
            {
                RawLine = line,
                Kind = ZuSavenEntryKind.Program,
                DisplayNameHint = line,
                SearchTerms = BuildProgramSearchTerms(line)
            });
        }

        return entries;
    }

    private static List<ZuSavenParsedEntry> DeduplicateEntries(IEnumerable<ZuSavenParsedEntry> entries)
    {
        var result = new List<ZuSavenParsedEntry>();
        var seenProgramKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFolderKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.Kind == ZuSavenEntryKind.CustomFolder)
            {
                var folderKey = $"{NormalizeTerm(entry.DisplayNameHint ?? string.Empty)}|{entry.FolderPath}";
                if (!seenFolderKeys.Add(folderKey))
                {
                    continue;
                }

                result.Add(entry);
                continue;
            }

            var programKey = string.Join('|', entry.SearchTerms.Select(NormalizeTerm).OrderBy(term => term));
            if (!seenProgramKeys.Add(programKey))
            {
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    private static IReadOnlyList<string> BuildProgramSearchTerms(string value)
    {
        var normalized = NormalizeTerm(value);
        var terms = new List<string> { normalized };

        if (normalized.Contains("copy q", StringComparison.Ordinal) || normalized == "copyq")
        {
            terms.Add("copyq");
        }

        if (normalized.Contains("mem reduct", StringComparison.Ordinal))
        {
            terms.Add("mem reduct");
        }

        if (normalized is "powershell" or "powershell 7")
        {
            terms.Add("powershell");
        }

        if (normalized.Contains("outlook", StringComparison.Ordinal))
        {
            terms.Add("outlook");
            terms.Add("microsoft outlook");
        }

        if (normalized.Contains("whatsapp", StringComparison.Ordinal))
        {
            terms.Add("whatsapp");
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ZuSavenMappingEntry CreateMapping(
        string rawLine,
        string action,
        ProgramProfile profile,
        string notes)
        => new()
        {
            ZuSavenLine = rawLine,
            Action = action,
            ProfileName = profile.Name,
            ProfileId = profile.Id,
            Notes = notes
        };

    private static string NormalizeTerm(string value)
        => value.Trim().ToLower(CultureInfo.InvariantCulture);

    private static string NormalizePath(string value)
        => value.Trim().TrimEnd('\\', '/');

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "folder" : slug;
    }

    private static ProfilePathEntry ClonePath(ProfilePathEntry entry)
        => new()
        {
            Label = entry.Label,
            SourcePath = entry.SourcePath,
            RelativeTarget = entry.RelativeTarget,
            IsDirectory = entry.IsDirectory
        };

    private enum ZuSavenEntryKind
    {
        Program,
        CustomFolder
    }

    private sealed class ZuSavenParsedEntry
    {
        public string RawLine { get; init; } = string.Empty;
        public ZuSavenEntryKind Kind { get; init; }
        public string? DisplayNameHint { get; init; }
        public IReadOnlyList<string> SearchTerms { get; init; } = [];
        public string? FolderPath { get; init; }
    }

    private sealed class EntryUpsertResult
    {
        public ZuSavenMappingEntry Mapping { get; init; } = new();
        public int ProfilesAdded { get; init; }
        public int ProfilesUpdated { get; init; }
        public int PathsMerged { get; init; }
        public string? ManualNote { get; init; }
    }
}
