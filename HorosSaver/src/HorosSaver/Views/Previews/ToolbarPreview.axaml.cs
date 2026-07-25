using Avalonia.Controls;
using HorosSaver.ViewModels.Previews;

namespace HorosSaver.Views.Previews;

public partial class ToolbarPreview : Window
{
    public ToolbarPreview()
    {
        InitializeComponent();
        DataContext ??= ToolbarPreviewViewModel.DesignInstance;
        ToolbarRegion.Width = 1040;
        ToolbarRegion.Height = 150;
    }
}
