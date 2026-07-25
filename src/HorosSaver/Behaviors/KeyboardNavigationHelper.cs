using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace HorosSaver.Behaviors;

public static class KeyboardNavigationHelper
{
    public static bool IsTextInputFocused(Visual? root)
    {
        var topLevel = root is not null ? TopLevel.GetTopLevel(root) : null;
        var focused = topLevel?.FocusManager?.GetFocusedElement();

        return focused is TextBox or AutoCompleteBox or ComboBox;
    }
}
