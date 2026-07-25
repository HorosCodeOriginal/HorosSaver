using System.Diagnostics;
using Avalonia.Controls;
using HorosSaver.Views;

namespace HorosSaver.Services;

public static class DesktopShellHelper
{
    public static bool TryOpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var target = path;
        if (File.Exists(target))
        {
            target = Path.GetDirectoryName(target) ?? target;
        }

        if (!Directory.Exists(target))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        return true;
    }

    public static Task<bool> TryCopyTextAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(false);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        try
        {
            var escaped = text.Replace("'", "''");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Set-Clipboard -Value '{escaped}'\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit();
            return Task.FromResult(process?.ExitCode == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public static async Task<bool> ConfirmAsync(Window? owner, string title, string message)
    {
        var result = await ConfirmWithOptionsAsync(owner, title, message).ConfigureAwait(true);
        return result.IsConfirmed;
    }

    public static async Task<ConfirmDialogResult> ConfirmWithOptionsAsync(
        Window? owner,
        string title,
        string message,
        ConfirmDialogOptions? options = null)
    {
        var dialog = new ConfirmDialogWindow
        {
            Title = title
        };
        dialog.SetMessage(message, options);

        if (owner is not null)
        {
            var confirmed = await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
            return new ConfirmDialogResult(confirmed, dialog.DeleteSnapshotsChecked);
        }

        var result = false;
        dialog.Confirmed += () => result = true;
        dialog.Show();
        await Task.Delay(50).ConfigureAwait(true);
        return new ConfirmDialogResult(result, dialog.DeleteSnapshotsChecked);
    }
}
