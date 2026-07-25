using Avalonia.Controls;
using Avalonia.Input;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Regions;

namespace HorosSaver.Views.Regions;

public partial class ProgramsRegion : UserControl
{
    public ProgramsRegion()
    {
        InitializeComponent();
    }

    private void OnProgramCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control
            || control.DataContext is not ProgramProfileItemViewModel program
            || DataContext is not ProgramsRegionViewModel regionViewModel)
        {
            return;
        }

        if (regionViewModel.EditProfilePathsCommand.CanExecute(program))
        {
            regionViewModel.EditProfilePathsCommand.Execute(program);
            e.Handled = true;
        }
    }

    private void OnProgramPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        if (sender is not Border { DataContext: ProgramProfileItemViewModel program }
            || DataContext is not ProgramsRegionViewModel viewModel)
        {
            return;
        }

        viewModel.SelectProgramCommand.Execute(program);
    }
}
