using Avalonia.Controls;
using HorosSaver.ViewModels.Previews;

namespace HorosSaver.Views.Previews;

public partial class SidebarPreview : Window
{
    public SidebarPreview()
    {
        InitializeComponent();
        DataContext ??= SidebarPreviewViewModel.DesignInstance;
        SidebarRegion.Height = 720;
        SidebarRegion.Width = 240;
    }
}
