using System.Text;

namespace HorosSaver.Services;

public static class AppFileLogger
{
    private static readonly object Sync = new();
    private static string? _logsRoot;

    public static void Initialize(string logsRoot)
    {
        _logsRoot = logsRoot;
        Directory.CreateDirectory(logsRoot);
        Info("HorosSaver gestartet.");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
        => Write("ERROR", message, exception);

    public static void LogUnhandledException(Exception exception, string source)
        => Write("FATAL", $"Unhandled exception ({source})", exception);

    private static void Write(string level, string message, Exception? exception = null)
    {
        if (_logsRoot is null)
        {
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var builder = new StringBuilder()
                .Append('[').Append(timestamp).Append("] [")
                .Append(level).Append("] ")
                .AppendLine(message);

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            var logFile = Path.Combine(_logsRoot, $"horossaver-{DateTime.Now:yyyyMMdd}.log");
            lock (Sync)
            {
                Directory.CreateDirectory(_logsRoot);
                File.AppendAllText(logFile, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
