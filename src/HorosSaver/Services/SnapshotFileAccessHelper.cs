namespace HorosSaver.Services;

internal static class SnapshotFileAccessHelper
{
    public const string LockedSkippedReason = "locked";

    public static bool IsFileLockedException(Exception exception)
    {
        if (exception is not IOException and not UnauthorizedAccessException)
        {
            return false;
        }

        const int sharingViolationHResult = unchecked((int)0x80070020);
        if (exception.HResult == sharingViolationHResult)
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
               || message.Contains("wird von einem anderen Prozess verwendet", StringComparison.OrdinalIgnoreCase)
               || message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Zugriff auf den Pfad", StringComparison.OrdinalIgnoreCase)
               || message.Contains("because it is being used", StringComparison.OrdinalIgnoreCase)
               || message.Contains("sharing violation", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Freigabeverletzung", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Die Datei wird durch einen anderen Prozess verwendet", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatLockedSkippedItem(string fullPath)
        => $"{LockedSkippedReason}: {fullPath}";
}
