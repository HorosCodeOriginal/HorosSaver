using Avalonia.Controls;
using HorosSaver.ViewModels;

namespace HorosSaver.Views;

public partial class BindProgramWizardWindow : Window
{
    public BindProgramWizardWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is BindProgramWizardViewModel viewModel)
        {
            viewModel.HostWindow = this;
            await viewModel.InitializeAsync().ConfigureAwait(true);
            RefreshProgramsListScrollLayout();
            SearchBox.Focus();
        }
    }

    private void RefreshProgramsListScrollLayout()
    {
        ProgramsListScroll.InvalidateMeasure();
        ProgramsListScroll.InvalidateArrange();
        if (ProgramsListScroll.Content is Control content)
        {
            content.InvalidateMeasure();
            content.InvalidateArrange();
        }
    }
}
