using Avalonia.Controls;
using HorosSaver.ViewModels.Previews;

namespace HorosSaver.Views.Previews;

public partial class ProgramsGridPreview : Window
{
    public ProgramsGridPreview()
    {
        InitializeComponent();
        DataContext ??= ProgramsGridPreviewViewModel.DesignInstance;
        ProgramsRegion.Width = 700;
        ProgramsRegion.Height = 1040;
    }
}
