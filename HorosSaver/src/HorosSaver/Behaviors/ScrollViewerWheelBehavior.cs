using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace HorosSaver.Behaviors;

/// <summary>
/// Enables mouse-wheel scrolling on a <see cref="ScrollViewer"/> when the pointer is over it,
/// even if keyboard focus is elsewhere (e.g. a search box).
/// </summary>
public static class ScrollViewerWheelBehavior
{
    public static readonly AttachedProperty<bool> EnableWheelOnHoverProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "EnableWheelOnHover",
            typeof(ScrollViewerWheelBehavior),
            defaultValue: false);

    public static bool GetEnableWheelOnHover(ScrollViewer element) =>
        element.GetValue(EnableWheelOnHoverProperty);

    public static void SetEnableWheelOnHover(ScrollViewer element, bool value) =>
        element.SetValue(EnableWheelOnHoverProperty, value);

    static ScrollViewerWheelBehavior()
    {
        EnableWheelOnHoverProperty.Changed.AddClassHandler<ScrollViewer>(OnEnableWheelOnHoverChanged);
    }

    private static void OnEnableWheelOnHoverChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            scrollViewer.Focusable = true;
            scrollViewer.PointerEntered += OnPointerEntered;
            scrollViewer.PointerMoved += OnPointerMoved;
            scrollViewer.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
        else if (e.OldValue is true)
        {
            scrollViewer.PointerEntered -= OnPointerEntered;
            scrollViewer.PointerMoved -= OnPointerMoved;
            scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        }
    }

    private static void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.Focus(NavigationMethod.Pointer);
        }
    }

    /// <summary>
    /// PointerEntered does not fire when the pointer is already over the list when the dialog opens.
    /// PointerMoved ensures the ScrollViewer can receive wheel input without a prior click.
    /// </summary>
    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var position = e.GetPosition(scrollViewer);
        if (!scrollViewer.Bounds.Contains(position))
        {
            return;
        }

        if (!scrollViewer.IsFocused)
        {
            scrollViewer.Focus(NavigationMethod.Pointer);
        }
    }

    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (!scrollViewer.IsVisible || !scrollViewer.IsEffectivelyVisible)
        {
            return;
        }

        var position = e.GetCurrentPoint(scrollViewer).Position;
        if (!scrollViewer.Bounds.Contains(position))
        {
            return;
        }

        var deltaY = e.Delta.Y;
        if (Math.Abs(deltaY) < double.Epsilon)
        {
            return;
        }

        if (!scrollViewer.IsFocused)
        {
            scrollViewer.Focus(NavigationMethod.Pointer);
        }

        EnsureScrollableExtent(scrollViewer);

        var maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        if (maxOffset < 1)
        {
            return;
        }

        var step = 48.0;
        var newY = Math.Clamp(scrollViewer.Offset.Y - deltaY * step, 0, maxOffset);

        if (Math.Abs(newY - scrollViewer.Offset.Y) < 0.01)
        {
            return;
        }

        scrollViewer.Offset = scrollViewer.Offset.WithY(newY);
        e.Handled = true;
    }

    /// <summary>
    /// After async content population the ScrollViewer extent can stay at viewport size until
    /// a layout-invalidating interaction (e.g. checkbox toggle). Force a measure pass first.
    /// </summary>
    private static void EnsureScrollableExtent(ScrollViewer scrollViewer)
    {
        var maxOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
        if (maxOffset >= 1)
        {
            return;
        }

        if (scrollViewer.Content is not Layoutable content)
        {
            return;
        }

        content.InvalidateMeasure();
        scrollViewer.InvalidateMeasure();
        scrollViewer.UpdateLayout();
    }
}
