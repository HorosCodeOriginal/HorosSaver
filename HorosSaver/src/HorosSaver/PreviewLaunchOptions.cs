namespace HorosSaver;

public static class PreviewLaunchOptions
{
    public static string? PreviewRegion { get; private set; }
    public static string? CaptureOutputPath { get; private set; }
    public static bool ToolbarCollapsed { get; private set; }

    public static void Parse(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--preview" when i + 1 < args.Length:
                    PreviewRegion = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    CaptureOutputPath = args[++i];
                    break;
                case "--toolbar-collapsed":
                    ToolbarCollapsed = true;
                    break;
            }
        }
    }

    public static bool IsPreviewMode => !string.IsNullOrWhiteSpace(PreviewRegion);

    public static bool IsCaptureOnlyMode =>
        !string.IsNullOrWhiteSpace(CaptureOutputPath) && !IsPreviewMode;
}
