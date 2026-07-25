using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HorosSaver.Models;

namespace HorosSaver.Converters;

public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string color && Color.TryParse(color, out var parsed))
        {
            return new SolidColorBrush(parsed);
        }

        return new SolidColorBrush(Color.Parse("#00D2B4"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToNavBackgroundConverter : IValueConverter
{
    public static readonly BoolToNavBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = value is true;
        return isActive
            ? new SolidColorBrush(Color.Parse("#16324A"))
            : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToNavForegroundConverter : IValueConverter
{
    public static readonly BoolToNavForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = value is true;
        return isActive
            ? new SolidColorBrush(Color.Parse("#00D2B4"))
            : new SolidColorBrush(Color.Parse("#A8B8CC"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToNavAccentBarConverter : IValueConverter
{
    public static readonly BoolToNavAccentBarConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = value is true;
        return isActive
            ? new SolidColorBrush(Color.Parse("#00D2B4"))
            : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToNavFontWeightConverter : IValueConverter
{
    public static readonly BoolToNavFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontWeight.SemiBold : FontWeight.Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToCardBorderConverter : IValueConverter
{
    public static readonly BoolToCardBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        return isSelected
            ? new SolidColorBrush(Color.Parse("#00D2B4"))
            : new SolidColorBrush(Color.Parse("#1E3A52"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToCardBorderThicknessConverter : IValueConverter
{
    public static readonly BoolToCardBorderThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? new Thickness(2) : new Thickness(1);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToTimelineNodeBrushConverter : IValueConverter
{
    public static readonly BoolToTimelineNodeBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCurrent = value is true;
        return isCurrent
            ? new SolidColorBrush(Color.Parse("#00D2B4"))
            : new SolidColorBrush(Color.Parse("#2A3A4D"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToTimelineStatusDotBrushConverter : IValueConverter
{
    public static readonly BoolToTimelineStatusDotBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.Parse("#4ADE80"))
            : new SolidColorBrush(Color.Parse("#5A6B7D"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class DateTimeOffsetToGermanLabelConverter : IValueConverter
{
    public static readonly DateTimeOffsetToGermanLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset timestamp)
        {
            return "—";
        }

        var now = DateTimeOffset.Now;
        var delta = now - timestamp;

        if (delta.TotalMinutes < 1)
        {
            return "Gerade eben";
        }

        if (delta.TotalHours < 1)
        {
            return $"vor {(int)delta.TotalMinutes} Min.";
        }

        if (delta.TotalHours < 24)
        {
            return $"Heute {timestamp:HH:mm}";
        }

        if (delta.TotalDays < 2)
        {
            return $"Gestern {timestamp:HH:mm}";
        }

        return timestamp.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BytesToSizeLabelConverter : IValueConverter
{
    public static readonly BytesToSizeLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "—";
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var size = bytes / 1024d;
        if (size < 1024)
        {
            return $"{size:0.#} KB";
        }

        size /= 1024d;
        if (size < 1024)
        {
            return $"{size:0.#} MB";
        }

        size /= 1024d;
        return $"{size:0.#} GB";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NavIconKeyToGeometryConverter : IValueConverter
{
    public static readonly NavIconKeyToGeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            "programme" => Geometry.Parse("M4,4 H9 V9 H4 Z M11,4 H16 V9 H11 Z M4,11 H9 V16 H4 Z M11,11 H16 V16 H11 Z"),
            "snapshots" => Geometry.Parse("M4,5 H14 V7 H4 Z M4,9 H16 V11 H4 Z M4,13 H12 V15 H4 Z M14,12 A3,3 0 1,1 13.99,12 Z M14,11 V13 L15.5,14"),
            "timeline" => Geometry.Parse("M9,3 A6,6 0 1,1 8.99,3 Z M9,5 V9 L12,11"),
            "wiederherstellen" => Geometry.Parse("M12,4 L12,7 C15,7 17,9 17,12 C17,15 14,17 10,17 C7,17 5,15 4,13 M4,13 L6,11 M4,13 L6,15"),
            "settings" => Geometry.Parse("M10,2 L12,2 L12.6,4.2 L14.8,5 L16,3.2 L17.8,4.4 L16.9,6.6 L18,8.5 L16,9.5 L16,11.5 L18,12.5 L16.9,14.4 L17.8,16.6 L16,17.8 L14.8,16 L12.6,16.8 L12,19 L10,19 L9.4,16.8 L7.2,16 L5.4,17.8 L3.6,16.6 L4.5,14.4 L3.4,12.5 L5.4,11.5 L5.4,9.5 L3.4,8.5 L4.5,6.6 L3.6,4.4 L5.4,3.2 L7.2,5 L9.4,4.2 Z M10,8.5 A1.5,1.5 0 1,0 10,11.5 A1.5,1.5 0 1,0 10,8.5"),
            _ => Geometry.Parse("M4,4 H16 V16 H4 Z")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class DetailIconKeyToGeometryConverter : IValueConverter
{
    public static readonly DetailIconKeyToGeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            "extension" => Geometry.Parse("M8,4 H12 V8 H8 Z M4,8 H8 V12 H4 Z M12,8 H16 V12 H12 Z M8,12 H12 V16 H8 Z"),
            "settings" => Geometry.Parse("M10,2 L12,2 L12.6,4.2 L14.8,5 L16,3.2 L17.8,4.4 L16.9,6.6 L18,8.5 L16,9.5 L16,11.5 L18,12.5 L16.9,14.4 L17.8,16.6 L16,17.8 L14.8,16 L12.6,16.8 L12,19 L10,19 L9.4,16.8 L7.2,16 L5.4,17.8 L3.6,16.6 L4.5,14.4 L3.4,12.5 L5.4,11.5 L5.4,9.5 L3.4,8.5 L4.5,6.6 L3.6,4.4 L5.4,3.2 L7.2,5 L9.4,4.2 Z M10,8.5 A1.5,1.5 0 1,0 10,11.5 A1.5,1.5 0 1,0 10,8.5"),
            "keybinding" => Geometry.Parse("M6,6 H14 V8 H8 V10 H12 V12 H8 V14 H14 V16 H6 Z"),
            "workspace" => Geometry.Parse("M4,5 H16 V15 H4 Z M6,7 V13 H14 V7 Z"),
            "bookmark" => Geometry.Parse("M6,4 H14 V16 L10,13 L6,16 Z"),
            "profile" => Geometry.Parse("M9,4 A3,3 0 1,1 8.99,4 Z M5,14 C5,11 7,10 9,10 C11,10 13,11 13,14"),
            "container" => Geometry.Parse("M4,6 H16 V14 H4 Z M6,4 H14 V6 H6 Z"),
            "image" => Geometry.Parse("M4,5 H16 V15 H4 Z M7,8 A1.5,1.5 0 1,1 6.99,8 Z M6,13 L10,9 L14,13"),
            "volume" => Geometry.Parse("M4,8 H8 L12,5 V15 L8,12 H4 Z"),
            "library" => Geometry.Parse("M5,4 H8 V16 H5 Z M10,4 H13 V16 H10 Z M15,6 H18 V16 H15 Z"),
            "playtime" => Geometry.Parse("M9,3 A6,6 0 1,1 8.99,3 Z M9,5 V9 L12,11"),
            "tools" => Geometry.Parse("M14,3 L16,5 L13,8 C14,10 13,12 11,13 L9,15 L7,13 L9,11 C8,9 9,7 11,6 Z"),
            _ => Geometry.Parse("M4,9 H16 M4,12 H12")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class DiffKindToBrushConverter : IValueConverter
{
    public static readonly DiffKindToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HorosSaver.Models.SnapshotDiffKind kind)
        {
            return new SolidColorBrush(Color.Parse("#2A3A4D"));
        }

        var color = kind switch
        {
            HorosSaver.Models.SnapshotDiffKind.Added => "#1A3D38",
            HorosSaver.Models.SnapshotDiffKind.Removed => "#4D2A2A",
            HorosSaver.Models.SnapshotDiffKind.Changed => "#4D3F1A",
            _ => "#2A3A4D"
        };

        return new SolidColorBrush(Color.Parse(color));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.45;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class GridColumnsToItemWidthConverter : IMultiValueConverter
{
    public static readonly GridColumnsToItemWidthConverter Instance = new();
    private const double ColumnGap = 8;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1
            || values[0] is not double width
            || width <= 0)
        {
            return double.NaN;
        }

        var columns = values.Count >= 2 && values[1] is int boundColumns
            ? boundColumns
            : parameter switch
            {
                int paramInt => paramInt,
                string paramString when int.TryParse(paramString, NumberStyles.Integer, culture, out var parsed) => parsed,
                _ => 2
            };

        if (columns >= 3 && width < 720)
        {
            columns = 2;
        }

        if (columns <= 0)
        {
            return double.NaN;
        }

        return Math.Max(0, (width - (ColumnGap * columns)) / columns);
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToChevronConverter : IValueConverter
{
    public static readonly BoolToChevronConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▾" : "▸";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToExpandLabelConverter : IValueConverter
{
    public static readonly BoolToExpandLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Gruppe zuklappen" : "Gruppe aufklappen";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ProgramSnapshotDisplayStatusToBrushConverter : IValueConverter
{
    public static readonly ProgramSnapshotDisplayStatusToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is ProgramSnapshotDisplayStatus displayStatus
            ? displayStatus
            : ProgramSnapshotDisplayStatus.None;

        var color = status switch
        {
            ProgramSnapshotDisplayStatus.Current => "#4ADE80",
            ProgramSnapshotDisplayStatus.Outdated => "#C48B6A",
            ProgramSnapshotDisplayStatus.Partial => "#E6A23C",
            _ => "#FFB822"
        };

        return new SolidColorBrush(Color.Parse(color));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToGridRowConverter : IValueConverter
{
    public static readonly BoolToGridRowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0 : 1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToGridRowSpanConverter : IValueConverter
{
    public static readonly BoolToGridRowSpanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 2 : 1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolNegationConverter : IValueConverter
{
    public static readonly BoolNegationConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

public sealed class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text && !string.IsNullOrWhiteSpace(text);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolAndConverter : IMultiValueConverter
{
    public static readonly BoolAndConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count > 0 && values.All(value => value is true);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
