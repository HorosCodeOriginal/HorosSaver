using HorosSaver.Models;

namespace HorosSaver.Services;

public sealed class ProfileStoreData
{
    public IReadOnlyList<ProgramProfile> Profiles { get; init; } = [];
    public IReadOnlyList<ProgramGroup> Groups { get; init; } = [];
}
