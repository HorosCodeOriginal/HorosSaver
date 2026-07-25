using Avalonia;
using Avalonia.Controls;

namespace HorosSaver.Views.Regions;

public partial class SidebarRegion : UserControl
{
    public SidebarRegion()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Bounds.Height < 100 || Content is not Border border)
        {
            return;
        }

        if (Math.Abs(border.Height - Bounds.Height) > 1)
        {
            border.Height = Bounds.Height;
        }
    }
}
