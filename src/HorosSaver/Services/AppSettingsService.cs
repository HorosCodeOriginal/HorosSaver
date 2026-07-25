using System.Text.Json;
using System.Text.Json.Serialization;
using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IStoragePathResolver _paths;
    private AppSettings _current = new();

    public AppSettingsService(IStoragePathResolver paths)
    {
        _paths = paths;
    }

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();

        if (!File.Exists(_paths.SettingsFilePath))
        {
            _current = new AppSettings();
            await SaveAsync(_current, cancellationToken).ConfigureAwait(false);
            return _current;
        }

        await using var stream = File.OpenRead(_paths.SettingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        _current = settings ?? new AppSettings();
        return _current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureDataDirectories();
        _current = settings;

        await using var stream = File.Create(_paths.SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
