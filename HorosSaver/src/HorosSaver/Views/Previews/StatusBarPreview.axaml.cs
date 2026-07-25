using Avalonia.Controls;
using HorosSaver.ViewModels.Previews;

namespace HorosSaver.Views.Previews;

public partial class StatusBarPreview : Window
{
    public StatusBarPreview()
    {
        InitializeComponent();
        DataContext ??= StatusBarPreviewViewModel.DesignInstance;
        StatusBarRegion.Width = 1040;
        StatusBarRegion.Height = 48;
    }
}
