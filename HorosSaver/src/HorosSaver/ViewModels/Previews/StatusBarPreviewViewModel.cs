using HorosSaver.ViewModels.Regions;

namespace HorosSaver.ViewModels.Previews;

public sealed class StatusBarPreviewViewModel : StatusBarRegionViewModel
{
    private StatusBarPreviewViewModel()
    {
        LeftLabel = "8 Profile geladen · Cursor ausgewählt";
        ShellLabel = "PowerShell 7.4";
        OsLabel = "Windows 11";
    }

    public static StatusBarPreviewViewModel DesignInstance { get; } = new();
}
