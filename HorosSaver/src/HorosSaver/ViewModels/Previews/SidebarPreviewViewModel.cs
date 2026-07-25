using HorosSaver.ViewModels.Regions;

namespace HorosSaver.ViewModels.Previews;

public sealed class SidebarPreviewViewModel : SidebarRegionViewModel
{
    private SidebarPreviewViewModel()
        : base(new PreviewNavigationHost())
    {
    }

    public static SidebarPreviewViewModel DesignInstance { get; } = new();

    private sealed class PreviewNavigationHost : INavigationHost
    {
        public void NavigateTo(string viewId)
        {
        }
    }
}
