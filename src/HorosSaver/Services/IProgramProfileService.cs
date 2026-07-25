using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IProgramProfileService
{
    Task<IReadOnlyList<ProgramProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default);
    Task<ProfileStoreData> LoadProfileStoreAsync(CancellationToken cancellationToken = default);
    Task SaveProfilesAsync(IEnumerable<ProgramProfile> profiles, CancellationToken cancellationToken = default);
    Task SaveProfileStoreAsync(
        IEnumerable<ProgramProfile> profiles,
        IEnumerable<ProgramGroup> groups,
        CancellationToken cancellationToken = default);
    Task UpdateSortOrderAsync(IEnumerable<ProgramProfile> orderedProfiles, CancellationToken cancellationToken = default);
    IReadOnlyList<ProgramGroup> AutoDetectGroups(IReadOnlyList<ProgramProfile> profiles);
    void ApplyAutoGroups(IReadOnlyList<ProgramProfile> profiles, IReadOnlyList<ProgramGroup> groups);
}
