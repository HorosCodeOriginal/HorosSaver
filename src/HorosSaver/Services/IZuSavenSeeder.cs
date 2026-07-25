using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IZuSavenSeeder
{
    Task<ZuSavenSeedResult> ApplyAsync(
        IList<ProgramProfile> profiles,
        CancellationToken cancellationToken = default);
}
