using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HorosSaver.Services;

namespace HorosSaver.Views.Regions;

public partial class WindowChromeBar : UserControl
{
    public static readonly StyledProperty<bool> ShowMinimizeButtonProperty =
        AvaloniaProperty.Register<WindowChromeBar, bool>(nameof(ShowMinimizeButton), true);

    public static readonly StyledProperty<bool> ShowMaximizeButtonProperty =
        AvaloniaProperty.Register<WindowChromeBar, bool>(nameof(ShowMaximizeButton), true);

    private Window? _hostWindow;

    public WindowChromeBar()
    {
        InitializeComponent();
    }

    public bool ShowMinimizeButton
    {
        get => GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public bool ShowMaximizeButton
    {
        get => GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachHostWindow(TopLevel.GetTopLevel(this) as Window);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachHostWindow();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowMinimizeButtonProperty || change.Property == ShowMaximizeButtonProperty)
        {
            SyncButtonVisibility();
        }
    }

    private void AttachHostWindow(Window? window)
    {
        DetachHostWindow();
        if (window is null)
        {
            return;
        }

        _hostWindow = window;
        _hostWindow.PropertyChanged += OnHostWindowPropertyChanged;
        _hostWindow.Opened += OnHostWindowOpened;

        SyncTitle();
        SyncButtonVisibility();
        WindowChromeHelper.TryEnableImmersiveDarkMode(window);
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.PropertyChanged -= OnHostWindowPropertyChanged;
        _hostWindow.Opened -= OnHostWindowOpened;
        _hostWindow = null;
    }

    private void OnHostWindowOpened(object? sender, EventArgs e)
    {
        if (_hostWindow is not null)
        {
            WindowChromeHelper.TryEnableImmersiveDarkMode(_hostWindow);
        }
    }

    private void OnHostWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.TitleProperty)
        {
            SyncTitle();
        }
        else if (e.Property == Window.CanResizeProperty)
        {
            SyncButtonVisibility();
        }
    }

    private void SyncTitle()
    {
        TitleTextBlock.Text = _hostWindow?.Title ?? string.Empty;
    }

    private void SyncButtonVisibility()
    {
        var canResize = _hostWindow?.CanResize ?? true;
        MinimizeButton.IsVisible = ShowMinimizeButton;
        MaximizeButton.IsVisible = ShowMaximizeButton && canResize;
    }

    private Window? HostWindow => _hostWindow ?? TopLevel.GetTopLevel(this) as Window;

    private void OnChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            HostWindow?.BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HostWindow is not null)
        {
            HostWindow.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HostWindow is not null)
        {
            HostWindow.WindowState = HostWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HostWindow?.Close();
    }
}
