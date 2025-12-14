using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Branchy.UI.Converters;

public sealed class BooleanToMaxHeightConverter : IValueConverter
{
    public static readonly BooleanToMaxHeightConverter Instance = new();

    private const double ExpandedHeight = 100;
    private const double CollapsedHeight = 0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? ExpandedHeight : CollapsedHeight;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double height)
        {
            return height > 0;
        }

        return false;
    }
}
