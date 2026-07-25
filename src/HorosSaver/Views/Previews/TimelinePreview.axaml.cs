using Avalonia.Controls;
using HorosSaver.ViewModels.Previews;

namespace HorosSaver.Views.Previews;

public partial class TimelinePreview : Window
{
    public TimelinePreview()
    {
        InitializeComponent();
        DataContext ??= TimelinePreviewViewModel.DesignInstance;
        TimelineRegion.Width = 360;
        TimelineRegion.Height = 760;
    }
}
