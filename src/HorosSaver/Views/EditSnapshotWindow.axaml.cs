using Avalonia.Controls;

namespace HorosSaver.Views;

public partial class EditSnapshotWindow : Window
{
    public EditSnapshotWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is ViewModels.EditSnapshotViewModel viewModel)
        {
            viewModel.HostWindow = this;
        }
    }
}
