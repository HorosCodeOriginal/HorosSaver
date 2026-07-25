namespace HorosSaver.Models;

public enum AppReinstallEngineAction
{
    Doctor,
    Capture,
    Validate,
    Initialize,
    Status
}

public sealed class EngineExecutionResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool EngineMissing { get; init; }

    public static EngineExecutionResult Missing(string message)
        => new()
        {
            Success = false,
            ExitCode = -1,
            Message = message,
            EngineMissing = true
        };

    public static EngineExecutionResult FromProcess(
        int exitCode,
        string stdout,
        string stderr,
        string actionLabel)
        => new()
        {
            ExitCode = exitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            Success = exitCode is >= 0 and <= 7,
            Message = exitCode is >= 0 and <= 7
                ? $"{actionLabel} abgeschlossen (Exit {exitCode})."
                : $"{actionLabel} fehlgeschlagen (Exit {exitCode})."
        };
}

public sealed class EngineAvailability
{
    public bool IsAvailable { get; init; }
    public string? EngineRoot { get; init; }
    public string? ScriptPath { get; init; }
    public string? PowerShellPath { get; init; }
    public string Message { get; init; } = string.Empty;
}
