using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HorosSaver.Services;

public static class PreviewCaptureService
{
    public static async Task CaptureWindowAsync(Window window, string outputPath, int delayMs = 800)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(delayMs);

            var captureWidth = ResolveCaptureWidth(window);
            var captureHeight = ResolveCaptureHeight(window);

            ExpandScrollableContent(window, captureWidth);

            window.Measure(new Size(captureWidth, double.PositiveInfinity));
            var desiredHeight = Math.Max(captureHeight, window.DesiredSize.Height);
            captureHeight = desiredHeight;

            window.Measure(new Size(captureWidth, captureHeight));
            window.Arrange(new Rect(0, 0, captureWidth, captureHeight));

            if (window.Content is Grid hostGrid)
            {
                hostGrid.Measure(new Size(captureWidth, captureHeight));
                hostGrid.Arrange(new Rect(0, 0, captureWidth, captureHeight));
            }

            if (ShouldResizeSinglePreviewRegion(window.Content as Grid, out var region))
            {
                region.Height = captureHeight;
                region.Width = captureWidth;
                region.Measure(new Size(captureWidth, captureHeight));
                region.Arrange(new Rect(0, 0, captureWidth, captureHeight));
            }

            var width = Math.Max(1, (int)Math.Ceiling(captureWidth));
            var height = Math.Max(1, (int)Math.Ceiling(captureHeight));
            var pixelSize = new PixelSize(width, height);
            var dpi = window.RenderScaling;

            using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * dpi, 96 * dpi));
            bitmap.Render(window);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            await using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            bitmap.Save(stream);
        });
    }

    private static void ExpandScrollableContent(Control root, double width)
    {
        foreach (var scrollViewer in root.GetVisualDescendants().OfType<ScrollViewer>())
        {
            if (scrollViewer.Content is not Control content)
            {
                continue;
            }

            content.Measure(new Size(width, double.PositiveInfinity));
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static double MeasureContentHeight(Control control, double width)
    {
        control.Measure(new Size(width, double.PositiveInfinity));
        return control.DesiredSize.Height;
    }

    private static bool ShouldResizeSinglePreviewRegion(Grid? grid, out Control region)
    {
        region = null!;
        if (grid is null || grid.Children.Count != 1)
        {
            return false;
        }

        if (grid.RowDefinitions.Count > 1 || grid.ColumnDefinitions.Count > 1)
        {
            return false;
        }

        if (grid.Children[0] is not Control child)
        {
            return false;
        }

        region = child;
        return true;
    }

    private static double ResolveCaptureWidth(Window window)
    {
        if (window.ClientSize.Width > 1)
        {
            return window.ClientSize.Width;
        }

        if (window.Bounds.Width > 1)
        {
            return window.Bounds.Width;
        }

        return window.Width > 0 ? window.Width : 1280;
    }

    private static double ResolveCaptureHeight(Window window)
    {
        if (window.WindowState == WindowState.Maximized)
        {
            var screen = window.Screens.ScreenFromWindow(window);
            if (screen is not null)
            {
                return screen.WorkingArea.Height;
            }
        }

        if (window.Bounds.Height > 1)
        {
            return window.Bounds.Height;
        }

        if (window.ClientSize.Height > 1)
        {
            return window.ClientSize.Height;
        }

        return window.Height > 0 ? window.Height : 800;
    }
}
