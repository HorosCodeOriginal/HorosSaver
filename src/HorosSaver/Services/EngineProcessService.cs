using System.Diagnostics;
using System.Text;

namespace HorosSaver.Services;

public interface IEngineProcessService
{
    Task<ProcessExecutionResult> RunPowerShellScriptAsync(
        string scriptPath,
        string workingDirectory,
        IReadOnlyList<string> argumentList,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class EngineProcessService : IEngineProcessService
{
    public async Task<ProcessExecutionResult> RunPowerShellScriptAsync(
        string scriptPath,
        string workingDirectory,
        IReadOnlyList<string> argumentList,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        var pwsh = ResolvePowerShellExecutable()
            ?? throw new FileNotFoundException("PowerShell 7 (pwsh) wurde nicht gefunden.");

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Skript nicht gefunden.", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pwsh,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data))
            {
                return;
            }

            stdoutBuilder.AppendLine(args.Data);
            logProgress?.Report(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data))
            {
                return;
            }

            stderrBuilder.AppendLine(args.Data);
            logProgress?.Report($"[stderr] {args.Data}");
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("PowerShell-Prozess konnte nicht gestartet werden.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessExecutionResult(
            process.ExitCode,
            stdoutBuilder.ToString().TrimEnd(),
            stderrBuilder.ToString().TrimEnd());
    }

    public static string? ResolvePowerShellExecutable()
    {
        var pathCandidate = FindOnPath("pwsh.exe");
        if (pathCandidate is not null)
        {
            return pathCandidate;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var defaultPwsh = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        if (File.Exists(defaultPwsh))
        {
            return defaultPwsh;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var x86Pwsh = Path.Combine(programFilesX86, "PowerShell", "7", "pwsh.exe");
        return File.Exists(x86Pwsh) ? x86Pwsh : null;
    }

    private static string? FindOnPath(string executable)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var folder in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
