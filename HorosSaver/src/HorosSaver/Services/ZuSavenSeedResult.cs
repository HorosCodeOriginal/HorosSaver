namespace HorosSaver.Services;

public sealed class ZuSavenSeedResult
{
    public bool Changed { get; init; }
    public int ProfilesAdded { get; init; }
    public int ProfilesUpdated { get; init; }
    public int PathsMerged { get; init; }
    public IReadOnlyList<ZuSavenMappingEntry> Mappings { get; init; } = [];
    public IReadOnlyList<string> ManualItems { get; init; } = [];
    public string? SourceFile { get; init; }
}

public sealed class ZuSavenMappingEntry
{
    public string ZuSavenLine { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}
