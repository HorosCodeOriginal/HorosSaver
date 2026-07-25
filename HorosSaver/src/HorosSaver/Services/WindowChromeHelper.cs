using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace HorosSaver.Services;

internal static class WindowChromeHelper
{
    public static void TryEnableImmersiveDarkMode(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
        if (handle == nint.Zero)
        {
            return;
        }

        const int DwmwaUseImmersiveDarkMode = 20;
        const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        var attribute = Environment.OSVersion.Version.Build >= 18985
            ? DwmwaUseImmersiveDarkMode
            : DwmwaUseImmersiveDarkModeBefore20H1;

        var useDark = 1;
        _ = DwmSetWindowAttribute(handle, attribute, ref useDark, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);
}
