using Avalonia.Controls;
using Avalonia.Input;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.Views.Regions;

public partial class TimelineRegion : UserControl
{
    public TimelineRegion()
    {
        InitializeComponent();
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        if (sender is not Button { DataContext: SnapshotItemViewModel snapshot }
            || DataContext is not TimelineRegionViewModel viewModel)
        {
            return;
        }

        viewModel.SelectSnapshotCommand.Execute(snapshot);
    }
}
