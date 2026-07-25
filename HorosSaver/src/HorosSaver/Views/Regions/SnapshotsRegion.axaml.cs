using Avalonia.Controls;
using Avalonia.Input;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.Views.Regions;

public partial class SnapshotsRegion : UserControl
{
    public SnapshotsRegion()
    {
        InitializeComponent();
    }

    private void OnSnapshotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        if (sender is not Button { DataContext: SnapshotOverviewItemViewModel snapshot }
            || DataContext is not SnapshotsRegionViewModel viewModel)
        {
            return;
        }

        viewModel.SelectSnapshotCommand.Execute(snapshot);
    }

    private void OnCheckboxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }
}
