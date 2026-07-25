using Avalonia.Controls;

namespace HorosSaver.Views;

public partial class SnapshotCaptureTargetWindow : Window
{
    public SnapshotCaptureTargetWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is ViewModels.SnapshotCaptureTargetViewModel viewModel)
        {
            viewModel.HostWindow = this;
        }
    }
}
