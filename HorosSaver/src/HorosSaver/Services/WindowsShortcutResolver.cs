using System.Runtime.InteropServices;

namespace HorosSaver.Services;

internal readonly record struct ShortcutInfo(
    string TargetPath,
    string? WorkingDirectory,
    string? Arguments);

internal static class WindowsShortcutResolver
{
    public static bool TryResolve(string shortcutPath, out ShortcutInfo info)
    {
        info = default;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(shortcutPath))
        {
            return false;
        }

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(Path.GetFullPath(shortcutPath));
            var targetPath = ((string?)link.TargetPath)?.Trim();

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            info = new ShortcutInfo(
                targetPath,
                ((string?)link.WorkingDirectory)?.Trim(),
                ((string?)link.Arguments)?.Trim());

            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (System.Reflection.TargetInvocationException)
        {
            return false;
        }
    }
}
