namespace HorosSaver.Services;

public static class SnapshotErrorFormatter
{
    private const int MaxUiLength = 120;

    public static string FormatForUi(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Snapshot fehlgeschlagen.";
        }

        if (IsFileLockedMessage(message))
        {
            var path = TryExtractQuotedPath(message);
            return path is not null
                ? $"Datei gesperrt: {Path.GetFileName(path)}"
                : "Datei gesperrt — Details im Log.";
        }

        return message.Length <= MaxUiLength
            ? message
            : message[..(MaxUiLength - 1)] + "…";
    }

    private static bool IsFileLockedMessage(string message)
        => message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
           || message.Contains("wird von einem anderen Prozess verwendet", StringComparison.OrdinalIgnoreCase)
           || message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase)
           || message.Contains("Zugriff auf den Pfad", StringComparison.OrdinalIgnoreCase)
           || message.Contains("because it is being used", StringComparison.OrdinalIgnoreCase);

    private static string? TryExtractQuotedPath(string message)
    {
        var quoteStart = message.IndexOf('\'', StringComparison.Ordinal);
        if (quoteStart < 0)
        {
            return null;
        }

        var quoteEnd = message.IndexOf('\'', quoteStart + 1);
        if (quoteEnd <= quoteStart)
        {
            return null;
        }

        var path = message[(quoteStart + 1)..quoteEnd];
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
