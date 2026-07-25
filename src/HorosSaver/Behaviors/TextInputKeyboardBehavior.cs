using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace HorosSaver.Behaviors;

/// <summary>
/// Prevents Enter/Escape in text fields from triggering window default/cancel buttons.
/// Escape clears non-empty text or removes focus; Enter removes focus.
/// </summary>
public static class TextInputKeyboardBehavior
{
    public static readonly AttachedProperty<bool> EnableNavigationKeysProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "EnableNavigationKeys",
            typeof(TextInputKeyboardBehavior),
            defaultValue: false);

    public static bool GetEnableNavigationKeys(TextBox element) =>
        element.GetValue(EnableNavigationKeysProperty);

    public static void SetEnableNavigationKeys(TextBox element, bool value) =>
        element.SetValue(EnableNavigationKeysProperty, value);

    static TextInputKeyboardBehavior()
    {
        EnableNavigationKeysProperty.Changed.AddClassHandler<TextBox>(OnEnableChanged);
    }

    private static void OnEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            textBox.KeyDown += OnKeyDown;
        }
        else if (e.OldValue is true)
        {
            textBox.KeyDown -= OnKeyDown;
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = string.Empty;
            }
            else
            {
                ClearTextBoxFocus(textBox);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            ClearTextBoxFocus(textBox);
            e.Handled = true;
        }
    }

    private static void ClearTextBoxFocus(TextBox textBox)
    {
        if (TopLevel.GetTopLevel(textBox) is InputElement topLevel)
        {
            topLevel.Focus(NavigationMethod.Unspecified);
        }
    }
}
