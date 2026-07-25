using Avalonia.Controls;

namespace HorosSaver.Views.Previews;

public partial class WindowChromePreview : Window
{
    public WindowChromePreview()
    {
        InitializeComponent();
        ChromeBar.Width = 1280;
        ChromeBar.Height = 32;
    }
}
