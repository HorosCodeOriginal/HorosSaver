using Avalonia.Controls;
using Avalonia.Interactivity;
using HorosSaver.Services;

namespace HorosSaver.Views;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    public event Action? Confirmed;

    public bool DeleteSnapshotsChecked => DeleteSnapshotsCheckBox.IsChecked == true;

    public void SetMessage(string message, ConfirmDialogOptions? options = null)
    {
        MessageText.Text = message;

        if (options is null)
        {
            DeleteSnapshotsCheckBox.IsVisible = false;
            DeleteSnapshotsCheckBox.IsChecked = true;
            ConfirmButton.Content = "Bestätigen";
            return;
        }

        DeleteSnapshotsCheckBox.IsVisible = options.ShowDeleteSnapshotsOption;
        DeleteSnapshotsCheckBox.IsChecked = options.DeleteSnapshotsDefault;
        DeleteSnapshotsCheckBox.Content = options.DeleteSnapshotsLabel;
        ConfirmButton.Content = options.ConfirmButtonText;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke();
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
