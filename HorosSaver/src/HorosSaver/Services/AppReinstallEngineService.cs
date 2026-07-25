using HorosSaver.Models;

namespace HorosSaver.Services;

public interface IAppReinstallEngineService
{
    EngineAvailability DescribeAvailability();
    Task<EngineExecutionResult> RunActionAsync(
        AppReinstallEngineAction action,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default);
}

public sealed class AppReinstallEngineService : IAppReinstallEngineService
{
    private readonly IAppReinstallEnginePathResolver _pathResolver;
    private readonly IEngineProcessService _processService;

    public AppReinstallEngineService(
        IAppReinstallEnginePathResolver pathResolver,
        IEngineProcessService processService)
    {
        _pathResolver = pathResolver;
        _processService = processService;
    }

    public EngineAvailability DescribeAvailability()
        => _pathResolver.DescribeAvailability();

    public async Task<EngineExecutionResult> RunActionAsync(
        AppReinstallEngineAction action,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        var availability = DescribeAvailability();
        if (!availability.IsAvailable
            || string.IsNullOrWhiteSpace(availability.EngineRoot)
            || string.IsNullOrWhiteSpace(availability.ScriptPath))
        {
            return EngineExecutionResult.Missing(availability.Message);
        }

        var actionLabel = GetActionLabel(action);
        logProgress?.Report($"=> {actionLabel} ({availability.ScriptPath})");

        try
        {
            var result = await _processService.RunPowerShellScriptAsync(
                availability.ScriptPath,
                availability.EngineRoot,
                ["-Action", action.ToString()],
                logProgress,
                cancellationToken).ConfigureAwait(false);

            return EngineExecutionResult.FromProcess(
                result.ExitCode,
                result.StandardOutput,
                result.StandardError,
                actionLabel);
        }
        catch (OperationCanceledException)
        {
            return new EngineExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Message = $"{actionLabel} abgebrochen."
            };
        }
        catch (Exception ex)
        {
            return new EngineExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Message = $"{actionLabel} fehlgeschlagen: {ex.Message}",
                StandardError = ex.Message
            };
        }
    }

    private static string GetActionLabel(AppReinstallEngineAction action)
        => action switch
        {
            AppReinstallEngineAction.Doctor => "Engine Doctor",
            AppReinstallEngineAction.Capture => "Inventar erfassen",
            AppReinstallEngineAction.Validate => "Katalog validieren",
            AppReinstallEngineAction.Initialize => "Katalog initialisieren",
            AppReinstallEngineAction.Status => "Engine-Status",
            _ => action.ToString()
        };
}
