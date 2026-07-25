using Avalonia.Controls;
using HorosSaver.ViewModels;

namespace HorosSaver.Views;

public partial class ProfilePathsWizardWindow : Window
{
    public ProfilePathsWizardWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is ProfilePathsWizardViewModel viewModel)
        {
            viewModel.HostWindow = this;
        }
    }
}
