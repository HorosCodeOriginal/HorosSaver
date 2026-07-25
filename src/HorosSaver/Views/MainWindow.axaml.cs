using Avalonia.Controls;
using Avalonia.Input;
using HorosSaver.Behaviors;
using HorosSaver.Services;
using HorosSaver.ViewModels;

namespace HorosSaver.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        KeyDown += OnWindowKeyDown;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        WindowState = WindowState.Maximized;
        TryEnableImmersiveDarkMode();
        ApplyDetailPanelWidth();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        PersistDetailPanelWidth();
    }

    private void OnDetailPanelSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        PersistDetailPanelWidth();
    }

    private void PersistDetailPanelWidth()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SaveDetailPanelWidth(ReadDetailPanelWidth());
        _ = viewModel.PersistDetailPanelWidthAsync();
    }

    private double ReadDetailPanelWidth()
        => ProgrammeTimelineRegion.Bounds.Width > 0
            ? ProgrammeTimelineRegion.Bounds.Width
            : 360;

    public void ApplyDetailPanelWidthFromViewModel()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var width = Math.Clamp(viewModel.DetailPanelWidth, 240, 640);
        ProgrammeSplitGrid.ColumnDefinitions[2].Width = new GridLength(width, GridUnitType.Pixel);
        ProgrammeSplitGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
    }

    private void ApplyDetailPanelWidth()
        => ApplyDetailPanelWidthFromViewModel();

    private void TryEnableImmersiveDarkMode()
        => WindowChromeHelper.TryEnableImmersiveDarkMode(this);

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.IsRestoreWizardView)
        {
            return;
        }

        if (KeyboardNavigationHelper.IsTextInputFocused(this))
        {
            return;
        }

        var wizard = viewModel.RestoreWizard;

        if (e.Key == Key.Escape)
        {
            if (wizard.IsFortschrittStep && wizard.IsRunning)
            {
                return;
            }

            if (wizard.CancelWizardCommand.CanExecute(null))
            {
                e.Handled = true;
                wizard.CancelWizardCommand.Execute(null);
            }

            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            if (wizard.IsAuswahlStep && wizard.StartRestoreCommand.CanExecute(null))
            {
                e.Handled = true;
                wizard.StartRestoreCommand.Execute(null);
            }
            else if (wizard.IsErgebnisStep && wizard.FinishCommand.CanExecute(null))
            {
                e.Handled = true;
                wizard.FinishCommand.Execute(null);
            }
        }
    }
}
