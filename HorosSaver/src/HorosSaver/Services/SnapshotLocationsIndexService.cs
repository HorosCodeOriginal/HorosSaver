using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public interface ISnapshotLocationsIndexService
{
    Task<SnapshotLocationsDocument> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SnapshotLocationsDocument document, CancellationToken cancellationToken = default);
    Task<string?> TryGetSnapshotPathAsync(
        string programId,
        string snapshotId,
        CancellationToken cancellationToken = default);
    string? TryGetSnapshotPath(string programId, string snapshotId);
    Task SetSnapshotPathAsync(
        string programId,
        string snapshotId,
        string absolutePath,
        CancellationToken cancellationToken = default);
    Task RemoveSnapshotAsync(string programId, string snapshotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SnapshotLocationEntry>> GetProgramEntriesAsync(
        string programId,
        CancellationToken cancellationToken = default);
}

public sealed class SnapshotLocationsIndexService : ISnapshotLocationsIndexService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStoragePathResolver _paths;

    public SnapshotLocationsIndexService(IStoragePathResolver paths)
    {
        _paths = paths;
    }

    public async Task<SnapshotLocationsDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        var filePath = _paths.SnapshotLocationsIndexPath;

        if (!File.Exists(filePath))
        {
            return new SnapshotLocationsDocument();
        }

        await using var stream = File.OpenRead(filePath);
        var document = await JsonSerializer.DeserializeAsync<SnapshotLocationsDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return document ?? new SnapshotLocationsDocument();
    }

    public async Task SaveAsync(SnapshotLocationsDocument document, CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        var filePath = _paths.SnapshotLocationsIndexPath;

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> TryGetSnapshotPathAsync(
        string programId,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var entry = document.Locations.FirstOrDefault(item =>
            string.Equals(item.ProgramId, programId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase));

        return entry?.AbsolutePath;
    }

    public string? TryGetSnapshotPath(string programId, string snapshotId)
    {
        if (!File.Exists(_paths.SnapshotLocationsIndexPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_paths.SnapshotLocationsIndexPath);
            var document = JsonSerializer.Deserialize<SnapshotLocationsDocument>(json, JsonOptions);
            var entry = document?.Locations.FirstOrDefault(item =>
                string.Equals(item.ProgramId, programId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase));

            return entry?.AbsolutePath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SetSnapshotPathAsync(
        string programId,
        string snapshotId,
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
        document.Locations.RemoveAll(item =>
            string.Equals(item.ProgramId, programId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase));

        document.Locations.Add(new SnapshotLocationEntry
        {
            ProgramId = programId,
            SnapshotId = snapshotId,
            AbsolutePath = absolutePath
        });

        await SaveAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveSnapshotAsync(string programId, string snapshotId, CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var removed = document.Locations.RemoveAll(item =>
            string.Equals(item.ProgramId, programId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<SnapshotLocationEntry>> GetProgramEntriesAsync(
        string programId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return document.Locations
            .Where(item => string.Equals(item.ProgramId, programId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
