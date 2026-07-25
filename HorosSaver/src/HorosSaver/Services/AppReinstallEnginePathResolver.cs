namespace HorosSaver.Services;

using HorosSaver.Models;

public interface IAppReinstallEnginePathResolver
{
    string? EngineRoot { get; }
    string? AppReinstallScriptPath { get; }
    bool IsAvailable { get; }
    void Reload(string? configuredOverride = null, bool useSavedSettings = true);
    EngineAvailability DescribeAvailability();
}

public sealed class AppReinstallEnginePathResolver : IAppReinstallEnginePathResolver
{
    public const string HorosSaverEngineRootEnvironmentVariable = "HOROSSAVER_ENGINE_ROOT";
    public const string HorosReviveEngineRootEnvironmentVariable = "HOROSREVIVE_ENGINE_ROOT";

    private readonly IAppSettingsService _settingsService;

    public AppReinstallEnginePathResolver(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
        Reload();
    }

    public string? EngineRoot { get; private set; }

    public string? AppReinstallScriptPath =>
        EngineRoot is null ? null : Path.Combine(EngineRoot, "scripts", "AppReinstall.ps1");

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(AppReinstallScriptPath) && File.Exists(AppReinstallScriptPath);

    public void Reload(string? configuredOverride = null, bool useSavedSettings = true)
    {
        var effectiveOverride = configuredOverride;
        if (effectiveOverride is null && useSavedSettings)
        {
            effectiveOverride = _settingsService.Current.EngineRootPath;
        }

        EngineRoot = ResolveEngineRoot(effectiveOverride);
    }

    public EngineAvailability DescribeAvailability()
    {
        var pwsh = EngineProcessService.ResolvePowerShellExecutable();
        if (pwsh is null)
        {
            return new EngineAvailability
            {
                IsAvailable = false,
                EngineRoot = EngineRoot,
                ScriptPath = AppReinstallScriptPath,
                Message = "PowerShell 7 (pwsh) nicht gefunden — bitte installieren."
            };
        }

        if (!IsAvailable)
        {
            return new EngineAvailability
            {
                IsAvailable = false,
                EngineRoot = EngineRoot,
                ScriptPath = AppReinstallScriptPath,
                PowerShellPath = pwsh,
                Message = "Engine nicht gefunden — Pfad in Einstellungen setzen oder repos/app-reinstall-workflow bereitstellen."
            };
        }

        return new EngineAvailability
        {
            IsAvailable = true,
            EngineRoot = EngineRoot,
            ScriptPath = AppReinstallScriptPath,
            PowerShellPath = pwsh,
            Message = $"Engine bereit: {EngineRoot}"
        };
    }

    private static string? ResolveEngineRoot(string? configuredOverride)
    {
        if (TryValidateRoot(configuredOverride, out var configuredRoot))
        {
            return configuredRoot;
        }

        var horosSaverEnv = Environment.GetEnvironmentVariable(HorosSaverEngineRootEnvironmentVariable);
        if (TryValidateRoot(horosSaverEnv, out var horosSaverRoot))
        {
            return horosSaverRoot;
        }

        var horosReviveEnv = Environment.GetEnvironmentVariable(HorosReviveEngineRootEnvironmentVariable);
        if (TryValidateRoot(horosReviveEnv, out var horosReviveRoot))
        {
            return horosReviveRoot;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var repoCandidate = Path.Combine(current.FullName, "repos", "app-reinstall-workflow");
            if (TryValidateRoot(repoCandidate, out var repoRoot))
            {
                return repoRoot;
            }

            var engineCandidate = Path.Combine(current.FullName, "engine");
            if (TryValidateRoot(engineCandidate, out var engineRoot))
            {
                return engineRoot;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool TryValidateRoot(string? candidate, out string? normalizedRoot)
    {
        normalizedRoot = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(candidate.Trim());
        var scriptPath = Path.Combine(fullPath, "scripts", "AppReinstall.ps1");
        if (!File.Exists(scriptPath))
        {
            return false;
        }

        normalizedRoot = fullPath;
        return true;
    }
}
