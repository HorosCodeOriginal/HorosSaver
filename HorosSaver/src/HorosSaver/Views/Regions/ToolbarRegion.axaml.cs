using Avalonia;
using Avalonia.Controls;

namespace HorosSaver.Views.Regions;

public partial class ToolbarRegion : UserControl
{
    public ToolbarRegion()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Bounds.Width < 100 || Content is not Border border)
        {
            return;
        }

        if (Math.Abs(border.Width - Bounds.Width) > 1)
        {
            border.Width = Bounds.Width;
        }

        if (border.Child is Grid grid)
        {
            var innerWidth = Math.Max(0, Bounds.Width - 40);
            if (Math.Abs(grid.Width - innerWidth) > 1)
            {
                grid.Width = innerWidth;
            }
        }
    }
}
